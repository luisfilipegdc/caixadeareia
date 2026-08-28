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

using CaixaInterativa.Depth;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// Os códigos de erro do NUI, travados contra o header do SDK.
///
/// <b>Por que este arquivo existe.</b> A tabela anterior supunha que os códigos eram
/// sequenciais a partir de <c>0x83010001</c>. Não são: quatro das cinco entradas apontavam
/// para o erro errado, e ninguém percebeu enquanto o sensor não ligava. Na primeira vez que
/// ligou, <c>0x83010009</c> — "sensor já em uso por outro processo" — caiu no ramo genérico
/// e a tela mandou conferir cabo e fonte de um Kinect que estava perfeito.
///
/// Os valores vêm de <c>Microsoft SDKs\Kinect\v1.8\inc\NuiApi.h</c>, onde
/// <c>FACILITY_NUI</c> é <c>0x301</c> e o padrão é <c>MAKE_HRESULT(SEVERITY_ERROR,
/// FACILITY_NUI, n)</c> — e não de memória.
/// </summary>
public class NuiErrosTests
{
    [Theory]
    // FACILITY_NUI: MAKE_HRESULT(1, 0x301, n) == 0x8301_000n
    [InlineData(1, NuiNative.E_NUI_FRAME_NO_DATA)]
    [InlineData(2, NuiNative.E_NUI_STREAM_NOT_ENABLED)]
    [InlineData(3, NuiNative.E_NUI_IMAGE_STREAM_IN_USE)]
    [InlineData(4, NuiNative.E_NUI_FRAME_LIMIT_EXCEEDED)]
    [InlineData(5, NuiNative.E_NUI_FEATURE_NOT_INITIALIZED)]
    [InlineData(6, NuiNative.E_NUI_NOTGENUINE)]
    [InlineData(7, NuiNative.E_NUI_INSUFFICIENTBANDWIDTH)]
    [InlineData(8, NuiNative.E_NUI_NOTSUPPORTED)]
    [InlineData(9, NuiNative.E_NUI_DEVICE_IN_USE)]
    [InlineData(15, NuiNative.E_NUI_HARDWARE_FEATURE_UNAVAILABLE)]
    [InlineData(20, NuiNative.E_NUI_NOTCONNECTED)]
    [InlineData(21, NuiNative.E_NUI_NOTREADY)]
    [InlineData(639, NuiNative.E_NUI_NOTPOWERED)]
    [InlineData(1413, NuiNative.E_NUI_BADINDEX)]
    public void CodigoDeFacilityNuiSegueOHeader(int codigo, int esperado)
    {
        const int SeveridadeErro = unchecked((int)0x80000000);
        const int FacilityNui = 0x301 << 16;

        Assert.Equal(esperado, SeveridadeErro | FacilityNui | codigo);
    }

    [Theory]
    // Estes quatro são __HRESULT_FROM_WIN32, e por isso caem em FACILITY_WIN32 (0x8007).
    // É exatamente a distinção que a tabela antiga não fazia.
    [InlineData(1167, NuiNative.E_NUI_DEVICE_NOT_CONNECTED)]
    [InlineData(21, NuiNative.E_NUI_DEVICE_NOT_READY)]
    [InlineData(1247, NuiNative.E_NUI_ALREADY_INITIALIZED)]
    [InlineData(259, NuiNative.E_NUI_NO_MORE_ITEMS)]
    public void CodigoDerivadoDoWin32SegueOHeader(int win32, int esperado)
    {
        const int HresultDeWin32 = unchecked((int)0x80070000);

        Assert.Equal(esperado, HresultDeWin32 | win32);
    }

    /// <summary>
    /// Os dois grupos não podem colidir. Se colidissem, um <c>switch</c> sobre eles não
    /// compilaria — mas a versão antiga não tinha os dois grupos para colidir.
    /// </summary>
    [Fact]
    public void NenhumCodigoSeRepete()
    {
        int[] todos =
        [
            NuiNative.E_NUI_FRAME_NO_DATA, NuiNative.E_NUI_STREAM_NOT_ENABLED,
            NuiNative.E_NUI_IMAGE_STREAM_IN_USE, NuiNative.E_NUI_FRAME_LIMIT_EXCEEDED,
            NuiNative.E_NUI_FEATURE_NOT_INITIALIZED, NuiNative.E_NUI_NOTGENUINE,
            NuiNative.E_NUI_INSUFFICIENTBANDWIDTH, NuiNative.E_NUI_NOTSUPPORTED,
            NuiNative.E_NUI_DEVICE_IN_USE, NuiNative.E_NUI_HARDWARE_FEATURE_UNAVAILABLE,
            NuiNative.E_NUI_NOTCONNECTED, NuiNative.E_NUI_NOTREADY,
            NuiNative.E_NUI_NOTPOWERED, NuiNative.E_NUI_BADINDEX,
            NuiNative.E_NUI_DEVICE_NOT_CONNECTED, NuiNative.E_NUI_DEVICE_NOT_READY,
            NuiNative.E_NUI_ALREADY_INITIALIZED, NuiNative.E_NUI_NO_MORE_ITEMS,
        ];

        Assert.Equal(todos.Length, todos.Distinct().Count());
    }

    // ───────────────────── as mensagens ─────────────────────

    /// <summary>
    /// <b>O caso que aconteceu na mesa.</b> Duas cópias do aplicativo abertas, a segunda
    /// recebe 0x83010009, e a tela mandava conferir cabo e fonte. Numa sala de aula alguém
    /// teria desmontado um sensor que funcionava.
    /// </summary>
    [Fact]
    public void SensorEmUsoNaoMandaConferirOCabo()
    {
        string m = NuiNative.DescribeHResult(NuiNative.E_NUI_DEVICE_IN_USE);

        Assert.Contains("outro programa", m);
        Assert.DoesNotContain("cabo USB", m);
        Assert.DoesNotContain("fonte de energia", m);
    }

    /// <summary>Nenhum código conhecido pode cair no ramo genérico.</summary>
    [Theory]
    [InlineData(NuiNative.E_NUI_DEVICE_IN_USE)]
    [InlineData(NuiNative.E_NUI_IMAGE_STREAM_IN_USE)]
    [InlineData(NuiNative.E_NUI_ALREADY_INITIALIZED)]
    [InlineData(NuiNative.E_NUI_DEVICE_NOT_CONNECTED)]
    [InlineData(NuiNative.E_NUI_NOTCONNECTED)]
    [InlineData(NuiNative.E_NUI_DEVICE_NOT_READY)]
    [InlineData(NuiNative.E_NUI_NOTREADY)]
    [InlineData(NuiNative.E_NUI_NOTPOWERED)]
    [InlineData(NuiNative.E_NUI_INSUFFICIENTBANDWIDTH)]
    [InlineData(NuiNative.E_NUI_NOTGENUINE)]
    [InlineData(NuiNative.E_NUI_NOTSUPPORTED)]
    [InlineData(NuiNative.E_NUI_HARDWARE_FEATURE_UNAVAILABLE)]
    [InlineData(NuiNative.E_NUI_FEATURE_NOT_INITIALIZED)]
    [InlineData(NuiNative.E_NUI_STREAM_NOT_ENABLED)]
    [InlineData(NuiNative.E_NUI_BADINDEX)]
    public void CodigoConhecidoTemMensagemPropria(int hr)
    {
        Assert.DoesNotContain("Erro nativo NUI", NuiNative.DescribeHResult(hr));
    }

    [Fact]
    public void CodigoDesconhecidoMostraOHexadecimal()
    {
        Assert.Contains("0x87654321", NuiNative.DescribeHResult(unchecked((int)0x87654321)));
    }

    /// <summary>
    /// Near mode não existe no Kinect do Xbox (1414/1473), só no Kinect for Windows (1517).
    /// A mensagem precisa dizer o modelo — sem isso, quem lê não sabe o que fazer.
    /// </summary>
    [Fact]
    public void RecursoIndisponivelNomeiaOModeloQueOTem()
    {
        string m = NuiNative.DescribeHResult(NuiNative.E_NUI_HARDWARE_FEATURE_UNAVAILABLE);

        Assert.Contains("1517", m);
        Assert.Contains("near mode", m, StringComparison.OrdinalIgnoreCase);
    }
}
