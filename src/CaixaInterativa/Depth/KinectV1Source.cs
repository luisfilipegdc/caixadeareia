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

using System.Runtime.InteropServices;
using static CaixaInterativa.Depth.NuiNative;

namespace CaixaInterativa.Depth;

/// <summary>
/// Fonte de profundidade do Kinect v1 (Xbox 360 / Kinect for Windows 1517)
/// via a API nativa NUI do SDK 1.8.
/// </summary>
public sealed class KinectV1Source : IDepthSource
{
    private readonly bool _nearMode;
    private readonly int _tiltAngle;

    private IntPtr _streamHandle = IntPtr.Zero;
    private IntPtr _frameEvent = IntPtr.Zero;
    private CancellationTokenSource? _cts;
    private Thread? _thread;
    private bool _nuiInitialized;

    public string Name => _nearMode ? "Kinect v1 (near mode)" : "Kinect v1";
    public int Width => 640;
    public int Height => 480;
    public bool IsRunning => _thread is { IsAlive: true };

    public event Action<RawDepthFrame>? FrameArrived;
    public event Action<string>? Faulted;

    /// <param name="nearMode">
    /// Exige um Kinect for Windows (modelo 1517). Num sensor de Xbox 360 a chamada
    /// falha e nos degradamos para o modo padrao em vez de abortar.
    /// </param>
    /// <param name="tiltAngle">Angulo do motor em graus (-27..27). Use -1 para nao mexer.</param>
    public KinectV1Source(bool nearMode = true, int tiltAngle = int.MinValue)
    {
        _nearMode = nearMode;
        _tiltAngle = tiltAngle;
    }

    public static bool TryProbe(out int sensorCount, out string message)
    {
        sensorCount = 0;
        try
        {
            int hr = NuiGetSensorCount(out sensorCount);
            if (hr != S_OK)
            {
                message = DescribeHResult(hr);
                return false;
            }
            message = sensorCount > 0
                ? $"{sensorCount} sensor(es) Kinect detectado(s)."
                : "Nenhum sensor Kinect detectado pelo driver da Microsoft.";
            return sensorCount > 0;
        }
        catch (DllNotFoundException)
        {
            message = "Kinect10.dll nao encontrada. Instale o Kinect for Windows SDK 1.8.";
            return false;
        }
        catch (Exception ex)
        {
            message = $"Falha ao consultar o sensor: {ex.Message}";
            return false;
        }
    }

    public void Start()
    {
        if (IsRunning) return;

        int hr = NuiInitialize(NUI_INITIALIZE_FLAG_USE_DEPTH);
        if (hr != S_OK) throw new InvalidOperationException(DescribeHResult(hr));
        _nuiInitialized = true;

        _frameEvent = CreateEvent(IntPtr.Zero, true, false, null);
        if (_frameEvent == IntPtr.Zero)
            throw new InvalidOperationException("Nao foi possivel criar o evento de sincronizacao de quadros.");

        // NUI_IMAGE_TYPE_DEPTH entrega milimetros nos 16 bits cheios. A variante
        // DEPTH_AND_PLAYER_INDEX usaria 3 bits para o indice do jogador e custaria
        // resolucao vertical que nos importa mais que o rastreamento de pessoas.
        hr = NuiImageStreamOpen(
            NUI_IMAGE_TYPE_DEPTH,
            NUI_IMAGE_RESOLUTION_640x480,
            0,
            2,
            _frameEvent,
            out _streamHandle);

        if (hr != S_OK)
        {
            Cleanup();
            throw new InvalidOperationException(DescribeHResult(hr));
        }

        if (_nearMode)
        {
            // O HRESULT aqui nao prova nada: a chamada retorna S_OK mesmo quando o near
            // mode nao e' aplicado (sensor de Xbox 360, ou flag errada). A unica
            // verificacao confiavel e' empirica - com near mode ativo aparecem leituras
            // abaixo de 800mm; sem ele, 800mm e' um piso duro.
            NuiImageStreamSetImageFrameFlags(_streamHandle, NUI_IMAGE_STREAM_FLAG_ENABLE_NEAR_MODE);
        }

        if (_tiltAngle != int.MinValue)
        {
            try { NuiCameraElevationSetAngle(Math.Clamp(_tiltAngle, -27, 27)); } catch { /* motor opcional */ }
        }

        _cts = new CancellationTokenSource();
        _thread = new Thread(() => Loop(_cts.Token))
        {
            IsBackground = true,
            Name = "KinectDepthCapture",
            // Acima do normal: perder quadros aqui vira tremor visivel na projecao.
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
    }

    private void Loop(CancellationToken token)
    {
        var managed = new ushort[Width * Height];
        long frameCounter = 0;

        // Um sensor que emudece sem desconectar nunca levantava exceção, então a
        // reconexão automática nunca era acionada e a tela ficava congelada em silêncio.
        // A regra de quando desistir mora numa classe própria, testável; aqui só a
        // contagem. Ver PoliticaDeTimeout.
        const int EsperaMs = 200;
        var timeout = new PoliticaDeTimeout(EsperaMs);

        try
        {
            while (!token.IsCancellationRequested)
            {
                uint wait = WaitForSingleObject(_frameEvent, EsperaMs);
                if (wait == WAIT_TIMEOUT)
                {
                    if (timeout.RegistrarTimeout()) Faulted?.Invoke(timeout.Mensagem());
                    continue;
                }
                if (wait != WAIT_OBJECT_0)
                {
                    Faulted?.Invoke("Falha na espera pelo quadro de profundidade.");
                    return;
                }

                int hr = NuiImageStreamGetNextFrame(_streamHandle, 0, out IntPtr framePtr);
                if (hr != S_OK || framePtr == IntPtr.Zero) continue;

                try
                {
                    var frame = Marshal.PtrToStructure<NuiImageFrame>(framePtr);
                    if (frame.pFrameTexture == IntPtr.Zero) continue;

                    int lockHr = TextureLockRect(frame.pFrameTexture, out var locked);
                    if (lockHr != S_OK || locked.pBits == IntPtr.Zero || locked.size <= 0) continue;

                    try
                    {
                        int pixels = Math.Min(managed.Length, locked.size / sizeof(ushort));
                        CopyDepth(locked.pBits, managed, pixels);
                    }
                    finally
                    {
                        TextureUnlockRect(frame.pFrameTexture);
                    }

                    // Chegou quadro: o sensor está vivo e a contagem de silêncio zera.
                    timeout.RegistrarQuadro();

                    FrameArrived?.Invoke(new RawDepthFrame
                    {
                        Data = (ushort[])managed.Clone(),
                        Width = Width,
                        Height = Height,
                        FrameNumber = frameCounter++
                    });
                }
                finally
                {
                    NuiImageStreamReleaseFrame(_streamHandle, framePtr);
                }
            }
        }
        catch (Exception ex)
        {
            Faulted?.Invoke($"Captura do Kinect interrompida: {ex.Message}");
        }
    }

    /// <summary>
    /// Copia desempacotando os 3 bits de indice de jogador: a profundidade em mm mora nos
    /// bits 15..3. Pixels saturados (o sensor nao conseguiu medir) viram 0, que e' a
    /// convencao de "sem leitura" usada pelo resto da pipeline.
    /// </summary>
    private static unsafe void CopyDepth(IntPtr source, ushort[] destination, int count)
    {
        var src = (ushort*)source;
        fixed (ushort* dst = destination)
        {
            for (int i = 0; i < count; i++)
            {
                ushort raw = (ushort)(src[i] >> NUI_IMAGE_PLAYER_INDEX_SHIFT);
                dst[i] = raw >= NUI_DEPTH_SATURATED ? (ushort)0 : raw;
            }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        if (_thread is { IsAlive: true }) _thread.Join(1500);
        _thread = null;
        _cts?.Dispose();
        _cts = null;
        Cleanup();
    }

    private void Cleanup()
    {
        if (_nuiInitialized)
        {
            try { NuiShutdown(); } catch { /* encerrando */ }
            _nuiInitialized = false;
        }
        if (_frameEvent != IntPtr.Zero)
        {
            CloseHandle(_frameEvent);
            _frameEvent = IntPtr.Zero;
        }
        _streamHandle = IntPtr.Zero;
    }

    public void Dispose() => Stop();
}
