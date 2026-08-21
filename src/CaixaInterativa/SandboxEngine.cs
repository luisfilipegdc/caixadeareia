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

using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CaixaInterativa.Config;
using CaixaInterativa.Depth;
using CaixaInterativa.Processing;
using CaixaInterativa.Rendering;

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

    public void StartSource(IDepthSource source)
    {
        StopSource();

        _source = source;
        _processor = new DepthProcessor(source.Width, source.Height)
        {
            Settings = Config.Processing
        };
        _processor.CalibrationCompleted += d =>
        {
            Application.Current?.Dispatcher.Invoke(() => CalibrationCompleted?.Invoke(d));
        };

        _heights = new float[source.Width * source.Height];
        _latestFrame = null;
        _lastRenderedFrameNumber = -1;

        source.FrameArrived += OnFrameArrived;
        source.Faulted += OnFaulted;

        source.Start();
        _timer.Start();
        StatusChanged?.Invoke($"Fonte iniciada: {source.Name}");
    }

    public void StopSource()
    {
        _timer.Stop();
        if (_source is null) return;

        _source.FrameArrived -= OnFrameArrived;
        _source.Faulted -= OnFaulted;
        _source.Dispose();
        _source = null;
    }

    private void OnFrameArrived(RawDepthFrame frame) => Volatile.Write(ref _latestFrame, frame);

    private void OnFaulted(string message)
        => Application.Current?.Dispatcher.Invoke(() => StatusChanged?.Invoke(message));

    private void OnTick(object? sender, EventArgs e)
    {
        var frame = Volatile.Read(ref _latestFrame);
        if (frame is null || _processor is null) return;
        if (frame.FrameNumber == _lastRenderedFrameNumber) return;
        _lastRenderedFrameNumber = frame.FrameNumber;

        _processor.Settings = Config.Processing;
        _processor.ProcessFrame(frame, _heights);

        var pixels = _renderer.Render(
            _heights, frame.Width, frame.Height,
            Config.Projection, Config.Processing, Config.Render);

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
        StatusChanged?.Invoke($"Calibrando plano-base ({frames} quadros)... nao mexa na areia.");
    }

    public void ResetCalibration()
    {
        _processor?.ResetCalibration();
        StatusChanged?.Invoke("Calibracao descartada.");
    }

    public void Dispose()
    {
        StopSource();
        _timer.Tick -= OnTick;
    }
}
