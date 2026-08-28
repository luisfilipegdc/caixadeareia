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

        if (silencioso)
        {
            // Na abertura automática não interrompemos com caixa de diálogo.
            TxtAjuda.Text =
                "Kinect não encontrado.\n\n" + motivo +
                "\n\nVerifique a fonte de energia e o cabo USB, e toque em “Ligar a caixa”.";
            SetStatus("Kinect indisponível — " + motivo);
            return;
        }

        var escolha = MessageBox.Show(
            $"O Kinect não foi encontrado.\n\n{motivo}\n\n" +
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
        SetStatus($"{nome} por {duracao:F0}s sobre {_coberturaAtual}. Observe por onde a água desce.");
    }

    private void ExecutarTerremoto()
    {
        var sismo = _engine.Terremoto;
        if (sismo is null || sismo.EmAndamento) return;

        sismo.Disparar(0.5f, 0.5f, (float)SldMagnitude.Value);
        SetStatus($"Terremoto de magnitude {SldMagnitude.Value:F1} sobre {_coberturaAtual}.");
    }

    /// <summary>
    /// Ateia fogo. O foco cai num ponto sorteado entre os que têm o que queimar — a
    /// própria simulação escolhe, para o incêndio não começar no asfalto e não pegar.
    /// </summary>
    private void ExecutarQueimada()
    {
        var fogo = _engine.Fogo;
        if (fogo is null || fogo.EmAndamento) return;

        fogo.VentoForca = (float)SldVento.Value;

        if (!fogo.Atear())
        {
            // Atear devolve falso quando nenhuma célula tem combustível suficiente. É o
            // caso da cobertura padrão, que é solo arenoso — dizer o que fazer vale mais
            // que um botão que não responde.
            MessageBox.Show(
                "Não há vegetação que possa queimar nesta cobertura.\n\n" +
                "Escolha uma cobertura com material combustível — Mata, Pastagem ou " +
                "Agricultura — e ateie o fogo de novo.",
                AppInfo.Nome, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetStatus($"Fogo ateado sobre {_coberturaAtual}. " +
                  $"Vento de {fogo.VentoPorExtenso()}. Observe por onde a frente avança.");
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
