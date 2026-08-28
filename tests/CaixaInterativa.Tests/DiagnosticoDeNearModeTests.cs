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

using CaixaInterativa.Processing;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// O diagnóstico de near mode é **indício**, não prova. Estes testes cobrem tanto o caso
/// em que ele deve avisar quanto os vários em que precisa ficar calado — porque um aviso
/// falso sobre hardware manda o professor procurar problema onde não há.
/// </summary>
public class DiagnosticoDeNearModeTests
{
    private const int N = 640 * 480;

    private static ushort[] Quadro(ushort valor)
    {
        var d = new ushort[N];
        Array.Fill(d, valor);
        return d;
    }

    private static DiagnosticoDeNearMode Observando(ushort[] quadro, int vezes = 20)
    {
        var d = new DiagnosticoDeNearMode();
        for (int i = 0; i < vezes; i++) d.Observar(quadro, 400, 2000);
        return d;
    }

    [Fact]
    public void LeiturasAbaixoDe800NaoGeramAviso()
    {
        var d = Observando(Quadro(600));

        Assert.True(d.Concluido);
        Assert.Equal(600, d.MinimaObservadaMm);
        Assert.Null(d.Aviso(nearModeSolicitado: true));
    }

    [Fact]
    public void MinimoPresoAcimaDe800ComNearModePedidoGeraAviso()
    {
        var d = Observando(Quadro(900));

        Assert.Equal(900, d.MinimaObservadaMm);
        string? aviso = d.Aviso(nearModeSolicitado: true);

        Assert.NotNull(aviso);
        Assert.Contains("900", aviso);
        // Precisa admitir a outra explicação possível, senão vira acusação.
        Assert.Contains("areia", aviso);
    }

    [Fact]
    public void SemNearModePedidoNuncaAvisa()
    {
        Assert.Null(Observando(Quadro(900)).Aviso(nearModeSolicitado: false));
    }

    [Fact]
    public void NaoOpinaAntesDeObservarOSuficiente()
    {
        var d = new DiagnosticoDeNearMode();
        d.Observar(Quadro(900), 400, 2000);

        Assert.False(d.Concluido);
        Assert.Null(d.Aviso(nearModeSolicitado: true));
    }

    /// <summary>
    /// Sensor tampado, caixa vazia, tudo fora de alcance: quase nenhuma leitura válida.
    /// A mínima observada não diz nada, e opinar seria ruído.
    /// </summary>
    [Fact]
    public void CoberturaBaixaDemaisNaoGeraAviso()
    {
        var d = new DiagnosticoDeNearMode();
        var quadro = Quadro(0);                    // tudo inválido
        quadro[0] = 1500;                          // uma leitura solitária
        for (int i = 0; i < 20; i++) d.Observar(quadro, 400, 2000);

        Assert.True(d.Concluido);
        Assert.Null(d.Aviso(nearModeSolicitado: true));
    }

    [Fact]
    public void GuardaAMenorLeituraAoLongoDosQuadros()
    {
        var d = new DiagnosticoDeNearMode();
        d.Observar(Quadro(1200), 400, 2000);
        d.Observar(Quadro(700), 400, 2000);
        d.Observar(Quadro(1100), 400, 2000);

        Assert.Equal(700, d.MinimaObservadaMm);
    }

    [Fact]
    public void LeiturasForaDaFaixaSaoIgnoradas()
    {
        var d = new DiagnosticoDeNearMode();
        var quadro = Quadro(900);
        for (int i = 0; i < quadro.Length; i += 7) quadro[i] = i % 14 == 0 ? (ushort)100 : (ushort)900;

        for (int i = 0; i < 20; i++) d.Observar(quadro, 400, 2000);

        // 100 mm está abaixo do mínimo válido: não pode virar a mínima observada.
        Assert.Equal(900, d.MinimaObservadaMm);
    }

    [Fact]
    public void ReiniciarLimpaAObservacao()
    {
        var d = Observando(Quadro(900));
        Assert.True(d.Concluido);

        d.Reiniciar();

        Assert.False(d.Concluido);
        Assert.Equal(0, d.MinimaObservadaMm);
        Assert.Null(d.Aviso(nearModeSolicitado: true));
    }
}
