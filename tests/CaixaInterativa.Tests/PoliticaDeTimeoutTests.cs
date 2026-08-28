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
/// A lógica de "quando desistir do sensor" e de "o near mode pegou?" vive em classes
/// próprias justamente para poder ser testada sem hardware. O laço de captura fica com
/// três linhas de fiação.
/// </summary>
public class PoliticaDeTimeoutTests
{
    [Fact]
    public void OperacaoNormalNuncaDeclaraFalha()
    {
        var p = new PoliticaDeTimeout(200);

        // Quadro, quadro, quadro: o sensor está entregando.
        for (int i = 0; i < 500; i++)
        {
            p.RegistrarQuadro();
            Assert.Equal(0, p.Consecutivos);
        }
    }

    /// <summary>
    /// Um engasgo isolado de USB não pode derrubar a aula. Alguns timeouts seguidos,
    /// desde que um quadro chegue antes do limite, não geram falha.
    /// </summary>
    [Fact]
    public void TimeoutOcasionalNaoDeclaraFalha()
    {
        var p = new PoliticaDeTimeout(200);

        for (int rodada = 0; rodada < 50; rodada++)
        {
            // Até um a menos que o limite, e então um quadro bom.
            for (int i = 0; i < p.LimiteDeTentativas - 1; i++)
                Assert.False(p.RegistrarTimeout(), $"Falhou cedo demais na rodada {rodada}.");

            p.RegistrarQuadro();
        }
    }

    [Fact]
    public void SilencioProlongadoDeclaraFalhaUmaVezSo()
    {
        var p = new PoliticaDeTimeout(200);

        int disparos = 0;
        for (int i = 0; i < 200; i++)
            if (p.RegistrarTimeout()) disparos++;

        Assert.Equal(1, disparos);
    }

    /// <summary>
    /// O limite é de tempo, não de contagem: a espera por tentativa define quantas cabem.
    /// </summary>
    [Theory]
    [InlineData(200, 15)]
    [InlineData(100, 30)]
    [InlineData(500, 6)]
    [InlineData(1000, 3)]
    public void LimiteDeTentativasDerivaDoTempoDeEspera(int esperaMs, int tentativasEsperadas)
    {
        Assert.Equal(tentativasEsperadas, new PoliticaDeTimeout(esperaMs).LimiteDeTentativas);
    }

    [Fact]
    public void OLimiteEDeTresSegundos()
    {
        var p = new PoliticaDeTimeout(200);
        while (!p.RegistrarTimeout()) { }

        Assert.Equal(PoliticaDeTimeout.LimiteMs, p.SilencioMs);
        Assert.Equal(3000, p.SilencioMs);
    }

    [Fact]
    public void QuadroDepoisDaFalhaRecomecaAContagem()
    {
        var p = new PoliticaDeTimeout(200);
        while (!p.RegistrarTimeout()) { }

        // Reconectou.
        p.RegistrarQuadro();
        Assert.Equal(0, p.Consecutivos);
        Assert.Equal(0, p.SilencioMs);

        // E volta a poder falhar de novo se emudecer outra vez.
        int disparos = 0;
        for (int i = 0; i < 100; i++) if (p.RegistrarTimeout()) disparos++;
        Assert.Equal(1, disparos);
    }

    [Fact]
    public void MensagemDizQuantosSegundosSemImagem()
    {
        var p = new PoliticaDeTimeout(200);
        while (!p.RegistrarTimeout()) { }

        Assert.Contains("3 segundos", p.Mensagem());
    }

    [Fact]
    public void EsperaInvalidaERecusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoliticaDeTimeout(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PoliticaDeTimeout(-1));
    }
}
