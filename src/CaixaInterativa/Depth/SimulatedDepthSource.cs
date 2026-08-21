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

namespace CaixaInterativa.Depth;

/// <summary>
/// Gera um relevo sintetico com a mesma geometria do Kinect v1 (640x480, mm).
/// Existe para desenvolver e calibrar o projetor sem hardware ligado, e para
/// reproduzir bugs da pipeline de forma deterministica.
/// </summary>
public sealed class SimulatedDepthSource : IDepthSource
{
    /// <summary>Altura relativa que representa "areia nivelada" na faixa 0..1.</summary>
    private const double NeutralLevel = 0.35;

    private readonly int _baseDistanceMm;
    private readonly int _maxReliefMm;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public string Name => "Simulador (sem hardware)";
    public int Width => 640;
    public int Height => 480;
    public bool IsRunning => _loop is { IsCompleted: false };

    /// <summary>
    /// Amplitude do relevo, 0 a 1. Em 0 a superficie fica plana, que e' o unico jeito de
    /// ensaiar a calibracao do plano-base sem hardware: calibrar com o relevo presente
    /// tornaria as colinas o novo zero e o mapa sairia todo achatado.
    /// </summary>
    public double ReliefScale { get; set; } = 1.0;

    public event Action<RawDepthFrame>? FrameArrived;
    public event Action<string>? Faulted;

    public SimulatedDepthSource(int baseDistanceMm = 900, int maxReliefMm = 200)
    {
        _baseDistanceMm = baseDistanceMm;
        _maxReliefMm = maxReliefMm;
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Run(() => Loop(token), token);
    }

    private void Loop(CancellationToken token)
    {
        var sw = Stopwatch.StartNew();
        var rng = new Random(1234);
        long frame = 0;
        var buffer = new ushort[Width * Height];

        try
        {
            while (!token.IsCancellationRequested)
            {
                double t = sw.Elapsed.TotalSeconds;

                for (int y = 0; y < Height; y++)
                {
                    // Coordenadas normalizadas em torno do centro, com aspecto corrigido.
                    double ny = (y - Height / 2.0) / (Height / 2.0);
                    for (int x = 0; x < Width; x++)
                    {
                        double nx = (x - Width / 2.0) / (Width / 2.0);

                        // Duas colinas que respiram + uma bacia, para exercitar
                        // toda a faixa do mapa de cores e as curvas de nivel.
                        double h = 0.0;
                        h += 0.85 * Gauss(nx - 0.35, ny - 0.20, 0.30 + 0.05 * Math.Sin(t * 0.7));
                        h += 0.60 * Gauss(nx + 0.40, ny + 0.30, 0.24);
                        h -= 0.45 * Gauss(nx + 0.10, ny - 0.45, 0.28);
                        h += 0.10 * Math.Sin(nx * 6.0 + t * 0.4) * Math.Cos(ny * 5.0);

                        h = Math.Clamp(h * 0.5 + NeutralLevel, 0.0, 1.0);

                        // Interpola em direcao ao nivel neutro: ReliefScale 0 = areia plana.
                        double scale = Math.Clamp(ReliefScale, 0.0, 1.0);
                        h = NeutralLevel + (h - NeutralLevel) * scale;

                        int distance = _baseDistanceMm - (int)(h * _maxReliefMm);

                        // Ruido de ~2mm, compativel com o que o Kinect v1 entrega de fato.
                        distance += rng.Next(-2, 3);

                        // ~0.5% de pixels invalidos, para exercitar o preenchimento de buracos.
                        buffer[y * Width + x] = rng.NextDouble() < 0.005 ? (ushort)0 : (ushort)distance;
                    }
                }

                FrameArrived?.Invoke(new RawDepthFrame
                {
                    Data = (ushort[])buffer.Clone(),
                    Width = Width,
                    Height = Height,
                    FrameNumber = frame++
                });

                Thread.Sleep(33); // ~30 fps, igual ao sensor real
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Faulted?.Invoke($"Simulador falhou: {ex.Message}");
        }
    }

    private static double Gauss(double dx, double dy, double sigma)
        => Math.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));

    public void Stop()
    {
        _cts?.Cancel();
        try { _loop?.Wait(1000); } catch { /* encerrando */ }
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    public void Dispose() => Stop();
}
