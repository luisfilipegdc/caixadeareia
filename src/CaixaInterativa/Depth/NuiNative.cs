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

    // Codigos de erro do NUI, conferidos linha a linha contra
    // "C:\Program Files\Microsoft SDKs\Kinect\v1.8\inc\NuiApi.h".
    //
    // A tabela anterior estava errada em quatro das cinco entradas: chutava que os codigos
    // eram sequenciais a partir de 0x83010001, e nao sao. O preco apareceu na primeira vez
    // que o sensor foi ligado de verdade — 0x83010009 caiu no ramo generico e a tela mandou
    // conferir cabo e fonte de um Kinect que estava perfeito. Numa sala de aula alguem teria
    // desconectado um sensor que funcionava.
    //
    // Duas familias, e e' dai que vem a confusao:
    //
    //   FACILITY_NUI (0x8301xxxx)  — erros proprios do NUI, MAKE_HRESULT(ERRO, 0x301, n)
    //   FACILITY_WIN32 (0x8007xxxx) — quatro deles sao __HRESULT_FROM_WIN32 de erros do Windows
    //
    // Os valores win32 foram confirmados em execucao com Win32Exception: 21 "dispositivo nao
    // esta pronto", 259 "nao ha mais dados", 1167 "dispositivo nao esta conectado",
    // 1247 "inicializacao ja havia sido concluida".

    // --- FACILITY_NUI ---
    public const int E_NUI_FRAME_NO_DATA         = unchecked((int)0x83010001);
    public const int E_NUI_STREAM_NOT_ENABLED    = unchecked((int)0x83010002);
    public const int E_NUI_IMAGE_STREAM_IN_USE   = unchecked((int)0x83010003);
    public const int E_NUI_FRAME_LIMIT_EXCEEDED  = unchecked((int)0x83010004);
    public const int E_NUI_FEATURE_NOT_INITIALIZED = unchecked((int)0x83010005);
    public const int E_NUI_NOTGENUINE            = unchecked((int)0x83010006);
    public const int E_NUI_INSUFFICIENTBANDWIDTH = unchecked((int)0x83010007);
    public const int E_NUI_NOTSUPPORTED          = unchecked((int)0x83010008);

    /// <summary>Outro processo ja abriu este sensor. Nao e' defeito de hardware.</summary>
    public const int E_NUI_DEVICE_IN_USE         = unchecked((int)0x83010009);

    public const int E_NUI_HARDWARE_FEATURE_UNAVAILABLE = unchecked((int)0x8301000F);
    public const int E_NUI_NOTCONNECTED          = unchecked((int)0x83010014);
    public const int E_NUI_NOTREADY              = unchecked((int)0x83010015);

    /// <summary>Hub e motor conectados, camera nao — tipicamente a fonte externa.</summary>
    public const int E_NUI_NOTPOWERED            = unchecked((int)0x8301027F);

    public const int E_NUI_BADINDEX              = unchecked((int)0x83010585);

    // --- FACILITY_WIN32 ---
    public const int E_NUI_DEVICE_NOT_CONNECTED  = unchecked((int)0x8007048F);
    public const int E_NUI_DEVICE_NOT_READY      = unchecked((int)0x80070015);
    public const int E_NUI_ALREADY_INITIALIZED   = unchecked((int)0x800704DF);
    public const int E_NUI_NO_MORE_ITEMS         = unchecked((int)0x80070103);

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

    /// <summary>
    /// Traduz o HRESULT para uma instrucao que da' para seguir com a caixa na frente.
    ///
    /// A regra de escrita: dizer o que fazer, e nunca mandar mexer no hardware quando o
    /// problema nao esta' no hardware. Um "verifique o cabo" errado custa mais caro que um
    /// codigo hexadecimal — o codigo faz procurar, o conselho errado faz desmontar.
    /// </summary>
    public static string DescribeHResult(int hr) => hr switch
    {
        // O caso que aparece na pratica: duas copias do aplicativo abertas ao mesmo tempo.
        E_NUI_DEVICE_IN_USE         => "O sensor ja esta sendo usado por outro programa. "
                                     + "Feche a outra janela da Caixa (ou o Kinect Studio) e tente de novo. "
                                     + "O cabo e a fonte estao bem.",
        E_NUI_IMAGE_STREAM_IN_USE   => "Este fluxo de imagem ja esta aberto. "
                                     + "Feche a outra janela da Caixa e tente de novo.",
        E_NUI_ALREADY_INITIALIZED   => "O sensor ja foi inicializado nesta sessao. "
                                     + "Toque em Parar e depois em Ligar a caixa.",

        E_NUI_DEVICE_NOT_CONNECTED  => "Sensor nao encontrado. Verifique o cabo USB e a fonte de energia.",
        E_NUI_NOTCONNECTED          => "O hub do Kinect se desconectou. Verifique o cabo USB.",
        E_NUI_DEVICE_NOT_READY or E_NUI_NOTREADY
                                    => "Sensor encontrado mas nao pronto. Aguarde a inicializacao ou reconecte.",
        E_NUI_NOTPOWERED            => "Sensor sem alimentacao. O adaptador de energia externo e' obrigatorio.",
        E_NUI_INSUFFICIENTBANDWIDTH => "Banda USB insuficiente. Use uma porta USB em outro controlador (nao compartilhada).",
        E_NUI_NOTGENUINE            => "Sensor nao reconhecido como original.",
        E_NUI_NOTSUPPORTED          => "Este modelo de sensor nao e' suportado pelo driver instalado.",

        // Near mode so' existe no Kinect for Windows (1517). No 1414/1473 do Xbox, nao.
        E_NUI_HARDWARE_FEATURE_UNAVAILABLE
                                    => "Este sensor nao tem o recurso pedido. "
                                     + "O near mode exige o Kinect for Windows (modelo 1517).",
        E_NUI_FEATURE_NOT_INITIALIZED
                                    => "Recurso pedido antes de o sensor estar inicializado. "
                                     + "Toque em Parar e depois em Ligar a caixa.",
        E_NUI_STREAM_NOT_ENABLED    => "O fluxo de profundidade nao foi habilitado na inicializacao.",
        E_NUI_BADINDEX              => "Indice de sensor invalido. Toque em Procurar Kinect.",

        _                           => $"Erro nativo NUI 0x{hr:X8}."
    };
}
