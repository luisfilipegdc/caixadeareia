using System.Windows;
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

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _uiTimer.Tick += (_, _) =>
        {
            FpsText.Text = _engine.Fps > 0 ? $"{_engine.Fps:F0} fps  |  fonte: {_engine.SourceName}" : "";
            PreviewHint.Visibility = _engine.Bitmap is null ? Visibility.Visible : Visibility.Collapsed;
        };
        _uiTimer.Start();

        Loaded += OnWindowLoaded;
        Closed += (_, _) => { _engine.Dispose(); _projection?.Close(); };
    }

    private void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        PopulateScreens();
        LoadSettingsIntoControls();
        ConfigPath.Text = AppConfig.DefaultPath;
        _loaded = true;
        DetectSensor();
    }

    // ---------------- Sensor ----------------

    private void OnDetect(object sender, RoutedEventArgs e) => DetectSensor();

    private void DetectSensor()
    {
        bool ok = KinectV1Source.TryProbe(out int count, out string message);
        SensorStatus.Text = message;
        BtnStartKinect.IsEnabled = ok;
        SetStatus(ok ? $"Kinect pronto ({count})." : "Kinect indisponivel - use o simulador.");
    }

    private void OnStartKinect(object sender, RoutedEventArgs e)
    {
        try
        {
            _config.Sensor.Source = "kinect";
            _config.Sensor.NearMode = ChkNearMode.IsChecked == true;

            _engine.StartSource(new KinectV1Source(
                nearMode: _config.Sensor.NearMode,
                tiltAngle: _config.Sensor.TiltAngle ?? int.MinValue));
        }
        catch (Exception ex)
        {
            SensorStatus.Text = ex.Message;
            SetStatus("Falha ao iniciar o Kinect.");
        }
    }

    private void OnStartSimulator(object sender, RoutedEventArgs e)
    {
        _config.Sensor.Source = "simulador";
        _simulator = new SimulatedDepthSource { ReliefScale = ChkFlatSim.IsChecked == true ? 0.0 : 1.0 };
        _engine.StartSource(_simulator);
    }

    private void OnFlatSimChanged(object sender, RoutedEventArgs e)
    {
        if (_simulator is null) return;
        _simulator.ReliefScale = ChkFlatSim.IsChecked == true ? 0.0 : 1.0;
    }

    // ---------------- Calibracao ----------------

    private void OnCalibrate(object sender, RoutedEventArgs e)
    {
        CalibStatus.Text = "Calibrando... nao mexa na areia.";
        _engine.CalibrateBase();
    }

    private void OnResetCalibration(object sender, RoutedEventArgs e)
    {
        _engine.ResetCalibration();
        CalibStatus.Text = "Nao calibrado.";
    }

    private void OnCalibrationCompleted(double averageMm)
    {
        double coverage = _engine.CoveragePercent;
        CalibStatus.Text =
            $"Calibrado. Distancia media sensor-fundo: {averageMm:F0} mm.\n" +
            $"Cobertura: {coverage:F0}% da area do sensor.";

        // Abaixo de 80% sobram buracos permanentes no mapa. Vale avisar na hora da
        // calibracao, e nao deixar o professor descobrir com a turma na frente.
        if (coverage < 80)
        {
            CalibStatus.Text +=
                "\n\nCobertura baixa. Verifique: sensor perpendicular e a 0,9-1,2 m, " +
                "sem sol direto na caixa, areia seca e fosca.";
        }

        SetStatus($"Plano-base capturado ({coverage:F0}% de cobertura).");
    }

    // ---------------- Projecao ----------------

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

        _projection = new ProjectionWindow(_engine);
        _projection.SaveRequested += SaveConfig;
        _projection.CalibrateRequested += () => _engine.CalibrateBase();
        _projection.Closed += (_, _) =>
        {
            _projection = null;
            BtnProject.Content = "Abrir projecao";
        };
        _projection.Show();
        BtnProject.Content = "Fechar projecao";
        SetStatus("Projecao aberta. Use F1 na janela de projecao para ver os atalhos.");
    }

    // ---------------- Ajustes ----------------

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

        UpdateLabels();
    }

    private void UpdateLabels()
    {
        LblMaxHeight.Text = $"{SldMaxHeight.Value:F0}";
        LblMinHeight.Text = $"{SldMinHeight.Value:F0}";
        LblContour.Text = SldContour.Value < 0.5 ? "off" : $"{SldContour.Value:F0}";
        LblBlur.Text = $"{SldBlur.Value:F0}";
        LblAlpha.Text = $"{SldAlpha.Value:F2}";
    }

    private void OnSave(object sender, RoutedEventArgs e) => SaveConfig();

    private void SaveConfig()
    {
        try
        {
            _config.Save();
            SetStatus($"Configuracao salva em {AppConfig.DefaultPath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Falha ao salvar: {ex.Message}");
        }
    }

    private void SetStatus(string message) => StatusText.Text = message;
}
