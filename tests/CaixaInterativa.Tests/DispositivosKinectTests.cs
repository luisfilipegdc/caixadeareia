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
/// O diagnóstico de 0x80004005 pela lista de dispositivos do Windows. As listas abaixo são
/// as que o <c>Get-PnpDevice -PresentOnly</c> devolveu na mesa, não exemplos inventados.
/// </summary>
public class DispositivosKinectTests
{
    /// <summary>
    /// <b>O caso que aconteceu.</b> Só a parte alimentada pelo USB aparece. A primeira versão
    /// afirmava "é a fonte" — e na mesa a luz estava verde fixa: era o controlador USB AMD
    /// do notebook. A mensagem tem de cobrir as duas causas e dizer como separá-las.
    /// </summary>
    [Fact]
    public void SoAudioPresenteSeparaFonteDePortaPelaLuz()
    {
        string[] presentes =
        [
            "Kinect for Windows Security Control",
            "Kinect for Windows USB Audio",
            "Grupo de microfones (Kinect for Windows USB Audio)",
            "Kinect for Windows Audio Array Control",
        ];

        string? m = DispositivosKinect.Diagnosticar(presentes);

        Assert.NotNull(m);
        Assert.Contains("luz", m);
        Assert.Contains("fonte", m);
        Assert.Contains("porta USB", m);
        Assert.DoesNotContain("SDK", m);
    }

    /// <summary>Com a câmera presente, a lista não explica a falha; vale a mensagem do HRESULT.</summary>
    [Fact]
    public void CameraPresenteNaoDiagnostica()
    {
        string[] presentes =
        [
            "Kinect for Windows Camera",
            "Kinect for Windows Device",
            "Kinect for Windows USB Audio",
        ];

        Assert.Null(DispositivosKinect.Diagnosticar(presentes));
    }

    /// <summary>Nada do Kinect na lista: não dá para afirmar que é a fonte.</summary>
    [Fact]
    public void SemNadaDoKinectNaoDiagnostica()
    {
        Assert.Null(DispositivosKinect.Diagnosticar(["Teclado HID", "Câmera integrada"]));
    }
}
