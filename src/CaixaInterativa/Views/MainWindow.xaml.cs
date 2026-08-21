// Caixa de Areia Interativa — sistema de projeção topográfica interativa
// Copyright (C) 2026 Luis Filipe Gomes de Carvalho
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
        _uiTimer.Tick += (_, _) => AtualizarIndicadores();
        _uiTimer.Start();

        Loaded += OnWindowLoaded;
        Closed += (_, _) => { _engine.Dispose(); _projection?.Close(); };
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        // Identidade vem toda de AppInfo, para que tela e documentação não divirjam.
        Title = AppInfo.TituloDaJanela;
        TxtAssinatura.Text = AppInfo.Assinatura;
        TxtEmailSuporte.Text = AppInfo.EmailSuporte;
        LnkSuporte.NavigateUri = new Uri(AppInfo.LinkDeSuporte);
        LnkPagina.NavigateUri = new Uri(AppInfo.PaginaDoProjeto);
        LnkGithub.NavigateUri = new Uri(AppInfo.Repositorio);

        PopulateScreens();
        LoadSettingsIntoControls();
        ConfigPath.Text = AppConfig.DefaultPath;
        ExpAvancado.IsExpanded = !_config.Interface.SimpleMode;
        _loaded = true;

        DetectSensor();
        AtualizarResumoCalibracao();

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
    }

    private void OnFlatSimChanged(object sender, RoutedEventArgs e)
    {
        if (_simulator is null) return;
        _simulator.ReliefScale = ChkFlatSim.IsChecked == true ? 0.0 : 1.0;
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
