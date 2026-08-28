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

using CaixaInterativa.Config;
using CaixaInterativa.Depth;
using CaixaInterativa.Processing;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// Caracteriza o <see cref="DepthProcessor"/> **sem alterá-lo**.
///
/// É o componente mais delicado do projeto depois do interop: as três etapas — buracos,
/// suavização temporal com α adaptativo, box blur — foram ajustadas contra hardware real,
/// e são a diferença entre uma projeção utilizável e uma que "ferve". Não havia nenhum
/// teste. Estes existem para que uma mudança futura precise ser deliberada.
///
/// Nada aqui propõe correção. Se algum destes testes falhar depois de uma alteração, o
/// comportamento validado em campo mudou.
/// </summary>
public class DepthProcessorTests
{
    private const int W = 64, H = 48;

    /// <summary>Sem blur, para isolar as etapas 1 e 2 do borramento espacial.</summary>
    private static ProcessingSettings SemBlur() => new() { SpatialBlurRadius = 0 };

    private static RawDepthFrame Quadro(ushort valor, long numero = 0)
    {
        var dados = new ushort[W * H];
        Array.Fill(dados, valor);
        return new RawDepthFrame { Data = dados, Width = W, Height = H, FrameNumber = numero };
    }

    private static DepthProcessor Calibrado(ushort distanciaMm, int quadros = 30,
                                            ProcessingSettings? ajustes = null)
    {
        var p = new DepthProcessor(W, H) { Settings = ajustes ?? SemBlur() };
        var saida = new float[W * H];

        p.BeginBaseCalibration(quadros);
        for (int i = 0; i < quadros; i++) p.ProcessFrame(Quadro(distanciaMm, i), saida);

        return p;
    }

    // ---------- Calibração ----------

    [Fact]
    public void CalibracaoEmSuperficiePlanaCobreTudo()
    {
        var p = Calibrado(900);

        Assert.True(p.IsCalibrated);
        Assert.False(p.IsCalibrating);
        Assert.Equal(100.0, p.CoveragePercent, precision: 6);
        Assert.Equal(900.0, p.AverageDistanceMm, precision: 3);
    }

    [Fact]
    public void DuranteACalibracaoASaidaFicaZerada()
    {
        var p = new DepthProcessor(W, H) { Settings = SemBlur() };
        var saida = new float[W * H];
        Array.Fill(saida, 123f);

        p.BeginBaseCalibration(10);
        p.ProcessFrame(Quadro(900), saida);

        Assert.True(p.IsCalibrating);
        Assert.All(saida, v => Assert.Equal(0f, v));
    }

    /// <summary>
    /// Um pixel que leu pouquíssimas vezes durante a calibração não ganha plano-base.
    /// O código exige um mínimo de amostras porque um pixel na borda do alcance
    /// promoveria ruído a referência — e daí todo o mapa daquele ponto sairia errado.
    /// </summary>
    [Fact]
    public void PixelIntermitenteNaoGanhaPlanoBase()
    {
        const int quadros = 30;
        var p = new DepthProcessor(W, H) { Settings = SemBlur() };
        var saida = new float[W * H];

        p.BeginBaseCalibration(quadros);
        for (int i = 0; i < quadros; i++)
        {
            var q = Quadro(900, i);
            // Um único pixel só é válido em 3 dos 30 quadros; o resto lê zero.
            if (i >= 3) q.Data[0] = 0;
            p.ProcessFrame(q, saida);
        }

        Assert.True(p.IsCalibrated);
        Assert.True(p.CoveragePercent < 100);
        Assert.True(p.CoveragePercent > 99, "Só um pixel deveria ter ficado de fora.");

        // Sem plano-base, aquele pixel fica permanentemente em zero — e não vira um pico.
        p.ProcessFrame(Quadro(850), saida);
        Assert.Equal(0f, saida[0]);
        Assert.Equal(50f, saida[1], precision: 3);
    }

    // ---------- Altura ----------

    [Fact]
    public void AlturaEADiferencaEmRelacaoAoPlanoBase()
    {
        var p = Calibrado(900);
        var saida = new float[W * H];

        p.ProcessFrame(Quadro(850), saida);   // 50 mm acima do plano

        Assert.All(saida, v => Assert.Equal(50f, v, precision: 3));
    }

    [Fact]
    public void EscavacaoProduzAlturaNegativa()
    {
        var p = Calibrado(900);
        var saida = new float[W * H];

        p.ProcessFrame(Quadro(940), saida);   // 40 mm abaixo do plano

        Assert.All(saida, v => Assert.Equal(-40f, v, precision: 3));
    }

    /// <summary>
    /// A faixa de cor é limitada por configuração; alturas fora dela são cortadas em vez
    /// de saturarem o mapa inteiro.
    /// </summary>
    [Fact]
    public void AlturaELimitadaAFaixaConfigurada()
    {
        var ajustes = new ProcessingSettings { SpatialBlurRadius = 0, MaxHeightMm = 120f };
        var p = Calibrado(900, ajustes: ajustes);
        var saida = new float[W * H];

        p.ProcessFrame(Quadro(500), saida);   // 400 mm acima, muito além do teto

        Assert.All(saida, v => Assert.Equal(120f, v, precision: 3));
    }

    // ---------- Etapa 1: buracos ----------

    /// <summary>
    /// Pixel inválido mantém o último valor bom. Zerar criaria crateras piscando nas
    /// bordas das mãos e dos montes de areia — foi o motivo de a etapa existir.
    /// </summary>
    [Fact]
    public void PixelInvalidoMantemOUltimoValorBom()
    {
        var p = Calibrado(900);
        var saida = new float[W * H];

        p.ProcessFrame(Quadro(850), saida);
        float antes = saida[0];

        var comBuraco = Quadro(850, 1);
        comBuraco.Data[0] = 0;                // sem leitura neste pixel
        p.ProcessFrame(comBuraco, saida);

        Assert.Equal(antes, saida[0], precision: 3);
        Assert.NotEqual(0f, saida[0]);
    }

    [Fact]
    public void PixelNuncaValidoFicaEmZeroSemNaN()
    {
        var p = Calibrado(900);
        var saida = new float[W * H];

        var todosInvalidos = Quadro(0);
        p.ProcessFrame(todosInvalidos, saida);

        Assert.All(saida, v =>
        {
            Assert.False(float.IsNaN(v));
            Assert.Equal(0f, v);
        });
    }

    // ---------- Etapa 2: α adaptativo ----------

    /// <summary>
    /// Movimento grande — uma mão entrando na caixa — passa do limiar e usa o α rápido,
    /// para não virar borrão arrastado.
    /// </summary>
    [Fact]
    public void SaltoGrandeUsaAlfaRapido()
    {
        var ajustes = new ProcessingSettings { SpatialBlurRadius = 0 };
        var p = Calibrado(900, ajustes: ajustes);
        var saida = new float[W * H];

        p.ProcessFrame(Quadro(900, 1), saida);        // primeira leitura: altura 0
        Assert.Equal(0f, saida[0], precision: 3);

        p.ProcessFrame(Quadro(850, 2), saida);        // salto de 50 mm > limiar de 25

        // 0 + 50 × FastAlpha(0,65) = 32,5
        Assert.Equal(50f * ajustes.FastAlpha, saida[0], precision: 2);
    }

    /// <summary>
    /// Variação pequena — o ruído de 2–4 mm do sensor — usa o α lento, que é o que
    /// impede a projeção de ferver com a areia parada.
    /// </summary>
    [Fact]
    public void VariacaoPequenaUsaAlfaLento()
    {
        var ajustes = new ProcessingSettings { SpatialBlurRadius = 0 };
        var p = Calibrado(900, ajustes: ajustes);
        var saida = new float[W * H];

        p.ProcessFrame(Quadro(900, 1), saida);
        p.ProcessFrame(Quadro(895, 2), saida);        // 5 mm, abaixo do limiar

        Assert.Equal(5f * ajustes.SmoothingAlpha, saida[0], precision: 2);
    }

    /// <summary>
    /// Com areia parada e ruído dentro do limiar, a saída converge e para de tremer.
    /// </summary>
    [Fact]
    public void AreiaParadaConvergeEEstabiliza()
    {
        var p = Calibrado(900);
        var saida = new float[W * H];
        var sorteio = new Random(2026);

        for (int i = 0; i < 120; i++)
        {
            ushort d = (ushort)(870 + sorteio.Next(-3, 4));   // 870 ± 3 mm
            p.ProcessFrame(Quadro(d, i + 1), saida);
        }

        float ultimo = saida[0];
        p.ProcessFrame(Quadro(870, 999), saida);

        Assert.InRange(ultimo, 25f, 35f);                    // perto dos 30 mm reais
        Assert.True(Math.Abs(saida[0] - ultimo) < 1.5f,
            $"A saída ainda oscila {Math.Abs(saida[0] - ultimo):F2} mm com areia parada.");
    }

    // ---------- Etapa 3: box blur ----------

    [Fact]
    public void BlurPreservaCampoConstanteInclusiveNasBordas()
    {
        var p = Calibrado(900, ajustes: new ProcessingSettings { SpatialBlurRadius = 3 });
        var saida = new float[W * H];

        p.ProcessFrame(Quadro(850), saida);

        Assert.All(saida, v => Assert.Equal(50f, v, precision: 2));
    }

    [Fact]
    public void RaioZeroDesligaOBlur()
    {
        var comBlur = Calibrado(900, ajustes: new ProcessingSettings { SpatialBlurRadius = 4 });
        var semBlur = Calibrado(900, ajustes: new ProcessingSettings { SpatialBlurRadius = 0 });

        var a = new float[W * H];
        var b = new float[W * H];

        // Um degrau: metade da cena mais alta que a outra.
        var degrau = Quadro(900);
        for (int y = 0; y < H; y++)
            for (int x = W / 2; x < W; x++)
                degrau.Data[y * W + x] = 850;

        comBlur.ProcessFrame(degrau, a);
        semBlur.ProcessFrame(degrau, b);

        // Sem blur o degrau é vertical; com blur, a transição é suavizada.
        Assert.Equal(0f, b[W / 2 - 1], precision: 3);
        Assert.Equal(50f, b[W / 2], precision: 3);
        Assert.True(a[W / 2 - 1] > 0f, "Com blur, o degrau deveria vazar para o vizinho.");
    }

    // ---------- Contratos ----------

    [Fact]
    public void QuadroComDimensaoDiferenteERecusado()
    {
        var p = Calibrado(900);
        var saida = new float[W * H];
        var outro = new RawDepthFrame
        {
            Data = new ushort[32 * 24], Width = 32, Height = 24, FrameNumber = 1,
        };

        Assert.Throws<ArgumentException>(() => p.ProcessFrame(outro, saida));
    }

    [Fact]
    public void ResetDescartaACalibracao()
    {
        var p = Calibrado(900);
        Assert.True(p.IsCalibrated);

        p.ResetCalibration();

        Assert.False(p.IsCalibrated);
        Assert.Equal(0.0, p.CoveragePercent);
    }

    // ---------- Exportar / importar ----------

    [Fact]
    public void ExportarEImportarPreservaOPlanoBase()
    {
        var origem = Calibrado(900);
        var dados = origem.Export("teste");
        Assert.NotNull(dados);

        var destino = new DepthProcessor(W, H) { Settings = SemBlur() };
        Assert.True(destino.Import(dados!));
        Assert.True(destino.IsCalibrated);

        var a = new float[W * H];
        var b = new float[W * H];
        origem.ProcessFrame(Quadro(850, 1), a);
        destino.ProcessFrame(Quadro(850, 1), b);

        Assert.Equal(a, b);
    }

    [Fact]
    public void ImportarDeOutraResolucaoERecusado()
    {
        var dados = Calibrado(900).Export("teste")!;
        var destino = new DepthProcessor(32, 24);

        Assert.False(destino.Import(dados));
        Assert.False(destino.IsCalibrated);
    }

    [Fact]
    public void ExportarSemCalibracaoDevolveNull()
    {
        Assert.Null(new DepthProcessor(W, H).Export("teste"));
    }
}
