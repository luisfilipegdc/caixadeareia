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
/// A assinatura precisa acertar duas coisas ao mesmo tempo: ignorar o ruído do sensor e
/// perceber uma modificação de verdade. Errar em qualquer direção estraga a comparação
/// pedagógica — um falso alarme constante faz o professor ignorar o aviso, e um alarme
/// que não dispara deixa passar uma conclusão falsa.
/// </summary>
public class AssinaturaDoRelevoTests
{
    private const int W = 640, H = 480;

    private static float[] Terreno(float amplitudeMm = 100f)
    {
        var campo = new float[W * H];
        for (int y = 0; y < H; y++)
        {
            double ny = (y - H / 2.0) / (H / 2.0);
            for (int x = 0; x < W; x++)
            {
                double nx = (x - W / 2.0) / (W / 2.0);
                double h = Math.Exp(-((nx - 0.3) * (nx - 0.3) + (ny - 0.2) * (ny - 0.2)) / 0.18)
                         - 0.5 * Math.Exp(-((nx + 0.4) * (nx + 0.4) + (ny + 0.3) * (ny + 0.3)) / 0.12);
                campo[y * W + x] = (float)(h * amplitudeMm);
            }
        }
        return campo;
    }

    [Fact]
    public void CampoIdenticoTemDiferencaZero()
    {
        var a = AssinaturaDoRelevo.De(Terreno(), W, H)!;
        var b = AssinaturaDoRelevo.De(Terreno(), W, H)!;

        var r = a.Comparar(b);

        Assert.True(r.MesmoRelevo);
        Assert.Equal(0f, r.DiferencaMaximaMm, precision: 4);
        Assert.Equal(0f, r.DiferencaMediaMm, precision: 4);
    }

    /// <summary>
    /// O teste que justifica a existência da classe. O Kinect v1 tem 2–4 mm de ruído por
    /// pixel; se a assinatura reagisse a isso, avisaria "o relevo mudou" em toda
    /// comparação, e o aviso viraria ruído de interface.
    ///
    /// A média sobre 1.600 pixels por região dilui ruído independente para bem abaixo de
    /// um milímetro. Semente fixa para o teste não oscilar.
    /// </summary>
    [Fact]
    public void RuidoDoSensorNaoContaComoMudanca()
    {
        var limpo = Terreno();
        var ruidoso = (float[])limpo.Clone();

        var sorteio = new Random(20260827);
        for (int i = 0; i < ruidoso.Length; i++)
            ruidoso[i] += (float)(sorteio.NextDouble() * 8.0 - 4.0);   // ±4 mm

        var r = AssinaturaDoRelevo.De(limpo, W, H)!
            .Comparar(AssinaturaDoRelevo.De(ruidoso, W, H)!);

        Assert.True(r.MesmoRelevo,
            $"Ruído de ±4 mm produziu diferença de {r.DiferencaMaximaMm:F2} mm.");

        // Margem confortável: bem abaixo da tolerância, não em cima dela.
        Assert.True(r.DiferencaMaximaMm < 1f,
            $"Esperava menos de 1 mm depois da média; deu {r.DiferencaMaximaMm:F2} mm.");
    }

    /// <summary>
    /// Um aluno cavando um buraco de 5 cm numa região precisa ser detectado. É o caso que
    /// invalida a comparação entre coberturas.
    /// </summary>
    [Fact]
    public void EscavacaoLocalizadaEDetectada()
    {
        var antes = Terreno();
        var depois = (float[])antes.Clone();

        // Um buraco de 50 mm cobrindo cerca de uma região da grade.
        for (int y = H / 3; y < H / 3 + H / 12; y++)
            for (int x = W / 4; x < W / 4 + W / 16; x++)
                depois[y * W + x] -= 50f;

        var r = AssinaturaDoRelevo.De(antes, W, H)!
            .Comparar(AssinaturaDoRelevo.De(depois, W, H)!);

        Assert.False(r.MesmoRelevo);
        Assert.True(r.DiferencaMaximaMm > AssinaturaDoRelevo.ToleranciaMm);
    }

    /// <summary>
    /// Aplainar a caixa inteira é a mudança mais óbvia possível e precisa ser detectada
    /// com folga.
    /// </summary>
    [Fact]
    public void AplainarACaixaEDetectado()
    {
        var r = AssinaturaDoRelevo.De(Terreno(), W, H)!
            .Comparar(AssinaturaDoRelevo.De(new float[W * H], W, H)!);

        Assert.False(r.MesmoRelevo);
        Assert.True(r.DiferencaMaximaMm > 30f);
    }

    /// <summary>
    /// Uma diferença logo abaixo da tolerância não deve alarmar; logo acima, deve.
    /// Verifica que o limiar é o que a constante diz, e não outro escondido.
    /// </summary>
    [Theory]
    [InlineData(5f, true)]
    [InlineData(9f, true)]
    [InlineData(11f, false)]
    [InlineData(40f, false)]
    public void ToleranciaVigoraNoValorDeclarado(float deslocamentoMm, bool esperaMesmoRelevo)
    {
        var antes = Terreno();
        var depois = (float[])antes.Clone();
        for (int i = 0; i < depois.Length; i++) depois[i] += deslocamentoMm;

        var r = AssinaturaDoRelevo.De(antes, W, H)!
            .Comparar(AssinaturaDoRelevo.De(depois, W, H)!);

        Assert.Equal(esperaMesmoRelevo, r.MesmoRelevo);
    }

    [Fact]
    public void ComparacaoESimetrica()
    {
        var a = AssinaturaDoRelevo.De(Terreno(), W, H)!;
        var b = AssinaturaDoRelevo.De(Terreno(80f), W, H)!;

        Assert.Equal(a.Comparar(b).DiferencaMaximaMm, b.Comparar(a).DiferencaMaximaMm, precision: 4);
        Assert.Equal(a.Comparar(b).MesmoRelevo, b.Comparar(a).MesmoRelevo);
    }

    /// <summary>
    /// A grade é fixa, então assinaturas de resoluções diferentes ainda se comparam. Isso
    /// importa se um dia houver outra fonte de profundidade.
    /// </summary>
    [Fact]
    public void ResolucoesDiferentesProduzemAssinaturasComparaveis()
    {
        var grande = AssinaturaDoRelevo.De(new float[W * H], W, H)!;
        var pequeno = AssinaturaDoRelevo.De(new float[320 * 240], 320, 240)!;

        Assert.Equal(AssinaturaDoRelevo.Colunas * AssinaturaDoRelevo.Linhas, grande.Medias.Count);
        Assert.Equal(grande.Medias.Count, pequeno.Medias.Count);
        Assert.True(grande.Comparar(pequeno).MesmoRelevo);
    }

    /// <summary>
    /// Entrada inválida devolve null em vez de lançar. Quem chama registra "não foi
    /// possível verificar" — nunca uma comparação silenciosamente errada.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 4)]
    [InlineData(W, 8)]
    public void EntradaPequenaDemaisDevolveNull(int largura, int altura)
    {
        Assert.Null(AssinaturaDoRelevo.De(new float[Math.Max(1, largura * altura)], largura, altura));
    }

    [Fact]
    public void CampoNuloOuCurtoDevolveNull()
    {
        Assert.Null(AssinaturaDoRelevo.De(null, W, H));
        Assert.Null(AssinaturaDoRelevo.De(new float[10], W, H));
    }
}
