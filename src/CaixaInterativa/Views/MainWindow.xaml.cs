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

using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CaixaInterativa.Config;
using CaixaInterativa.Contexto;
using CaixaInterativa.Diagnostico;
using CaixaInterativa.Depth;
using CaixaInterativa.Processing;
using CaixaInterativa.Simulation;

namespace CaixaInterativa.Views;

public partial class MainWindow : Window
{
    private readonly AppConfig _config;
    private readonly SandboxEngine _engine;
    private readonly DispatcherTimer _uiTimer;
    private ProjectionWindow? _projection;
    private SimulatedDepthSource? _simulator;
    private bool _loaded;

    public MainWindow()
    {
        InitializeComponent();

        _config = AppConfig.Load();
        _engine = new SandboxEngine(_config);
        _engine.BitmapReplaced += () => Preview.Source = _engine.Bitmap;
        _engine.StatusChanged += SetStatus;
        _engine.CalibrationCompleted += OnCalibrationCompleted;
        _engine.StateChanged += OnStateChanged;

        // Toda fonte que inicia recebe a cobertura que o combo está exibindo — inclusive as
        // que o motor inicia sozinho, na reconexão do sensor. As chamadas espalhadas pelos
        // caminhos da interface não alcançavam essa, e depois de o sensor cair e voltar a
        // tela passava a mentir sobre o que cobre o solo.
        _engine.SourceStarted += AplicarCoberturaSelecionada;

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += (_, _) => { AtualizarIndicadores(); AtualizarSimulacao(); };
        _uiTimer.Start();

        Loaded += OnWindowLoaded;
        Closed += (_, _) => { _engine.Dispose(); _projection?.Close(); };
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        // Identidade vem toda de AppInfo, para que tela e documentação não divirjam.
        Title = AppInfo.TituloDaJanela;
        TxtVersao.Text = AppInfo.VersaoExibida;
        TxtAssinatura.Text = AppInfo.Assinatura;
        LnkSuporte.NavigateUri = new Uri(AppInfo.LinkDeSuporte);
        LnkPagina.NavigateUri = new Uri(AppInfo.PaginaDoProjeto);
        LnkGithub.NavigateUri = new Uri(AppInfo.Repositorio);

        PreencherCombos();
        PopulateScreens();
        LoadSettingsIntoControls();
        ConfigPath.Text = AppConfig.DefaultPath;
        ExpAvancado.IsExpanded = !_config.Interface.SimpleMode;
        _loaded = true;

        DetectSensor();
        AtualizarResumoCalibracao();
        CarregarContexto();

        // OnStateChanged só dispara quando o estado MUDA. Ao abrir, o estado já é Parado
        // desde o construtor, então nenhum evento acontece e os botões ficam como o XAML
        // os deixou — todos habilitados, inclusive “Nivelar e calibrar”, que com a caixa
        // desligada só consegue responder “Inicie uma fonte antes de calibrar”.
        // Aplicar o estado uma vez aqui alinha a tela com a realidade.
        OnStateChanged(_engine.State, _engine.StateMessage);

        // Numa aula, abrir o programa deve bastar. Se há uma fonte configurada e ela
        // está disponível, subimos sozinhos — inclusive restaurando a calibração.
        if (_config.Sensor.AutoStart) Ligar(silencioso: true);
    }

    // ================= Ligar / desligar =================

    private void OnLigar(object sender, RoutedEventArgs e) => Ligar(silencioso: false);

    /// <summary>
    /// Escolhe a melhor fonte disponível sem obrigar o professor a saber qual é.
    /// Kinect quando houver; simulador como alternativa para ensaiar sem hardware.
    /// </summary>
    private void Ligar(bool silencioso)
    {
        if (_engine.State != EngineState.Parado)
        {
            SetStatus("A caixa já está ligada.");
            return;
        }

        bool temKinect = KinectV1Source.TryProbe(out _, out string motivo);

        if (temKinect)
        {
            try
            {
                _config.Sensor.Source = "kinect";
                bool near = ChkNearMode.IsChecked == true;
                int tilt = _config.Sensor.TiltAngle ?? int.MinValue;
                _engine.StartSource(() => new KinectV1Source(near, tilt));
                AplicarCoberturaSelecionada();
                return;
            }
            catch (Exception ex)
            {
                motivo = ex.Message;
            }
        }

        // "Não encontrado" era dito mesmo quando o driver tinha acabado de enumerar o
        // sensor — o caso real foi duas cópias do aplicativo abertas ao mesmo tempo. O
        // conselho fixo de conferir cabo e fonte mandava desmontar hardware que estava bom.
        //
        // Agora o cabeçalho pergunta ao driver antes de afirmar, e o conselho sobre cabo só
        // aparece quando nenhum sensor foi enumerado. Quando um foi, quem sabe o que fazer
        // é a mensagem do erro, que já vem específica de DescribeHResult.
        bool enumerado = KinectV1Source.TryProbe(out int quantos, out _) && quantos > 0;

        string cabecalho = enumerado
            ? "O Kinect está conectado, mas não pôde ser iniciado."
            : "Kinect não encontrado.";

        string conselho = enumerado
            ? "Depois de resolver, toque em “Ligar a caixa”."
            : "Verifique a fonte de energia e o cabo USB, e toque em “Ligar a caixa”.";

        if (silencioso)
        {
            // Na abertura automática não interrompemos com caixa de diálogo.
            TxtAjuda.Text = $"{cabecalho}\n\n{motivo}\n\n{conselho}";
            SetStatus((enumerado ? "Kinect não iniciou — " : "Kinect indisponível — ") + motivo);
            return;
        }

        var escolha = MessageBox.Show(
            $"{cabecalho}\n\n{motivo}\n\n" +
            "Deseja usar o simulador? Ele reproduz o funcionamento sem o sensor, " +
            "para você preparar a aula ou testar a projeção.",
            "Caixa de Areia Interativa",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (escolha == MessageBoxResult.Yes) IniciarSimulador();
    }

    private void OnParar(object sender, RoutedEventArgs e)
    {
        _engine.StopSource();
        SetStatus("Caixa desligada.");
    }

    // ================= Sensor (modo avançado) =================

    private void OnDetect(object sender, RoutedEventArgs e) => DetectSensor();

    private void DetectSensor()
    {
        bool ok = KinectV1Source.TryProbe(out int count, out string message);
        SensorStatus.Text = message;
        BtnStartKinect.IsEnabled = ok;
        if (ok) SetStatus($"Kinect pronto ({count}).");
    }

    private void OnStartKinect(object sender, RoutedEventArgs e)
    {
        try
        {
            _config.Sensor.Source = "kinect";
            _config.Sensor.NearMode = ChkNearMode.IsChecked == true;
            bool near = _config.Sensor.NearMode;
            int tilt = _config.Sensor.TiltAngle ?? int.MinValue;
            _engine.StartSource(() => new KinectV1Source(near, tilt));
            AplicarCoberturaSelecionada();
        }
        catch (Exception ex)
        {
            SensorStatus.Text = ex.Message;
            SetStatus("Falha ao iniciar o Kinect.");
        }
    }

    private void OnStartSimulator(object sender, RoutedEventArgs e) => IniciarSimulador();

    private void IniciarSimulador()
    {
        _config.Sensor.Source = "simulador";
        _engine.StartSource(() =>
        {
            _simulator = new SimulatedDepthSource
            {
                ReliefScale = ChkFlatSim.IsChecked == true ? 0.0 : 1.0
            };
            return _simulator;
        });
        AplicarCoberturaSelecionada();
    }

    private void OnFlatSimChanged(object sender, RoutedEventArgs e)
    {
        if (_simulator is null) return;
        _simulator.ReliefScale = ChkFlatSim.IsChecked == true ? 0.0 : 1.0;
    }

    // ================= Simulações =================

    /// <summary>
    /// Catálogo de simulações. Chuva e terremoto são fenômenos que o professor dispara;
    /// alagamento e deslizamento são resultados que eles produzem, não itens do menu —
    /// tratá-los como opções separadas confundiria causa com efeito.
    /// </summary>
    private enum Simulacao { Chuva, Terremoto, Queimada }

    private Simulacao _simulacaoAtual = Simulacao.Chuva;
    /// <summary>
    /// Uma execução registrada, com a assinatura do relevo em que ela aconteceu.
    ///
    /// A assinatura é o que permite dizer se a comparação entre duas execuções isola de
    /// fato a variável estudada. Sem ela, o histórico afirmaria que a diferença veio da
    /// cobertura mesmo quando veio da areia ter sido remodelada no intervalo.
    /// </summary>
    private readonly record struct Execucao(
        string Simulacao,
        string Cobertura,
        string Resultado,
        double Valor,
        AssinaturaDoRelevo? Relevo);

    private readonly List<Execucao> _historico = [];
    private string _coberturaAtual = "Mata";
    private bool _chovendoAntes, _tremendoAntes, _queimandoAntes;

    private void PreencherCombos()
    {
        CmbSimulacao.Items.Clear();
        CmbSimulacao.Items.Add("Chuva e enchente");
        CmbSimulacao.Items.Add("Terremoto");
        CmbSimulacao.Items.Add("Queimada");
        CmbSimulacao.SelectedIndex = 0;

        CmbIntensidade.Items.Clear();
        foreach (var n in new[] { "Garoa", "Chuva forte", "Tempestade" })
            CmbIntensidade.Items.Add(n);
        CmbIntensidade.SelectedIndex = 1;

        // O catálogo de coberturas vem do próprio modelo, para que acrescentar um tipo
        // de solo não exija lembrar de editar a interface.
        CmbCobertura.Items.Clear();
        foreach (var tipo in PropriedadesDoSolo.Todos)
            CmbCobertura.Items.Add(PropriedadesDoSolo.De(tipo).Nome);
        CmbCobertura.SelectedIndex = 0;
    }

    private void OnSimulacaoChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        _simulacaoAtual = CmbSimulacao.SelectedIndex switch
        {
            1 => Simulacao.Terremoto,
            2 => Simulacao.Queimada,
            _ => Simulacao.Chuva,
        };

        CfgChuva.Visibility = _simulacaoAtual == Simulacao.Chuva ? Visibility.Visible : Visibility.Collapsed;
        CfgTremor.Visibility = _simulacaoAtual == Simulacao.Terremoto ? Visibility.Visible : Visibility.Collapsed;
        CfgFogo.Visibility = _simulacaoAtual == Simulacao.Queimada ? Visibility.Visible : Visibility.Collapsed;

        TxtSimulacaoInfo.Text = _simulacaoAtual switch
        {
            Simulacao.Terremoto =>
                "Ondas sísmicas a partir do centro da caixa. O dano depende do solo e da encosta.",
            Simulacao.Queimada =>
                "O fogo começa onde há vegetação e se espalha conforme o vento, a encosta e " +
                "o que existe para queimar. Água no caminho segura a frente de chama.",
            _ => "Chuva sobre todo o território. A água escorre, acumula e alaga.",
        };

        AtualizarBotaoExecutar();
    }

    private void OnConfigChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LblDuracao is not null) LblDuracao.Text = $"{SldDuracao.Value:F0}s";
        if (LblMagnitude is not null) LblMagnitude.Text = $"{SldMagnitude.Value:F1}";
        if (LblVento is not null) LblVento.Text = $"{SldVento.Value:F2}";
    }

    private void OnCoberturaChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_loaded || _engine.Agua is null) return;

        AplicarCoberturaSelecionada();
        SetStatus($"Cobertura: {_coberturaAtual}. Execute a simulação para ver o efeito.");
    }

    /// <summary>
    /// Escreve no mapa de cobertura o que o combo está mostrando.
    ///
    /// Precisa ser chamado sempre que uma fonte inicia, porque cada <c>StartSource</c> cria
    /// uma <c>WaterSimulation</c> nova, e o construtor dela preenche o solo com areia. O
    /// combo, enquanto isso, continua exibindo o primeiro item da lista — "Mata".
    ///
    /// Sem esta sincronização o programa abre mentindo: a primeira chuva cai sobre areia
    /// enquanto o professor lê "Mata" na tela, e o histórico de comparação registra o
    /// resultado como se fosse de mata. Na queimada o sintoma é mais visível — atear fogo
    /// responde "não há vegetação que possa queimar" com "Mata" escrito logo acima.
    /// Reproduzido na validação visual de 28/08/2026, antes de existir esta chamada.
    /// </summary>
    private void AplicarCoberturaSelecionada()
    {
        if (_engine.Agua is null) return;

        int i = Math.Clamp(CmbCobertura.SelectedIndex, 0, PropriedadesDoSolo.Todos.Length - 1);
        var tipo = PropriedadesDoSolo.Todos[i];
        var prop = PropriedadesDoSolo.De(tipo);

        _coberturaAtual = prop.Nome;
        _engine.Agua.Solo.Preencher(tipo);
        Registro.Info($"Cobertura do terreno: {prop.Nome}");

        // A ressalva vai junto do parâmetro, e não só no código-fonte: sem ela o professor
        // lê uma comparação didática como se fosse dado hidrológico medido.
        TxtCoberturaInfo.Text = prop.Descricao + "\n" + prop.Resumo
                              + "\n" + PropriedadesDoSolo.AvisoDidatico;
    }

    private static (float MmPorSegundo, string Nome) IntensidadeChuva(int indice) => indice switch
    {
        0 => (3f, "Garoa"),
        2 => (18f, "Tempestade"),
        _ => (8f, "Chuva forte"),
    };

    private void OnExecutarSimulacao(object sender, RoutedEventArgs e)
    {
        if (_engine.State == EngineState.Parado)
        {
            SetStatus("Ligue a caixa antes de executar uma simulação.");
            return;
        }

        if (!_engine.IsCalibrated)
        {
            MessageBox.Show(
                "A caixa ainda não foi calibrada, então o sistema não conhece o relevo — " +
                "a água não saberia para onde escorrer nem onde estariam as encostas.\n\n" +
                "Alise a areia, toque em “Nivelar e calibrar” e tente de novo.",
                AppInfo.Nome, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        switch (_simulacaoAtual)
        {
            case Simulacao.Chuva: ExecutarChuva(); break;
            case Simulacao.Queimada: ExecutarQueimada(); break;
            default: ExecutarTerremoto(); break;
        }
    }

    private void ExecutarChuva()
    {
        var agua = _engine.Agua;
        if (agua is null) return;

        if (agua.Chovendo)
        {
            agua.PararChuva();
            SetStatus("Chuva interrompida. A água continua escoando.");
            return;
        }

        var (mm, nome) = IntensidadeChuva(CmbIntensidade.SelectedIndex);
        float duracao = (float)SldDuracao.Value;
        agua.IniciarChuva(mm, duracao);
        Registro.Info($"Chuva: {nome} ({mm:F0} mm/s) por {duracao:F0}s sobre {_coberturaAtual}");
        SetStatus($"{nome} por {duracao:F0}s sobre {_coberturaAtual}. Observe por onde a água desce.");
    }

    private void ExecutarTerremoto()
    {
        var sismo = _engine.Terremoto;
        if (sismo is null || sismo.EmAndamento) return;

        sismo.Disparar(0.5f, 0.5f, (float)SldMagnitude.Value);
        Registro.Info($"Terremoto: magnitude {SldMagnitude.Value:F1} sobre {_coberturaAtual}");
        SetStatus($"Terremoto de magnitude {SldMagnitude.Value:F1} sobre {_coberturaAtual}.");
    }

    /// <summary>
    /// Ateia fogo. O foco cai num ponto sorteado entre os que têm o que queimar — a
    /// própria simulação escolhe, para o incêndio não começar no asfalto e não pegar.
    /// </summary>
    private void ExecutarQueimada(float? u = null, float? v = null)
    {
        var fogo = _engine.Fogo;
        if (fogo is null || fogo.EmAndamento) return;

        fogo.VentoForca = (float)SldVento.Value;

        if (!fogo.Atear(u, v))
        {
            // Duas causas, dois conselhos opostos: tocar no mar pede outro lugar, e
            // cobertura sem combustível pede trocar a cobertura. Um "não pegou" genérico
            // mandaria mexer na coisa errada em metade das vezes.
            string aviso = fogo.PontoRecusado switch
            {
                FireSimulation.MotivoDaRecusa.NoMar =>
                    "Ali é mar.\n\nO fogo não atravessa água — escolha um ponto na parte " +
                    "seca do relevo, acima da linha d’água.",

                // O caso que a caixa de verdade encontrou: a cobertura queima, mas não
                // sobrou terra seca. Mandar trocar a cobertura aqui seria mandar consertar
                // o que não está quebrado.
                FireSimulation.MotivoDaRecusa.TudoNoMar =>
                    $"A cobertura ({_coberturaAtual}) queima, mas o relevo está todo abaixo " +
                    "da linha d’água.\n\nLevante areia para formar terra seca — ou aumente " +
                    "“Profundidade dos vales”, em Ajustes técnicos, para baixar o nível do mar.",

                _ =>
                    $"A cobertura atual ({_coberturaAtual}) não tem vegetação que queime.\n\n" +
                    "Escolha Mata, Pastagem ou Agricultura e ateie o fogo de novo.",
            };

            MessageBox.Show(aviso, AppInfo.Nome, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string onde = u is null ? "num ponto sorteado" : "no ponto que você escolheu";
        SetStatus($"Fogo ateado {onde}, sobre {_coberturaAtual}. " +
                  $"Vento de {fogo.VentoPorExtenso()}. Observe por onde a frente avança.");
    }

    /// <summary>
    /// Clique na prévia: ateia fogo naquele ponto do relevo.
    ///
    /// A conversão precisa desfazer duas coisas antes de chegar ao ponto do território.
    /// A primeira é o <c>Stretch="Uniform"</c>: a imagem cabe dentro do controle com
    /// tarjas, e um clique na tarja não é um clique no mapa. A segunda é a ROI — o que
    /// aparece é um recorte do campo do sensor, e a simulação trabalha no campo inteiro.
    /// </summary>
    private void OnPreviewClique(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_simulacaoAtual != Simulacao.Queimada) return;
        if (Preview.Source is not System.Windows.Media.Imaging.BitmapSource bmp) return;

        var p = e.GetPosition(Preview);

        // Desfaz o encaixe uniforme.
        double escala = Math.Min(Preview.ActualWidth / bmp.PixelWidth,
                                 Preview.ActualHeight / bmp.PixelHeight);
        if (escala <= 0) return;

        double larguraVisivel = bmp.PixelWidth * escala;
        double alturaVisivel = bmp.PixelHeight * escala;
        double margemX = (Preview.ActualWidth - larguraVisivel) / 2;
        double margemY = (Preview.ActualHeight - alturaVisivel) / 2;

        double px = (p.X - margemX) / escala;
        double py = (p.Y - margemY) / escala;

        // Clique na tarja não é clique no mapa.
        if (px < 0 || py < 0 || px >= bmp.PixelWidth || py >= bmp.PixelHeight) return;

        // Da ROI de volta para o campo inteiro do sensor.
        var proj = _engine.Config.Projection;
        int larguraCampo = _engine.LarguraCampo;
        int alturaCampo = _engine.AlturaCampo;
        if (larguraCampo <= 0 || alturaCampo <= 0) return;

        double campoX = proj.RoiLeft + px;
        double campoY = proj.RoiTop + py;

        ExecutarQueimada((float)(campoX / larguraCampo), (float)(campoY / alturaCampo));
    }

    private void OnSecar(object sender, RoutedEventArgs e)
    {
        // Genérico: limpa todos os módulos registrados. Antes eram duas linhas por
        // fenômeno aqui, e o fogo tinha ficado de fora da limpeza.
        _engine.LimparSimulacoes();
        SetStatus("Simulação limpa. O terreno continua como está.");
        AtualizarSimulacao();
    }

    private void AtualizarBotaoExecutar()
    {
        bool ocupado = _engine.Terremoto?.EmAndamento == true
                       || _engine.Fogo?.EmAndamento == true;
        BtnExecutar.IsEnabled = !ocupado;

        BtnExecutar.Content = _simulacaoAtual switch
        {
            Simulacao.Chuva when _engine.Agua?.Chovendo == true => "⏸  Parar a chuva",
            Simulacao.Chuva => "🌧  Fazer chover",
            Simulacao.Queimada => "🔥  Atear fogo",
            _ => "⚡  Provocar terremoto",
        };
    }

    /// <summary>Estado das simulações, em linguagem de aula.</summary>
    private void AtualizarSimulacao()
    {
        AtualizarBotaoExecutar();

        var agua = _engine.Agua;
        var sismo = _engine.Terremoto;

        // Registra o resultado na transição executando -> parou, quando o pico do
        // episódio está fechado e ainda não foi diluído pelo escoamento.
        if (agua is not null)
        {
            if (_chovendoAntes && !agua.Chovendo)
                Registrar("Chuva", _coberturaAtual, $"{agua.PicoAlagamentoPercent:F0}% alagado", agua.PicoAlagamentoPercent);
            _chovendoAntes = agua.Chovendo;
        }
        if (sismo is not null)
        {
            if (_tremendoAntes && !sismo.EmAndamento)
                Registrar("Terremoto", _coberturaAtual, $"{sismo.AreaDeslizamentoPercent:F1}% de deslizamento", sismo.AreaDeslizamentoPercent);
            _tremendoAntes = sismo.EmAndamento;
        }

        var fogo = _engine.Fogo;
        if (fogo is not null)
        {
            if (_queimandoAntes && !fogo.EmAndamento)
                Registrar("Queimada", _coberturaAtual,
                          $"{fogo.AreaQueimadaPercent:F0}% queimado", fogo.AreaQueimadaPercent);
            _queimandoAntes = fogo.EmAndamento;
        }

        if (_simulacaoAtual == Simulacao.Queimada && fogo is not null)
        {
            TxtResultado.Text = fogo.EmAndamento
                ? $"Queimando… {fogo.TempoDecorrido:F0}s\n" +
                  $"Área queimada: {fogo.AreaQueimadaPercent:F0}%\n" +
                  $"Vento de {fogo.VentoPorExtenso()}"
                : fogo.Ativo && fogo.AreaQueimadaPercent > 0
                    ? $"O fogo apagou.\nÁrea queimada: {fogo.AreaQueimadaPercent:F0}%\n" +
                      "O solo queimado repele a água — faça chover para ver a diferença."
                    : "Nenhuma simulação executada.";
            return;
        }

        if (_simulacaoAtual == Simulacao.Terremoto && sismo is not null)
        {
            TxtResultado.Text = sismo.EmAndamento
                ? $"Tremendo… {sismo.TempoDecorrido:F1}s\nÁrea afetada: {sismo.AreaAfetadaPercent:F0}%"
                : sismo.Ativo && sismo.AreaAfetadaPercent > 0
                    ? $"Magnitude {sismo.Magnitude:F1} sobre {_coberturaAtual}\n" +
                      $"Área afetada: {sismo.AreaAfetadaPercent:F0}%\n" +
                      $"Risco de deslizamento: {sismo.AreaDeslizamentoPercent:F1}%"
                    : "Nenhuma simulação executada.";
            return;
        }

        if (agua is null) { TxtResultado.Text = "Nenhuma simulação executada."; return; }

        TxtResultado.Text = agua.Chovendo
            ? $"Chovendo… {agua.ChuvaRestanteSegundos:F0}s\nÁrea alagada: {agua.AreaAlagadaPercent:F0}%"
            : agua.Ativo && agua.VolumeLitros > 0.01
                ? $"Escoando · {Litros(agua.VolumeLitros)}\n" +
                  $"Alagado: {agua.AreaAlagadaPercent:F0}%  ·  pico {agua.PicoAlagamentoPercent:F0}%\n" +
                  $"Infiltrado: {Litros(agua.InfiltradoLitros)}" + NotaDeEstimativa
                : agua.Ativo && agua.PicoAlagamentoPercent > 0
                    ? $"A água escoou.\nPico de alagamento: {agua.PicoAlagamentoPercent:F0}%\n" +
                      $"Infiltrado: {Litros(agua.InfiltradoLitros)}" + NotaDeEstimativa
                    : "Nenhuma simulação executada.";
    }

    /// <summary>
    /// Volume em litros, marcado como estimativa enquanto a caixa não for medida.
    ///
    /// O valor deriva do tamanho da célula, que deriva da largura que o sensor cobre —
    /// um número que hoje é suposição. O erro entra ao quadrado, porque a área da célula
    /// é o lado ao quadrado. As porcentagens não passam por essa conta e continuam
    /// confiáveis, por isso não levam a marca.
    /// </summary>
    private string Litros(double valor)
        => _config.Caixa.LarguraMedida ? $"{valor:F1} L" : $"≈ {valor:F1} L";

    private string NotaDeEstimativa
        => _config.Caixa.LarguraMedida
            ? ""
            : "\n≈ estimativa: depende da largura da caixa, ainda não medida.";

    private void Registrar(string simulacao, string cobertura, string resultado, double valor)
    {
        if (valor <= 0) return;
        _historico.RemoveAll(h => h.Simulacao == simulacao && h.Cobertura == cobertura);

        // A assinatura é tirada agora, do relevo em que esta execução aconteceu.
        // Calcular médias já produz uma cópia, então não guardamos referência ao buffer
        // vivo do engine.
        var relevo = AssinaturaDoRelevo.De(
            _engine.Alturas, _engine.LarguraCampo, _engine.AlturaCampo);

        _historico.Add(new Execucao(simulacao, cobertura, resultado, valor, relevo));

        // O resultado de cada episódio fica registrado: é o que permite reconstruir uma
        // aula depois, e comparar o que aconteceu em turmas diferentes.
        Registro.Info($"Resultado — {simulacao} sobre {cobertura}: {resultado}");
        AtualizarComparacao();
    }

    private void AtualizarComparacao()
    {
        if (_historico.Count == 0)
        {
            TxtComparacao.Text = "Execute a mesma simulação em coberturas diferentes para comparar.";
            return;
        }

        var linhas = _historico
            .OrderBy(h => h.Simulacao).ThenBy(h => h.Valor)
            .Select(h => $"{h.Simulacao} · {h.Cobertura}: {h.Resultado}");

        string texto = string.Join("\n", linhas);

        // A conclusão da aula é a razão entre o melhor e o pior cenário da mesma
        // simulação — o número que responde se a intervenção adiantou.
        //
        // Mas essa frase só é honesta se o relevo tiver ficado igual entre as duas
        // execuções. Se a areia mudou no intervalo, a diferença observada não é
        // atribuível à cobertura, e dizer que é seria ensinar algo errado.
        foreach (var grupo in _historico.GroupBy(h => h.Simulacao).Where(g => g.Count() >= 2))
        {
            var melhor = grupo.MinBy(h => h.Valor);
            var pior = grupo.MaxBy(h => h.Valor);
            if (melhor.Valor <= 0.01) continue;

            texto += $"\n\n{pior.Cobertura} teve {pior.Valor / melhor.Valor:F1}× " +
                     $"o resultado de {melhor.Cobertura}, na mesma simulação.";

            texto += AvisoDeRelevo(melhor.Relevo, pior.Relevo);
        }

        TxtComparacao.Text = texto;
    }

    /// <summary>
    /// A ressalva que acompanha uma comparação, conforme o relevo tenha ficado igual ou
    /// não entre as duas execuções.
    ///
    /// Nunca bloqueia nada: o número comparado continua na tela. O que muda é o que se
    /// pode concluir dele — e isso é conteúdo de aula, não obstáculo. Um estudante que
    /// descobre que precisa manter o terreno fixo para comparar coberturas aprendeu
    /// controle de variáveis sem ninguém precisar usar a expressão.
    /// </summary>
    private static string AvisoDeRelevo(AssinaturaDoRelevo? a, AssinaturaDoRelevo? b)
    {
        if (a is null || b is null)
            return "\n⚠ Não foi possível verificar se o relevo continuou o mesmo entre " +
                   "as duas execuções.";

        var comparacao = a.Comparar(b);

        return comparacao.MesmoRelevo
            ? "\n✓ O relevo era o mesmo nas duas execuções, então a diferença vem da " +
              "cobertura."
            : $"\n⚠ O relevo mudou entre as duas execuções — até " +
              $"{comparacao.DiferencaMaximaMm:F0} mm de diferença em alguma região. " +
              "Parte do que mudou pode vir da areia, não da cobertura. Para comparar " +
              "só a cobertura, repita sem mexer no terreno.";
    }

    private void OnLimparComparacao(object sender, RoutedEventArgs e)
    {
        _historico.Clear();
        AtualizarComparacao();
    }

    // ================= Calibração =================

    private void OnCalibrate(object sender, RoutedEventArgs e)
    {
        if (_engine.State == EngineState.Parado)
        {
            SetStatus("Ligue a caixa antes de calibrar.");
            return;
        }
        TxtCalibResumo.Text = "Calibrando… não mexa na areia.";
        _engine.CalibrateBase();
    }

    private void OnResetCalibration(object sender, RoutedEventArgs e)
    {
        var confirmar = MessageBox.Show(
            "Apagar a calibração salva? Você precisará nivelar a areia e calibrar de novo.",
            "Caixa de Areia Interativa",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmar != MessageBoxResult.Yes) return;

        _engine.ResetCalibration();
        AtualizarResumoCalibracao();
    }

    private void OnCalibrationCompleted(double averageMm)
    {
        AtualizarResumoCalibracao();

        if (_engine.CoveragePercent < 80)
        {
            MessageBox.Show(
                $"A calibração ficou com {_engine.CoveragePercent:F0}% de cobertura, " +
                "abaixo do recomendado (80%).\n\n" +
                "Isso deixa falhas no mapa. Verifique:\n\n" +
                "• o sensor está perpendicular à caixa, a 0,9–1,2 m da areia\n" +
                "• não há sol direto na caixa\n" +
                "• a areia está seca e fosca\n\n" +
                "A calibração foi salva mesmo assim, e você pode refazê-la a qualquer momento.",
                "Cobertura baixa",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AtualizarResumoCalibracao()
    {
        var quando = CalibrationStore.SavedAt();

        // Depois de calibrada, a instrução já cumpriu o papel. Ela some para devolver
        // duas linhas de altura à barra lateral, que numa tela de 768px fazem diferença.
        TxtCalibAjuda.Visibility = _engine.IsCalibrated
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (_engine.IsCalibrated)
        {
            string idade = quando is null
                ? "nesta sessão"
                : quando.Value.ToString("dd/MM 'às' HH:mm");
            TxtCalibResumo.Text =
                $"Calibrada em {idade}\n{_engine.CoveragePercent:F0}% de cobertura";
            BtnCalibrar.Content = "Calibrar de novo";
        }
        else if (quando is not null)
        {
            TxtCalibResumo.Text =
                $"Há uma calibração salva de {quando.Value:dd/MM 'às' HH:mm}.\n" +
                "Ligue a caixa para carregá-la.";
            BtnCalibrar.Content = "Nivelar e calibrar";
        }
        else
        {
            TxtCalibResumo.Text = "Ainda não calibrada.";
            BtnCalibrar.Content = "Nivelar e calibrar";
        }
    }

    // ================= Estado =================

    private void OnStateChanged(EngineState estado, string mensagem)
    {
        TxtEstado.Text = mensagem;

        (Color cor, string detalhe) = estado switch
        {
            EngineState.Pronto          => (Color.FromRgb(0x4C, 0xC3, 0x8A), "tudo funcionando"),
            EngineState.PrecisaCalibrar => (Color.FromRgb(0xE0, 0xB0, 0x4A), "falta calibrar"),
            EngineState.Calibrando      => (Color.FromRgb(0x4F, 0xA3, 0xE3), "aguarde"),
            EngineState.Reconectando    => (Color.FromRgb(0xE0, 0xB0, 0x4A), "tentando religar"),
            EngineState.Erro            => (Color.FromRgb(0xE0, 0x6A, 0x55), "precisa de atenção"),
            _                           => (Color.FromRgb(0x6B, 0x6B, 0x78), "desligada"),
        };

        LuzEstado.Fill = new SolidColorBrush(cor);
        TxtEstadoDetalhe.Text = detalhe;

        // O resumo lateral também muda quando a calibração é carregada sozinha,
        // não só quando o professor calibra à mão.
        AtualizarResumoCalibracao();

        BtnLigar.IsEnabled = estado == EngineState.Parado;
        BtnCalibrar.IsEnabled = estado is EngineState.Pronto or EngineState.PrecisaCalibrar;
        BtnParar.IsEnabled = estado != EngineState.Parado;

        TxtAjuda.Text = estado switch
        {
            EngineState.Parado =>
                "Toque em “Ligar a caixa” para começar.",
            EngineState.PrecisaCalibrar =>
                "A caixa está lendo o sensor.\n\n" +
                "Alise a areia, tire as mãos da caixa e toque em “Nivelar e calibrar”.",
            EngineState.Calibrando =>
                "Calibrando…\n\nNão mexa na areia e mantenha as mãos fora da caixa.",
            EngineState.Reconectando =>
                "O sensor foi desconectado.\n\n" +
                "Verifique o cabo USB e a fonte de energia. A reconexão é automática.",
            EngineState.Erro =>
                "Algo deu errado com o sensor.\n\n" + _engine.StateMessage,
            _ => "",
        };

        AtualizarIndicadores();
    }

    private void AtualizarIndicadores()
    {
        bool temImagem = _engine.Bitmap is not null && _engine.State != EngineState.Parado;
        PainelAjuda.Visibility = temImagem ? Visibility.Collapsed : Visibility.Visible;

        FpsText.Text = _engine.Fps > 0
            ? $"{_engine.Fps:F0} fps  ·  {_engine.SourceName}"
            : "";
    }

    // ================= Projeção =================

    private void PopulateScreens()
    {
        CmbScreen.Items.Clear();
        var screens = System.Windows.Forms.Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            var b = screens[i].Bounds;
            string primary = screens[i].Primary ? " (principal)" : "";
            CmbScreen.Items.Add($"{i}: {b.Width}x{b.Height}{primary}");
        }
        CmbScreen.SelectedIndex = Math.Clamp(_config.Projection.ScreenIndex, 0, Math.Max(0, screens.Length - 1));
    }

    private void OnScreenChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_loaded || CmbScreen.SelectedIndex < 0) return;
        _config.Projection.ScreenIndex = CmbScreen.SelectedIndex;
        _projection?.MoveToScreen(CmbScreen.SelectedIndex);
    }

    private void OnToggleProjection(object sender, RoutedEventArgs e)
    {
        if (_projection is not null)
        {
            _projection.Close();
            return;
        }

        // Só um monitor significa que a projeção vai cobrir a tela do professor.
        // Melhor avisar que deixar a pessoa achar que travou.
        if (System.Windows.Forms.Screen.AllScreens.Length < 2)
        {
            var seguir = MessageBox.Show(
                "Só há um monitor conectado, então a projeção vai ocupar esta tela inteira.\n\n" +
                "Para sair dela, aperte Esc.\n\nContinuar?",
                "Caixa de Areia Interativa",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (seguir != MessageBoxResult.Yes) return;
        }

        _projection = new ProjectionWindow(_engine);
        _projection.SaveRequested += SaveConfig;
        _projection.CalibrateRequested += () => _engine.CalibrateBase();
        _projection.Closed += (_, _) =>
        {
            _projection = null;
            BtnProjetar.Content = "🖵  Abrir projeção";
        };
        _projection.Show();
        BtnProjetar.Content = "🖵  Fechar projeção";
        SetStatus("Projeção aberta. F1 mostra os atalhos de alinhamento.");
    }

    // ================= Ajustes =================

    private void OnAvancadoToggled(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        _config.Interface.SimpleMode = !ExpAvancado.IsExpanded;
    }

    private void LoadSettingsIntoControls()
    {
        var p = _config.Processing;
        var r = _config.Render;

        SldMaxHeight.Value = p.MaxHeightMm;
        SldMinHeight.Value = p.MinHeightMm;
        SldBlur.Value = p.SpatialBlurRadius;
        SldAlpha.Value = p.SmoothingAlpha;
        SldContour.Value = r.ContourIntervalMm;
        ChkHillshade.IsChecked = r.HillshadeEnabled;
        ChkNearMode.IsChecked = _config.Sensor.NearMode;
        ChkAutoStart.IsChecked = _config.Sensor.AutoStart;
        ChkAutoCalib.IsChecked = _config.Sensor.AutoLoadCalibration;
        ChkAutoReconnect.IsChecked = _config.Sensor.AutoReconnect;

        UpdateLabels();
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;

        var p = _config.Processing;
        var r = _config.Render;

        p.MaxHeightMm = (float)SldMaxHeight.Value;
        p.MinHeightMm = (float)SldMinHeight.Value;
        p.SpatialBlurRadius = (int)SldBlur.Value;
        p.SmoothingAlpha = (float)SldAlpha.Value;
        r.ContourIntervalMm = (float)SldContour.Value;
        r.HillshadeEnabled = ChkHillshade.IsChecked == true;

        _config.Sensor.AutoStart = ChkAutoStart.IsChecked == true;
        _config.Sensor.AutoLoadCalibration = ChkAutoCalib.IsChecked == true;
        _config.Sensor.AutoReconnect = ChkAutoReconnect.IsChecked == true;

        UpdateLabels();
    }

    private void UpdateLabels()
    {
        LblMaxHeight.Text = $"{SldMaxHeight.Value:F0} mm";
        LblMinHeight.Text = $"{SldMinHeight.Value:F0} mm";
        LblContour.Text = SldContour.Value < 0.5 ? "sem" : $"{SldContour.Value:F0} mm";
        LblBlur.Text = $"{SldBlur.Value:F0}";
        LblAlpha.Text = $"{SldAlpha.Value:F2}";
    }

    // ================= Contexto real (experimental) =================

    private ResultadoDoCarregamento? _contexto;

    /// <summary>
    /// Carrega o pacote de contexto do arquivo local, uma vez, ao abrir.
    ///
    /// Se não houver pacote, ou se ele estiver corrompido ou numa versão que este programa
    /// não entende, a seção informa e some de cena. Contexto externo é enfeite pedagógico:
    /// nada aqui pode impedir a caixa de funcionar.
    /// </summary>
    private void CarregarContexto()
    {
        _contexto = LeitorDeContexto.Carregar();

        CmbContexto.Items.Clear();
        foreach (var c in _contexto.Contextos) CmbContexto.Items.Add(c.Rotulo);

        bool temContexto = _contexto.Contextos.Count > 0;
        CmbContexto.IsEnabled = temContexto;

        TxtContextoErro.Text = temContexto
            ? ""
            : _contexto.Erro ?? "Nenhum contexto disponível neste pacote.";
    }

    private void OnContextoChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_loaded || _contexto is null) return;

        int i = CmbContexto.SelectedIndex;
        if (i < 0 || i >= _contexto.Contextos.Count)
        {
            BoxContexto.Visibility = Visibility.Collapsed;
            BoxAtividade.Visibility = Visibility.Collapsed;
            return;
        }

        var c = _contexto.Contextos[i];
        var obs = c.Observado;
        var classes = c.ClassesDidaticas;

        // Número observado e classe lado a lado. O número fica porque é auditável; a
        // classe fica porque é o que a aula usa. Nenhum dos dois vira parâmetro.
        //
        // A auditoria pedagógica mexeu só no vocabulário. "(mediana)" virou "valor
        // típico" — mesma conta, e a procedência continua dizendo qual é. "Potência
        // radiativa" virou "calor liberado". O risco de fogo passou a mostrar a classe
        // primeiro e o índice depois, com a escala à vista: sozinho, "1,00" é lido como
        // "100% de chance de incêndio", que não é o que o índice do INPE afirma.
        var linhas = new List<string>
        {
            c.RotuloPorExtenso,
            "",
            $"Focos de calor vistos por satélite: {obs?.Focos:N0}",
        };

        if (obs?.DiasSemChuvaMediano is double dias)
            linhas.Add($"Dias sem chuva (valor típico): {dias:F0}  →  {classes?.Secura}");

        if (obs?.RiscoFogoMediano is double risco)
            linhas.Add($"Risco de fogo: {classes?.Risco}  (índice {risco:F2}, numa escala de 0 a 1)");

        if (obs?.FrpMedianoMw is double frp)
            linhas.Add($"Calor liberado pelos focos (valor típico): {frp:F1} MW");

        // "relativa_ao_recorte" era o identificador do campo aparecendo cru na tela.
        // Aqui vai o que ele significa, que é o que muda a leitura: as classes comparam
        // este território com os outros do pacote, e não com um padrão do INPE.
        if (classes is not null)
        {
            linhas.Add("");
            linhas.Add($"“{classes.Secura}” e “{classes.Risco}” comparam este território " +
                       "com os outros deste pacote — não são categorias oficiais do INPE.");
        }

        TxtContextoObservado.Text = string.Join("\n", linhas);
        TxtRelevoNaoEOTerritorio.Text = AtividadeConceitual.RelevoNaoRepresentaOTerritorio;

        var p = _contexto.Pacote?.Proveniencia;
        var origem = p?.Origem(c.Periodo);

        TxtContextoProcedencia.Text = p is null
            ? ""
            : string.Join("\n",
                [
                    p.Resumo,
                    origem is null
                        ? $"Origem do período {c.Periodo}: não declarada no pacote."
                        : $"Arquivo: {origem.Recurso} — {origem.DiasObservados} dias observados" +
                          (origem.AmostraParcial ? " (mês incompleto)" : ""),
                    // Sem ponto antes de "Onde": o método já vem pontuado do pacote.
                    $"Agregação: {p.MetodoDeAgregacao} Onde a tela diz “valor típico”, " +
                    "o número é a mediana do recorte.",
                    $"Classificação: {p.MetodoDeClassificacao}",
                    .. p.Observacoes,
                ]);

        BoxContexto.Visibility = Visibility.Visible;
        PrepararComparacao(c);
        MostrarAtividade(_comparaveis.Count > 0
            ? AtividadeConceitual.MesmoTerritorioPeriodosDiferentes
            : AtividadeConceitual.QueimadasNoCerrado);
    }

    /// <summary>Os contextos do mesmo território em outros períodos.</summary>
    private List<ContextoTerritorial> _comparaveis = [];

    /// <summary>
    /// Oferece os outros períodos do mesmo território, se houver.
    ///
    /// Só aparece quando existe com o que comparar — um combo vazio na tela sugere que
    /// falta alguma coisa, quando na verdade o pacote é de um período só.
    /// </summary>
    private void PrepararComparacao(ContextoTerritorial a)
    {
        _comparaveis = (_contexto?.Contextos ?? [])
            .Where(outro => ComparadorDeContextos.SaoCompativeis(a, outro))
            .OrderBy(outro => outro.Periodo, StringComparer.Ordinal)
            .ToList();

        CmbContextoB.SelectionChanged -= OnContextoBChanged;
        CmbContextoB.Items.Clear();
        foreach (var outro in _comparaveis) CmbContextoB.Items.Add(outro.Periodo);
        CmbContextoB.SelectionChanged += OnContextoBChanged;

        var visivel = _comparaveis.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        LblComparar.Visibility = visivel;
        CmbContextoB.Visibility = visivel;
        BoxComparacao.Visibility = Visibility.Collapsed;
    }

    private void OnContextoBChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_loaded || _contexto is null) return;

        int ia = CmbContexto.SelectedIndex;
        int ib = CmbContextoB.SelectedIndex;
        if (ia < 0 || ia >= _contexto.Contextos.Count || ib < 0 || ib >= _comparaveis.Count)
        {
            BoxComparacao.Visibility = Visibility.Collapsed;
            return;
        }

        var comparacao = ComparadorDeContextos.Comparar(_contexto.Contextos[ia], _comparaveis[ib]);
        if (comparacao is null)
        {
            BoxComparacao.Visibility = Visibility.Collapsed;
            return;
        }

        // O título dizia o território e os dois períodos, e deixava o leitor deduzir o que
        // a caixa verde era. Agora ele diz o que a caixa é; o território virou subtítulo.
        TxtComparacaoTitulo.Text = "O QUE MUDOU ENTRE OS DOIS PERÍODOS";

        string a = ContextoTerritorial.PeriodoPorExtenso(comparacao.PeriodoA);
        string b = ContextoTerritorial.PeriodoPorExtenso(comparacao.PeriodoB);

        TxtComparacaoCampos.Text = string.Join("\n",
        [
            $"{comparacao.Bioma} · {comparacao.Uf}",
            $"{a}  →  {b}",
            "",
            .. comparacao.Campos.Select(campo => campo.Descrever()),
        ]);

        // A ressalva de não causalidade acompanha toda comparação. Junto vai a origem de
        // cada período: quantos dias cada um representa muda o que a contagem significa.
        var avisos = new List<string> { ComparacaoDeContextos.AvisoDeNaoCausalidade };

        foreach (string periodo in new[] { comparacao.PeriodoA, comparacao.PeriodoB })
        {
            var origem = _contexto.Pacote?.Proveniencia?.Origem(periodo);
            if (origem is null) continue;

            avisos.Add($"{ContextoTerritorial.PeriodoPorExtenso(periodo)}: " +
                       $"{origem.DiasObservados} dias observados ({origem.Recurso})" +
                       (origem.AmostraParcial ? " — mês incompleto" : ""));
        }

        TxtComparacaoAviso.Text = string.Join("\n", avisos);
        BoxComparacao.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Mostra a atividade conceitual. Ela apenas <b>descreve</b> de onde vem cada parte —
    /// não configura cobertura, vento nem chuva. Ligar contexto observado a parâmetro de
    /// simulação é decisão pedagógica que precisa ser tomada de propósito.
    /// </summary>
    private void MostrarAtividade(AtividadeConceitual a)
    {
        TxtAtividadeTitulo.Text = a.Titulo;
        TxtAtividadePergunta.Text = a.Pergunta;
        TxtAtividadeObservacao.Text = a.Observacao;
        TxtAtividadeHipotese.Text = a.Hipotese;
        TxtAtividadeExperimento.Text = a.Experimento;

        // Os rótulos de natureza vão no texto, não só na cor: quem imprime a tela, quem
        // lê por cima do ombro e quem usa leitor de tela precisa da mesma separação.
        TxtAtividadeOrigens.Text = string.Join("\n\n",
            "DADO EXTERNO OBSERVADO — " + a.DeOndeVemOContexto,
            "MEDIÇÃO DA CAIXA — " + a.DeOndeVemORelevo,
            "MODELO DIDÁTICO — " + a.DeOndeVemAPropagacao);

        BoxAtividade.Visibility = Visibility.Visible;
    }

    private void OnSave(object sender, RoutedEventArgs e) => SaveConfig();

    private void SaveConfig()
    {
        try
        {
            _config.Save();
            SetStatus($"Configuração salva em {AppConfig.DefaultPath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Falha ao salvar: {ex.Message}");
        }
    }

    private void SetStatus(string message) => StatusText.Text = message;

    /// <summary>
    /// Abre links no navegador ou no cliente de e-mail do sistema. WPF não faz isso
    /// sozinho: sem UseShellExecute o Hyperlink simplesmente não reage ao clique.
    /// </summary>
    private void OnAbrirLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Sem navegador padrão configurado, por exemplo. Mostrar o endereço é mais
            // útil que uma falha silenciosa — a pessoa pode copiar à mão.
            MessageBox.Show(
                $"Não foi possível abrir o link automaticamente.\n\n{e.Uri.AbsoluteUri}\n\n({ex.Message})",
                AppInfo.Nome, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        e.Handled = true;
    }
}
