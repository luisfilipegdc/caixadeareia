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

namespace CaixaInterativa.Depth;

/// <summary>
/// P/Invoke para a API nativa NUI do Kinect for Windows SDK 1.8 (Kinect10.dll).
///
/// Usamos a API nativa em vez do wrapper gerenciado Microsoft.Kinect.dll porque aquele
/// assembly tem como alvo o .NET Framework 4.0 e carrega-lo no .NET 8 e' fonte de
/// problemas. Como so precisamos do stream de profundidade, o contrato nativo e'
/// pequeno o suficiente para valer a troca.
///
/// Kinect10.dll e' instalado em System32 pelo SDK 1.8 e so existe para x64/x86 nativo,
/// por isso o projeto fixa PlatformTarget=x64.
/// </summary>
internal static class NuiNative
{
    private const string Dll = "Kinect10.dll";

    // --- Flags de inicializacao ---
    public const uint NUI_INITIALIZE_FLAG_USE_DEPTH = 0x00000020;

    // --- NUI_IMAGE_TYPE ---
    public const int NUI_IMAGE_TYPE_DEPTH_AND_PLAYER_INDEX = 0;
    public const int NUI_IMAGE_TYPE_DEPTH = 4;

    // --- NUI_IMAGE_RESOLUTION ---
    public const int NUI_IMAGE_RESOLUTION_320x240 = 1;
    public const int NUI_IMAGE_RESOLUTION_640x480 = 2;

    /// <summary>
    /// Bits reservados ao indice de jogador na base do pixel de profundidade.
    /// Mesmo com NUI_IMAGE_TYPE_DEPTH (sem player index), o SDK 1.8 entrega o valor
    /// deslocado: a profundidade em mm esta nos bits 15..3. Verificado no sensor -
    /// todos os valores lidos eram multiplos de 8 e o maximo era exatamente 0x1FFF&lt;&lt;3.
    /// </summary>
    public const int NUI_IMAGE_PLAYER_INDEX_SHIFT = 3;

    /// <summary>Valor saturado de 13 bits: o sensor nao conseguiu medir aquele ponto.</summary>
    public const ushort NUI_DEPTH_SATURATED = 0x1FFF;

    // --- Flags de stream (NuiImageCamera.h) ---
    public const uint NUI_IMAGE_STREAM_FLAG_SUPPRESS_NO_FRAME_DATA = 0x00010000;

    /// <summary>
    /// Near mode: 0,4m-3,0m em vez de 0,8m-4,0m. So funciona no Kinect for Windows
    /// (modelo 1517), nao no sensor de Xbox 360. Para uma caixa de areia com o sensor
    /// a ~1m e' a diferenca entre leitura limpa e leitura cortada.
    ///
    /// Cuidado com o valor: 0x00040000 e' TOO_FAR_IS_NONZERO, nao near mode. Trocar os
    /// dois nao gera erro - SetImageFrameFlags retorna S_OK de qualquer jeito - e o
    /// sintoma e' silencioso: o alcance minimo continua em 800mm e tudo mais perto que
    /// isso le zero, como se a superficie nao devolvesse infravermelho.
    /// </summary>
    public const uint NUI_IMAGE_STREAM_FLAG_ENABLE_NEAR_MODE = 0x00020000;

    /// <summary>Pontos alem do alcance retornam saturado em vez de zero.</summary>
    public const uint NUI_IMAGE_STREAM_FLAG_TOO_FAR_IS_NONZERO = 0x00040000;

    public const uint NUI_IMAGE_STREAM_FLAG_DISTINCT_OVERFLOW_DEPTH_VALUES = 0x00080000;

    public const int E_NUI_DEVICE_NOT_CONNECTED = unchecked((int)0x83010001);
    public const int E_NUI_DEVICE_NOT_READY     = unchecked((int)0x83010002);
    public const int E_NUI_NOTGENUINE           = unchecked((int)0x83010004);
    public const int E_NUI_INSUFFICIENTBANDWIDTH= unchecked((int)0x83010007);
    public const int E_NUI_NOTPOWERED           = unchecked((int)0x83010006);
    public const int S_OK = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct NuiImageViewArea
    {
        public int eDigitalZoom;
        public int lCenterX;
        public int lCenterY;
    }

    /// <summary>Espelha NUI_IMAGE_FRAME de NuiImageCamera.h. A ordem dos campos importa.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NuiImageFrame
    {
        public long liTimeStamp;
        public uint dwFrameNumber;
        public int eImageType;
        public int eResolution;
        public IntPtr pFrameTexture;   // INuiFrameTexture*
        public uint dwFrameFlags;
        public NuiImageViewArea ViewArea;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NuiLockedRect
    {
        public int Pitch;
        public int size;
        public IntPtr pBits;
    }

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int NuiGetSensorCount(out int pCount);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int NuiInitialize(uint dwFlags);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern void NuiShutdown();

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int NuiImageStreamOpen(
        int eImageType,
        int eResolution,
        uint dwImageFrameFlags,
        uint dwFrameLimit,
        IntPtr hNextFrameEvent,
        out IntPtr phStreamHandle);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int NuiImageStreamSetImageFrameFlags(IntPtr hStream, uint dwImageFrameFlags);

    /// <summary>
    /// Atencao: a API flat devolve um PONTEIRO para um NUI_IMAGE_FRAME de propriedade do
    /// runtime (<c>CONST NUI_IMAGE_FRAME **ppcImageFrame</c>), diferente do metodo homonimo
    /// da interface INuiSensor, que preenche a struct por valor. Declarar
    /// <c>out NuiImageFrame</c> aqui faz o runtime escrever so os 8 bytes do ponteiro nos
    /// primeiros bytes da struct; o resto fica com lixo e o pFrameTexture aparente vira
    /// endereco invalido - o que corrompe a heap na primeira leitura.
    /// </summary>
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int NuiImageStreamGetNextFrame(
        IntPtr hStream,
        uint dwMillisecondsToWait,
        out IntPtr ppcImageFrame);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int NuiImageStreamReleaseFrame(IntPtr hStream, IntPtr pImageFrame);

    /// <summary>Inclina o sensor. Angulo em graus, -27 a +27. Uso com moderacao: o motor
    /// e' fragil e a Microsoft recomenda no maximo 1 movimento por segundo.</summary>
    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int NuiCameraElevationSetAngle(long lAngleDegrees);

    [DllImport(Dll, CallingConvention = CallingConvention.StdCall)]
    public static extern int NuiCameraElevationGetAngle(out long plAngleDegrees);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    public const uint WAIT_OBJECT_0 = 0x00000000;
    public const uint WAIT_TIMEOUT = 0x00000102;

    // --- INuiFrameTexture ---
    // vtable: 0=QueryInterface 1=AddRef 2=Release 3=BufferLen 4=Pitch
    //         5=LockRect 6=GetLevelDesc 7=UnlockRect
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int LockRectDelegate(IntPtr self, uint level, out NuiLockedRect rect, IntPtr pRect, uint flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int UnlockRectDelegate(IntPtr self, uint level);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseDelegate(IntPtr self);

    private static T VTableCall<T>(IntPtr pInterface, int slot) where T : Delegate
    {
        IntPtr vtable = Marshal.ReadIntPtr(pInterface);
        IntPtr fn = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(fn);
    }

    public static int TextureLockRect(IntPtr texture, out NuiLockedRect rect)
        => VTableCall<LockRectDelegate>(texture, 5)(texture, 0, out rect, IntPtr.Zero, 0);

    public static int TextureUnlockRect(IntPtr texture)
        => VTableCall<UnlockRectDelegate>(texture, 7)(texture, 0);

    public static uint TextureRelease(IntPtr texture)
        => VTableCall<ReleaseDelegate>(texture, 2)(texture);

    public static string DescribeHResult(int hr) => hr switch
    {
        E_NUI_DEVICE_NOT_CONNECTED  => "Sensor nao encontrado. Verifique o cabo USB e a fonte de energia.",
        E_NUI_DEVICE_NOT_READY      => "Sensor encontrado mas nao pronto. Aguarde a inicializacao ou reconecte.",
        E_NUI_NOTPOWERED            => "Sensor sem alimentacao. O adaptador de energia externo e' obrigatorio.",
        E_NUI_INSUFFICIENTBANDWIDTH => "Banda USB insuficiente. Use uma porta USB em outro controlador (nao compartilhada).",
        E_NUI_NOTGENUINE            => "Sensor nao reconhecido como original.",
        _                           => $"Erro nativo NUI 0x{hr:X8}."
    };
}
