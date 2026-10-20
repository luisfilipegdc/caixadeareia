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
/// Pergunta ao Windows quais partes do Kinect estao conectadas agora.
///
/// <b>Por que existe.</b> Sem a fonte de 12 V, o Kinect v1 ainda aparece pelo USB: o audio
/// e o controle de seguranca sao alimentados pelo cabo, a camera e o motor nao. O driver
/// conta o sensor, o NuiInitialize devolve E_FAIL (0x80004005), e o HRESULT sozinho nao
/// diz qual das causas e'. A lista de dispositivos estreita: camera ausente com audio
/// presente e' fonte ou porta USB, e a luz do sensor decide entre as duas.
///
/// O caso real tinha, presentes, "Security Control", "USB Audio" e "Audio Array Control";
/// "Camera" e "Audio Control" so como instalados, sem estar conectados.
/// </summary>
internal static class DispositivosKinect
{
    /// <summary>
    /// Mensagem para quando so a parte alimentada pelo USB aparece, ou <c>null</c> quando a
    /// lista nao permite afirmar nada — ai vale a mensagem do HRESULT.
    /// </summary>
    public static string? Diagnosticar(IEnumerable<string> presentes)
    {
        bool camera = presentes.Any(n => EhKinect(n) && Contem(n, "Camera"));
        bool audio = presentes.Any(n => EhKinect(n) && Contem(n, "Audio"));

        if (camera || !audio) return null;

        // As duas causas deixam a mesma lista; quem separa e' a luz do sensor. O primeiro
        // texto afirmava "a fonte" e errou na mesa: luz verde fixa, fonte boa, e a camera
        // ausente por causa do controlador USB (AMD) do notebook.
        return "O áudio do Kinect aparece no Windows, mas a câmera não. Olhe a luz do sensor. "
             + "Apagada: a fonte de energia externa não está chegando — confira a tomada e o "
             + "encaixe na emenda do cabo do Kinect. "
             + "Verde: a energia está boa e o problema é a porta USB — troque de porta, "
             + "prefira uma USB 2.0 ou use um hub USB 2.0. Em notebooks com processador AMD, "
             + "o Kinect v1 muitas vezes não funciona em nenhuma porta; teste num computador "
             + "com processador Intel.";
    }

    /// <summary>Le os dispositivos conectados e diagnostica. Nunca lanca.</summary>
    public static string? DiagnosticarAgora()
    {
        try { return Diagnosticar(NomesPresentes()); }
        catch { return null; }
    }

    private static bool EhKinect(string nome) => Contem(nome, "Kinect");

    private static bool Contem(string nome, string trecho)
        => nome.Contains(trecho, StringComparison.OrdinalIgnoreCase);

    // --- SetupAPI ---

    private const uint DIGCF_PRESENT = 0x2;
    private const uint DIGCF_ALLCLASSES = 0x4;
    private const uint SPDRP_DEVICEDESC = 0x0;
    private const uint SPDRP_FRIENDLYNAME = 0xC;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDevinfoData
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(IntPtr classGuid, string? enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SpDevinfoData deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(
        IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData, uint property,
        out uint propertyRegDataType, byte[]? propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    private static List<string> NomesPresentes()
    {
        var nomes = new List<string>();
        IntPtr set = SetupDiGetClassDevsW(IntPtr.Zero, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
        if (set == INVALID_HANDLE_VALUE) return nomes;

        try
        {
            var info = new SpDevinfoData { cbSize = (uint)Marshal.SizeOf<SpDevinfoData>() };
            for (uint i = 0; SetupDiEnumDeviceInfo(set, i, ref info); i++)
            {
                string? nome = Propriedade(set, ref info, SPDRP_FRIENDLYNAME)
                            ?? Propriedade(set, ref info, SPDRP_DEVICEDESC);
                if (nome is not null) nomes.Add(nome);
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
        return nomes;
    }

    private static string? Propriedade(IntPtr set, ref SpDevinfoData info, uint propriedade)
    {
        SetupDiGetDeviceRegistryPropertyW(set, ref info, propriedade, out _, null, 0, out uint tamanho);
        if (tamanho == 0) return null;

        var buffer = new byte[tamanho];
        if (!SetupDiGetDeviceRegistryPropertyW(set, ref info, propriedade, out _, buffer, tamanho, out _))
            return null;

        string texto = System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        return texto.Length > 0 ? texto : null;
    }
}
