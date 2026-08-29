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

using CaixaInterativa.Rendering;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// A conversão de um clique na prévia para um ponto do campo.
///
/// <b>O defeito que estes testes fecham.</b> A projeção é espelhada para casar com a
/// caixa física — no Kinect v1 isso é a regra, porque o sensor entrega a profundidade
/// espelhada. A prévia não espelhava, e o clique era lido direto nas coordenadas dela:
/// clicar no morro da direita da tela acendia o morro da esquerda da areia.
///
/// Agora a prévia espelha junto, e a conversão desfaz o espelho para chegar ao campo.
/// </summary>
public class PontoNaPreviaTests
{
    // Um caso simples: controle do mesmo tamanho da imagem, sem tarjas, sem ROI.
    private const int W = 640, H = 480;

    private static PontoDoCampo Converter(
        double x, double y,
        bool flipH = false, bool flipV = false,
        double larguraControle = W, double alturaControle = H,
        int roiEsquerda = 0, int roiTopo = 0,
        int larguraImagem = W, int alturaImagem = H,
        int larguraCampo = W, int alturaCampo = H)
    {
        Assert.True(PontoNaPrevia.TentarConverter(
            x, y, larguraControle, alturaControle,
            larguraImagem, alturaImagem,
            flipH, flipV, roiEsquerda, roiTopo,
            larguraCampo, alturaCampo, out var ponto));
        return ponto;
    }

    private static bool Recusa(
        double x, double y,
        double larguraControle = W, double alturaControle = H,
        int larguraImagem = W, int alturaImagem = H,
        int larguraCampo = W, int alturaCampo = H)
        => !PontoNaPrevia.TentarConverter(
            x, y, larguraControle, alturaControle,
            larguraImagem, alturaImagem,
            false, false, 0, 0, larguraCampo, alturaCampo, out _);

    // ───────────────────────── sem espelho ─────────────────────────

    [Fact]
    public void SemEspelhoOCantoEsquerdoVaiParaAEsquerda()
    {
        var p = Converter(0, 0);

        Assert.Equal(0f, p.U, precision: 4);
        Assert.Equal(0f, p.V, precision: 4);
    }

    [Fact]
    public void SemEspelhoOCentroVaiParaOCentro()
    {
        var p = Converter(W / 2.0, H / 2.0);

        Assert.Equal(0.5f, p.U, precision: 3);
        Assert.Equal(0.5f, p.V, precision: 3);
    }

    [Fact]
    public void SemEspelhoOCantoDireitoVaiParaADireita()
    {
        var p = Converter(W - 1, H - 1);

        Assert.True(p.U > 0.99f, $"U={p.U}");
        Assert.True(p.V > 0.99f, $"V={p.V}");
    }

    // ───────────────────────── com espelho horizontal ─────────────────────────

    /// <summary>
    /// <b>O caso do defeito.</b> Com a prévia espelhada, o que aparece à esquerda da tela
    /// é o lado direito do campo. Clicar ali tem de acender ali.
    /// </summary>
    [Fact]
    public void ComEspelhoOCliqueEsquerdoVaiParaOLadoLogicoDireito()
    {
        var p = Converter(0, H / 2.0, flipH: true);

        Assert.True(p.U > 0.99f, $"Clique na borda esquerda deu U={p.U}; esperava o lado direito.");
    }

    [Fact]
    public void ComEspelhoOCentroContinuaNoCentro()
    {
        var p = Converter(W / 2.0, H / 2.0, flipH: true);

        Assert.Equal(0.5f, p.U, precision: 3);
        Assert.Equal(0.5f, p.V, precision: 3);
    }

    [Fact]
    public void ComEspelhoOCliqueDireitoVaiParaOLadoLogicoEsquerdo()
    {
        var p = Converter(W - 1, H / 2.0, flipH: true);

        Assert.True(p.U < 0.01f, $"Clique na borda direita deu U={p.U}; esperava o lado esquerdo.");
    }

    /// <summary>O espelho horizontal não pode mexer no eixo vertical.</summary>
    [Fact]
    public void OEspelhoHorizontalNaoAfetaOEixoVertical()
    {
        var sem = Converter(W / 4.0, H / 4.0);
        var com = Converter(W / 4.0, H / 4.0, flipH: true);

        Assert.Equal(sem.V, com.V, precision: 5);
        Assert.NotEqual(sem.U, com.U, precision: 3);
    }

    // ───────────────────────── espelho vertical ─────────────────────────

    [Fact]
    public void ComEspelhoVerticalOTopoVaiParaABase()
    {
        var p = Converter(W / 2.0, 0, flipV: true);

        Assert.True(p.V > 0.99f, $"Clique no topo deu V={p.V}; esperava a base.");
    }

    [Fact]
    public void ComEspelhoVerticalABaseVaiParaOTopo()
    {
        var p = Converter(W / 2.0, H - 1, flipV: true);

        Assert.True(p.V < 0.01f, $"Clique na base deu V={p.V}; esperava o topo.");
    }

    [Fact]
    public void OsDoisEspelhosJuntosInvertemOsDoisEixos()
    {
        var p = Converter(0, 0, flipH: true, flipV: true);

        Assert.True(p.U > 0.99f, $"U={p.U}");
        Assert.True(p.V > 0.99f, $"V={p.V}");
    }

    // ───────────────────────── ROI ─────────────────────────

    /// <summary>
    /// A prévia mostra um recorte, e a simulação trabalha no campo inteiro. A borda
    /// visual do recorte tem de cair na borda lógica dele, não na do campo.
    /// </summary>
    [Fact]
    public void ABordaEsquerdaDoRecorteCaiNoInicioDoRecorte()
    {
        // Recorte de 320x240 começando em (160, 120) dentro de um campo de 640x480.
        var p = Converter(0, 0, roiEsquerda: 160, roiTopo: 120,
                          larguraControle: 320, alturaControle: 240,
                          larguraImagem: 320, alturaImagem: 240);

        Assert.Equal(160f / W, p.U, precision: 4);
        Assert.Equal(120f / H, p.V, precision: 4);
    }

    [Fact]
    public void OCentroDoRecorteCaiNoCentroDoRecorte()
    {
        var p = Converter(160, 120, roiEsquerda: 160, roiTopo: 120,
                          larguraControle: 320, alturaControle: 240,
                          larguraImagem: 320, alturaImagem: 240);

        Assert.Equal((160f + 160f) / W, p.U, precision: 4);
        Assert.Equal((120f + 120f) / H, p.V, precision: 4);
    }

    /// <summary>Com recorte e espelho ao mesmo tempo, o espelho é dentro do recorte.</summary>
    [Fact]
    public void OEspelhoOcorreDentroDoRecorteNaoDoCampo()
    {
        var p = Converter(0, 120, flipH: true, roiEsquerda: 160, roiTopo: 120,
                          larguraControle: 320, alturaControle: 240,
                          larguraImagem: 320, alturaImagem: 240);

        // Borda esquerda da tela → borda direita do recorte → coluna 160+319 do campo.
        Assert.Equal((160f + 319f) / W, p.U, precision: 3);
    }

    // ───────────────────────── escala e tarjas ─────────────────────────

    /// <summary>
    /// O ponto lógico não pode depender do tamanho do controle. Redimensionar a janela
    /// muda quantos pixels de tela cada célula ocupa, e nada mais.
    /// </summary>
    [Theory]
    [InlineData(320, 240)]
    [InlineData(640, 480)]
    [InlineData(1280, 960)]
    public void OTamanhoDoControleNaoMudaOPontoLogico(double largura, double altura)
    {
        var p = Converter(largura * 0.25, altura * 0.75,
                          larguraControle: largura, alturaControle: altura);

        Assert.Equal(0.25f, p.U, precision: 3);
        Assert.Equal(0.75f, p.V, precision: 3);
    }

    /// <summary>
    /// Controle mais largo que a imagem: <c>Stretch="Uniform"</c> centraliza e deixa
    /// tarjas laterais. O clique no meio da imagem continua sendo o meio do campo.
    /// </summary>
    [Fact]
    public void ComTarjasLateraisOCentroDaImagemContinuaNoCentro()
    {
        // Controle 1000x480 para imagem 640x480: escala 1, tarjas de 180 de cada lado.
        var p = Converter(500, 240, larguraControle: 1000, alturaControle: 480);

        Assert.Equal(0.5f, p.U, precision: 3);
        Assert.Equal(0.5f, p.V, precision: 3);
    }

    [Fact]
    public void ComTarjasSuperioresOCentroDaImagemContinuaNoCentro()
    {
        // Controle 640x800 para imagem 640x480: tarjas de 160 acima e abaixo.
        var p = Converter(320, 400, larguraControle: 640, alturaControle: 800);

        Assert.Equal(0.5f, p.U, precision: 3);
        Assert.Equal(0.5f, p.V, precision: 3);
    }

    // ───────────────────────── fora da área útil ─────────────────────────

    /// <summary>
    /// Clique na tarja é recusado, não saturado. Acender fogo numa quina porque alguém
    /// errou o alvo seria pior que não acender.
    /// </summary>
    [Theory]
    [InlineData(50, 240)]    // tarja esquerda
    [InlineData(950, 240)]   // tarja direita
    public void CliqueNaTarjaERecusado(double x, double y)
    {
        Assert.True(Recusa(x, y, larguraControle: 1000, alturaControle: 480));
    }

    [Theory]
    [InlineData(-10, 100)]
    [InlineData(100, -10)]
    [InlineData(1000, 100)]
    [InlineData(100, 1000)]
    public void CliqueForaDoControleERecusado(double x, double y)
    {
        Assert.True(Recusa(x, y));
    }

    [Fact]
    public void SemImagemOuSemCampoRecusaEmVezDeDividirPorZero()
    {
        Assert.True(Recusa(10, 10, larguraImagem: 0, alturaImagem: 0));
        Assert.True(Recusa(10, 10, larguraCampo: 0, alturaCampo: 0));
        Assert.True(Recusa(10, 10, larguraControle: 0, alturaControle: 0));
    }

    /// <summary>
    /// Nenhuma entrada aceita pode produzir coordenada fora de 0 a 1 — é o que garante
    /// que a simulação nunca receba um índice absurdo.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TodoPontoAceitoCaiDentroDoCampo(bool flipH, bool flipV)
    {
        for (int x = 0; x < W; x += 7)
            for (int y = 0; y < H; y += 11)
            {
                var p = Converter(x, y, flipH, flipV);
                Assert.InRange(p.U, 0f, 1f);
                Assert.InRange(p.V, 0f, 1f);
            }
    }
}
