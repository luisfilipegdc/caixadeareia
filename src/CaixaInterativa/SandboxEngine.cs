// Caixa de Areia Interativa — sistema de projeção topográfica interativa
// Copyright (C) 2026 Projeto Caixa de Areia
//
// Este programa é software livre: você pode redistribuí-lo e/ou modificá-lo
// sob os termos da Licença Pública Geral GNU, conforme publicada pela Free
// Software Foundation, na versão 2 da Licença ou (a seu critério) qualquer
// versão posterior.
//
// Este programa é distribuído na esperança de que seja útil, mas SEM QUALQUER
// GARANTIA; sem sequer a garantia implícita de COMERCIALIZAÇÃO ou ADEQUAÇÃO A
// UMA FINALIDADE ESPECÍFICA. Consulte a Licença Pública Geral GNU para mais
// detalhes. Uma cópia acompanha este programa no arquivo LICENSE.

using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CaixaInterativa.Config;
using CaixaInterativa.Diagnostico;
using CaixaInterativa.Depth;
using CaixaInterativa.Processing;
using CaixaInterativa.Rendering;
using CaixaInterativa.Simulation;

namespace CaixaInterativa;

/// <summary>
/// Orquestra sensor, processamento e renderizacao, e publica o resultado como um
/// WriteableBitmap que qualquer janela pode exibir.
///
/// O sensor entrega quadros numa thread propria; a UI consome no seu proprio ritmo.
/// Guardamos apenas o quadro mais recente em vez de enfileirar: se a renderizacao
/// atrasar, queremos descartar quadros velhos, nao acumular latencia. Numa caixa de
/// areia, atraso e' pior que perda - o aluno mexe a mao e espera a cor mudar agora.
/// </summary>
public sealed class SandboxEngine : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly TopographicRenderer _renderer = new();
    private readonly Stopwatch _fpsClock = Stopwatch.StartNew();

    private IDepthSource? _source;
    private DepthProcessor? _processor;
    private float[] _heights = [];
    private RawDepthFrame? _latestFrame;
    private WriteableBitmap? _bitmap;

    private int _framesSinceTick;
    private long _lastRenderedFrameNumber = -1;
    private readonly Stopwatch _simClock = Stopwatch.StartNew();

    /// <summary>Simulação de água. Criada junto com a fonte, porque depende da resolução.</summary>
    public WaterSimulation? Agua { get; private set; }

    /// <summary>Simulação de terremoto, compartilhando o mapa de solo com a água.</summary>
    public EarthquakeSimulation? Terremoto { get; private set; }

    /// <summary>Simulação de queimada, que altera o mapa de solo ao terminar.</summary>
    public FireSimulation? Fogo { get; private set; }

    private readonly List<ISimulationModule> _modulos = new(3);

    /// <summary>
    /// Os módulos de simulação, na ordem em que são atualizados e desenhados.
    ///
    /// O ciclo de quadro (atualizar, coletar camadas, limpar) percorre esta lista e não
    /// conhece nenhum módulo pelo nome. As propriedades concretas acima continuam
    /// existindo porque a interface precisa de controles próprios de cada fenômeno —
    /// intensidade de chuva, magnitude — e generalizar isso exigiria um sistema de
    /// parâmetros que não cabe neste passo.
    ///
    /// **Invariante:** a ordem desta lista precisa produzir <see cref="CamadaVisual.Ordem"/>
    /// crescente na concatenação. Hoje água (100) · terremoto (200, 210) · fogo (300).
    /// Um módulo novo respeita a ordem ou o engine passa a ordenar — nunca o renderizador,
    /// que ordenaria dentro do laço de pixels.
    /// </summary>
    public IReadOnlyList<ISimulationModule> Modulos => _modulos;

    /// <summary>Campo de alturas atual, para quem precisa consultar o relevo.</summary>
    public float[] Alturas => _heights;
    public int LarguraCampo => _source?.Width ?? 0;
    public int AlturaCampo => _source?.Height ?? 0;

    public AppConfig Config { get; }
    public WriteableBitmap? Bitmap => _bitmap;
    public double Fps { get; private set; }
    public string SourceName => _source?.Name ?? "nenhuma";
    public bool IsCalibrated => _processor?.IsCalibrated ?? false;
    public bool IsCalibrating => _processor?.IsCalibrating ?? false;
    public double CoveragePercent => _processor?.CoveragePercent ?? 0;

    public event Action? BitmapReplaced;
    public event Action<string>? StatusChanged;
    public event Action<double>? CalibrationCompleted;

    /// <summary>Estado geral para a interface pintar de verde, amarelo ou vermelho.</summary>
    public event Action<EngineState, string>? StateChanged;

    /// <summary>
    /// Passo de tempo fixo, em segundos, para execuções que precisam ser comparáveis.
    ///
    /// Nulo em uso normal, e a simulação anda no relógio. A atividade oficial o define
    /// enquanto executa, porque sem ele A e B recebem sequências de passos diferentes e a
    /// diferença entre as duas deixa de ser atribuível à cobertura.
    /// </summary>
    public float? PassoFixoSegundos { get; set; }

    /// <summary>
    /// Identidade da sessão da fonte. Muda a cada <see cref="StartSource(IDepthSource)"/>.
    ///
    /// Cada início de fonte cria uma <c>WaterSimulation</c> nova, com solo e saturação
    /// zerados. Um resultado medido antes disso não é comparável com um medido depois, e
    /// é este número que permite a uma comparação perceber a troca sem guardar referência
    /// a objeto nenhum.
    /// </summary>
    public int SessaoDaFonte { get; private set; }

    /// <summary>
    /// Uma fonte nova comecou. Quem observa precisa reaplicar o que so' a interface sabe.
    ///
    /// Existe por causa de um defeito encontrado com o sensor ligado: cada StartSource cria
    /// uma WaterSimulation nova, e o construtor dela preenche o solo com areia. As chamadas
    /// manuais a AplicarCoberturaSelecionada cobriam os caminhos da interface, mas nao o do
    /// timer de reconexao, que chama StartSource de dentro do motor. Depois de o sensor cair
    /// e voltar, o combo continuava exibindo "Pastagem" enquanto o solo era areia — e atear
    /// fogo respondia "nao ha' vegetacao que possa queimar" com Pastagem escrito acima.
    /// </summary>
    public event Action? SourceStarted;

    private EngineState _state = EngineState.Parado;
    public EngineState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(value, StateMessage);
        }
    }

    public string StateMessage { get; private set; } = "Parado";

    private void SetState(EngineState estado, string mensagem)
    {
        StateMessage = mensagem;
        if (_state == estado) { StateChanged?.Invoke(estado, mensagem); return; }
        State = estado;
    }

    // Reconexão automática
    private Func<IDepthSource>? _sourceFactory;
    private DispatcherTimer? _reconnectTimer;
    private int _reconnectAttempts;

    public SandboxEngine(AppConfig config)
    {
        Config = config;

        // ~60Hz de tentativa; o sensor entrega 30, entao metade dos ticks nao faz nada.
        // Deixar o timer mais rapido que a fonte tira ate 16ms de latencia percebida.
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _timer.Tick += OnTick;
    }

    /// <summary>
    /// Inicia uma fonte. A fábrica é guardada para permitir reconexão automática:
    /// quando o sensor cai, criamos uma instância nova em vez de reusar a que falhou.
    /// </summary>
    public void StartSource(Func<IDepthSource> factory)
    {
        _sourceFactory = factory;
        _reconnectAttempts = 0;
        StartSource(factory());
    }

    public void StartSource(IDepthSource source)
    {
        StopSource();
        StopReconnectTimer();

        _source = source;
        SessaoDaFonte++;
        _processor = new DepthProcessor(source.Width, source.Height)
        {
            Settings = Config.Processing
        };
        _processor.CalibrationCompleted += d =>
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Salva sozinho: uma calibracao que existe so na memoria se perde ao
                // fechar o programa, e o professor recomeca do zero na aula seguinte.
                SaveCalibration();
                SetState(EngineState.Pronto, "Pronto");
                CalibrationCompleted?.Invoke(d);
            });
        };

        _heights = new float[source.Width * source.Height];
        // A largura vem da configuração em vez de um literal escondido no construtor.
        // O padrão é o mesmo 1250 mm de antes, então nada muda até alguém medir.
        float larguraMm = Config.Caixa.LarguraCobertaPeloSensorMm;
        Agua = new WaterSimulation(source.Width, source.Height, larguraMm);
        // Os dois módulos leem o mesmo mapa de cobertura: mudar de mata para solo solto
        // deve afetar a enchente e o terremoto ao mesmo tempo, como no território real.
        Terremoto = new EarthquakeSimulation(source.Width, source.Height, larguraMm) { Solo = Agua.Solo };
        // O fogo lê a água para saber onde não pode passar, e escreve no solo a cicatriz
        // que a chuva seguinte vai encontrar.
        Fogo = new FireSimulation(source.Width, source.Height)
        {
            Solo = Agua.Solo,
            Agua = Agua.Profundidade,
            // A mesma faixa de alturas que o renderizador usa para pintar o mapa. E' assim
            // que o fogo sabe onde comeca o azul: sem ela, o incendio atravessaria o mar.
            AlturaMinimaMm = Config.Processing.MinHeightMm,
            AlturaMaximaMm = Config.Processing.MaxHeightMm,
        };

        _nearMode.Reiniciar();
        _avisouNearMode = false;

        // A ordem de registro é a ordem de atualização e de composição visual.
        _modulos.Clear();
        _modulos.Add(Agua);
        _modulos.Add(Terremoto);
        _modulos.Add(Fogo);
        _latestFrame = null;
        _lastRenderedFrameNumber = -1;

        source.FrameArrived += OnFrameArrived;
        source.Faulted += OnFaulted;

        source.Start();
        _timer.Start();
        StatusChanged?.Invoke($"Fonte iniciada: {source.Name}");
        Registro.Info($"Fonte iniciada: {source.Name} ({source.Width}x{source.Height})");

        // Restaura a calibração salva antes de qualquer coisa: se houver uma válida,
        // o professor abre o programa e já vê o relevo, sem passar pela calibração.
        bool restaurada = false;
        if (Config.Sensor.AutoLoadCalibration)
            restaurada = TryLoadCalibration();

        SetState(restaurada ? EngineState.Pronto : EngineState.PrecisaCalibrar,
                 restaurada
                     ? $"Pronto — calibração de {CalibrationAge()} carregada"
                     : "Nivele a areia e toque em Calibrar");

        // Por ultimo: quem escuta reaplica a cobertura escolhida sobre o solo recem-criado.
        SourceStarted?.Invoke();
    }

    /// <summary>Restaura a calibração gravada em disco, se houver e se servir.</summary>
    public bool TryLoadCalibration()
    {
        if (_processor is null || _source is null) return false;

        var dados = CalibrationStore.Load(_source.Width, _source.Height);
        if (dados is null) return false;
        if (!_processor.Import(dados)) return false;

        StatusChanged?.Invoke(
            $"Calibração de {dados.SavedAt:dd/MM HH:mm} carregada " +
            $"({dados.CoveragePercent:F0}% de cobertura).");
        Registro.Info($"Calibração carregada de {dados.SavedAt:dd/MM HH:mm}, " +
                      $"{dados.CoveragePercent:F1}% de cobertura");
        return true;
    }

    /// <summary>Grava a calibração atual para as próximas aulas.</summary>
    public bool SaveCalibration()
    {
        if (_processor is null || _source is null) return false;

        var dados = _processor.Export(_source.Name);
        if (dados is null) return false;

        try
        {
            CalibrationStore.Save(dados);
            StatusChanged?.Invoke("Calibração salva. Nas próximas vezes ela é carregada sozinha.");
            Registro.Info($"Calibração salva: {dados.CoveragePercent:F1}% de cobertura, " +
                          $"distância média {dados.AverageDistanceMm:F0} mm");
            if (dados.CoveragePercent < 80)
                Registro.Aviso($"Cobertura abaixo do recomendado: {dados.CoveragePercent:F1}%");
            return true;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"Não foi possível salvar a calibração: {ex.Message}");
            return false;
        }
    }

    private string CalibrationAge()
    {
        var quando = CalibrationStore.SavedAt();
        if (quando is null) return "antes";
        var idade = DateTime.Now - quando.Value;
        if (idade.TotalMinutes < 60) return "agora há pouco";
        if (idade.TotalHours < 24) return $"{idade.Hours}h atrás";
        if (idade.TotalDays < 2) return "ontem";
        return $"{(int)idade.TotalDays} dias atrás";
    }

    public void StopSource()
    {
        _timer.Stop();
        if (_source is null) return;
        SetState(EngineState.Parado, "Parado");

        _source.FrameArrived -= OnFrameArrived;
        _source.Faulted -= OnFaulted;
        _source.Dispose();
        _source = null;
    }

    private void OnFrameArrived(RawDepthFrame frame) => Volatile.Write(ref _latestFrame, frame);

    private void OnFaulted(string message)
        => Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusChanged?.Invoke(message);
            Registro.Erro($"Sensor caiu: {message}");
            SetState(EngineState.Erro, "Sensor desconectado");
            if (Config.Sensor.AutoReconnect) StartReconnectTimer();
        });

    /// <summary>
    /// Tenta religar o sensor sozinho. Um cabo esbarrado ou uma queda momentânea de
    /// energia no sensor não deveria encerrar a aula e obrigar o professor a mexer no
    /// computador na frente da turma.
    /// </summary>
    private void StartReconnectTimer()
    {
        if (_sourceFactory is null || _reconnectTimer is not null) return;

        _reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _reconnectTimer.Tick += (_, _) =>
        {
            _reconnectAttempts++;
            SetState(EngineState.Reconectando,
                     $"Reconectando ao sensor… (tentativa {_reconnectAttempts})");

            try
            {
                var nova = _sourceFactory!();
                StartSource(nova);   // limpa o timer e restaura a calibração
                StatusChanged?.Invoke($"Sensor reconectado após {_reconnectAttempts} tentativa(s).");
                Registro.Info($"Sensor reconectado após {_reconnectAttempts} tentativa(s).");
                _reconnectAttempts = 0;
            }
            catch (Exception ex)
            {
                // Segue tentando. Desistir depois de N tentativas deixaria o sistema
                // morto justamente quando o problema é temporário — um sensor que
                // demora a inicializar depois de reconectado, por exemplo.
                StatusChanged?.Invoke($"Tentativa {_reconnectAttempts} falhou: {ex.Message}");
            }
        };
        _reconnectTimer.Start();
    }

    private void StopReconnectTimer()
    {
        if (_reconnectTimer is null) return;
        _reconnectTimer.Stop();
        _reconnectTimer = null;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var frame = Volatile.Read(ref _latestFrame);
        if (frame is null || _processor is null) return;
        if (frame.FrameNumber == _lastRenderedFrameNumber) return;
        _lastRenderedFrameNumber = frame.FrameNumber;

        _processor.Settings = Config.Processing;
        _processor.ProcessFrame(frame, _heights);

        ObservarNearMode(frame);

        // dt real, limitado a 100ms. Um travamento momentâneo não pode virar um salto
        // que despeja meio segundo de chuva de uma vez e estoura a simulação.
        float dtReal = (float)Math.Min(0.1, _simClock.Elapsed.TotalSeconds);
        _simClock.Restart();

        // Numa execução controlada o passo deixa de vir do relógio.
        //
        // Medido: a mesma chuva sobre o mesmo relevo, só mudando o passo, deu pico de
        // 0,0% a 9,9% numa encosta e de 67,6% a 79,8% numa bacia fechada. É mais variação
        // do que a troca de cobertura produz — o solver usa substeps limitados a 12 pelo
        // CFL, e passos grandes saem da faixa em que ele é preciso.
        //
        // Com passo fixo o resultado é exatamente reprodutível: três execuções deram
        // 79,6875 nas três casas. A simulação passa a andar em tempo de simulação, não em
        // tempo de parede — a 29 fps, vinte segundos de chuva levam ~20,7s de relógio.
        float dt = PassoFixoSegundos ?? dtReal;

        // O fogo lê a água para saber onde não pode passar, e `WaterSimulation` troca o
        // buffer de profundidade a cada substep (`MoverAgua` faz o swap com `_aguaNova`).
        // A referência entregue uma única vez em `StartSource` ficava defasada: medido,
        // em 7 de 20 quadros ela apontava para o buffer anterior. Reapontar aqui custa
        // uma atribuição e mantém a barreira de água lendo o estado corrente.
        if (Fogo is not null && Agua is not null) Fogo.Agua = Agua.Profundidade;

        // Genérico: o laço não sabe quantos módulos existem nem quais são. Acrescentar
        // um fenômeno deixa de exigir uma linha aqui.
        for (int i = 0; i < _modulos.Count; i++)
        {
            var modulo = _modulos[i];
            if (modulo.Ativo) modulo.Atualizar(_heights, frame.Width, frame.Height, dt);
        }

        // Os deslizadores de altura mexem nesta faixa durante a aula, e e' ela que define
        // onde o mapa vira mar. Copiar so' na construcao deixaria o fogo parando numa cota
        // que nao corresponde mais ao azul que esta' na tela.
        if (Fogo is not null)
        {
            Fogo.AlturaMinimaMm = Config.Processing.MinHeightMm;
            Fogo.AlturaMaximaMm = Config.Processing.MaxHeightMm;
        }

        ColetarCamadas();

        var pixels = _renderer.Render(
            _heights, frame.Width, frame.Height,
            Config.Projection, Config.Processing, Config.Render,
            _camadasDoQuadro);

        EnsureBitmap(_renderer.Width, _renderer.Height);

        _bitmap!.WritePixels(
            new Int32Rect(0, 0, _renderer.Width, _renderer.Height),
            pixels, _renderer.Stride, 0);

        _framesSinceTick++;
        if (_fpsClock.ElapsedMilliseconds >= 500)
        {
            Fps = _framesSinceTick * 1000.0 / _fpsClock.ElapsedMilliseconds;
            _framesSinceTick = 0;
            _fpsClock.Restart();
        }
    }

    /// <summary>
    /// Camadas visuais do quadro. Lista reaproveitada entre quadros: <c>Clear</c> zera a
    /// contagem sem devolver o array, então o caminho de renderização não aloca depois do
    /// primeiro quadro.
    /// </summary>
    private readonly List<CamadaVisual> _camadasDoQuadro = new(4);

    /// <summary>
    /// Junta as camadas dos módulos ativos, na ordem em que precisam ser desenhadas.
    ///
    /// A ordem sai daqui já correta e o renderizador não ordena nada: os módulos são
    /// percorridos na sequência água → terremoto → fogo, e as camadas de cada um já vêm
    /// em ordem crescente, o que dá 100 · 200, 210 · 300. Acrescentar um módulo exige
    /// respeitar essa invariante — ou passar a ordenar aqui, nunca por quadro no laço
    /// de pixels.
    /// </summary>
    private void ColetarCamadas()
    {
        _camadasDoQuadro.Clear();

        for (int i = 0; i < _modulos.Count; i++)
        {
            var modulo = _modulos[i];
            if (modulo.Ativo) Acrescentar(modulo.Camadas);
        }
    }

    /// <summary>
    /// Encerra todas as simulações e devolve a caixa ao mapa topográfico puro. O relevo
    /// não é tocado: continua sendo o que está fisicamente na areia.
    /// </summary>
    public void LimparSimulacoes()
    {
        for (int i = 0; i < _modulos.Count; i++)
        {
            _modulos[i].Limpar();
            _modulos[i].Ativo = false;
        }
    }

    /// <summary>
    /// Por índice, e não com <c>foreach</c>: percorrer um <see cref="IReadOnlyList{T}"/>
    /// com foreach aloca um enumerador por chamada, e isto roda a cada quadro.
    /// </summary>
    private void Acrescentar(IReadOnlyList<CamadaVisual> camadas)
    {
        for (int i = 0; i < camadas.Count; i++) _camadasDoQuadro.Add(camadas[i]);
    }

    /// <summary>
    /// Diagnóstico do near mode. Vazio até uma fonte iniciar.
    /// </summary>
    private readonly DiagnosticoDeNearMode _nearMode = new();

    /// <summary>Menor profundidade válida observada desde que a fonte iniciou, em mm.</summary>
    public ushort MenorProfundidadeMm => _nearMode.MinimaObservadaMm;

    private bool _avisouNearMode;

    /// <summary>
    /// Observa os primeiros quadros para saber se apareceu alguma leitura abaixo dos
    /// 800 mm que o sensor entrega sem near mode.
    ///
    /// É indício, não prova: se a areia estiver toda a mais de 80 cm, a mínima fica acima
    /// de 800 mm com o near mode funcionando. Por isso o resultado sai como aviso no
    /// status, uma única vez, e não interrompe nada.
    /// </summary>
    private void ObservarNearMode(RawDepthFrame frame)
    {
        if (_avisouNearMode) return;

        _nearMode.Observar(frame.Data,
                           Config.Processing.MinValidDepthMm,
                           Config.Processing.MaxValidDepthMm);

        if (!_nearMode.Concluido) return;

        // Só faz sentido perguntar isso do sensor real; o simulador não tem near mode.
        bool sensorReal = _source is KinectV1Source;
        string? aviso = _nearMode.Aviso(sensorReal && Config.Sensor.NearMode);

        _avisouNearMode = true;
        if (aviso is not null) StatusChanged?.Invoke(aviso);
    }

    private void EnsureBitmap(int width, int height)
    {
        if (_bitmap is not null && _bitmap.PixelWidth == width && _bitmap.PixelHeight == height)
            return;

        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        BitmapReplaced?.Invoke();
    }

    public void CalibrateBase(int frames = 60)
    {
        if (_processor is null)
        {
            StatusChanged?.Invoke("Inicie uma fonte antes de calibrar.");
            return;
        }
        _processor.BeginBaseCalibration(frames);
        SetState(EngineState.Calibrando, "Calibrando — não mexa na areia");
        StatusChanged?.Invoke($"Calibrando plano-base ({frames} quadros)... nao mexa na areia.");
    }

    public void ResetCalibration()
    {
        _processor?.ResetCalibration();
        CalibrationStore.Delete();
        SetState(EngineState.PrecisaCalibrar, "Nivele a areia e toque em Calibrar");
        StatusChanged?.Invoke("Calibracao descartada.");
    }

    public void Dispose()
    {
        StopReconnectTimer();
        StopSource();
        _timer.Tick -= OnTick;
    }
}
