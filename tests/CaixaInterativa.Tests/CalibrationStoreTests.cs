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

using System.IO;
using CaixaInterativa.Config;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// A calibração salva é o que permite abrir o programa numa aula e usar sem recalibrar.
/// Se este arquivo corromper, o professor perde o passo mais demorado do fluxo com a
/// turma esperando — por isso a gravação é atômica e o carregamento é defensivo.
/// </summary>
public class CalibrationStoreTests : IDisposable
{
    private readonly string _pasta;

    public CalibrationStoreTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "caixa-testes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
    }

    public void Dispose()
    {
        try { Directory.Delete(_pasta, recursive: true); } catch { /* limpeza best-effort */ }
    }

    private string Caminho(string nome = "calibracao.dat") => Path.Combine(_pasta, nome);

    private static CalibrationData Exemplo(int w = 64, int h = 48)
    {
        var plano = new float[w * h];
        var valido = new bool[w * h];

        for (int i = 0; i < plano.Length; i++)
        {
            // Padrão reconhecível, com casas decimais, para pegar perda de precisão.
            plano[i] = 900f + (i % 37) * 0.25f;
            valido[i] = i % 5 != 0;   // ~20% inválidos, como uma caixa real
        }

        return new CalibrationData
        {
            Width = w,
            Height = h,
            BasePlaneMm = plano,
            BaseValid = valido,
            CoveragePercent = 80.0,
            AverageDistanceMm = 912.5,
            SavedAt = new DateTime(2026, 8, 27, 21, 30, 0, DateTimeKind.Local),
            SourceName = "Kinect v1 (near mode)",
        };
    }

    [Fact]
    public void GravarELerDevolveExatamenteOMesmo()
    {
        var original = Exemplo();
        string caminho = Caminho();

        CalibrationStore.Save(original, caminho);
        var lido = CalibrationStore.Load(original.Width, original.Height, caminho);

        Assert.NotNull(lido);
        Assert.Equal(original.Width, lido!.Width);
        Assert.Equal(original.Height, lido.Height);
        Assert.Equal(original.BasePlaneMm, lido.BasePlaneMm);
        Assert.Equal(original.BaseValid, lido.BaseValid);
        Assert.Equal(original.CoveragePercent, lido.CoveragePercent);
        Assert.Equal(original.AverageDistanceMm, lido.AverageDistanceMm);
        Assert.Equal(original.SavedAt, lido.SavedAt);
        Assert.Equal(original.SourceName, lido.SourceName);
    }

    /// <summary>
    /// A gravação escreve num temporário e move por cima. Se sobrar um `.tmp`, uma queda
    /// de energia no meio da próxima gravação poderia deixar dois arquivos divergentes.
    /// </summary>
    [Fact]
    public void GravacaoNaoDeixaArquivoTemporario()
    {
        string caminho = Caminho();
        CalibrationStore.Save(Exemplo(), caminho);

        Assert.True(File.Exists(caminho));
        Assert.False(File.Exists(caminho + ".tmp"));
        Assert.Single(Directory.GetFiles(_pasta));
    }

    /// <summary>
    /// Uma calibração de outra resolução não serve: os índices não correspondem e o mapa
    /// sairia embaralhado. Precisa recusar, não tentar adaptar.
    /// </summary>
    [Fact]
    public void ResolucaoDiferenteERecusada()
    {
        string caminho = Caminho();
        CalibrationStore.Save(Exemplo(64, 48), caminho);

        Assert.Null(CalibrationStore.Load(32, 24, caminho));
        Assert.Null(CalibrationStore.Load(64, 24, caminho));
        Assert.NotNull(CalibrationStore.Load(64, 48, caminho));
    }

    [Fact]
    public void ArquivoInexistenteDevolveNullSemLancar()
    {
        Assert.Null(CalibrationStore.Load(64, 48, Caminho("nao-existe.dat")));
        Assert.False(CalibrationStore.Exists(Caminho("nao-existe.dat")));
    }

    [Fact]
    public void ArquivoComAssinaturaErradaDevolveNull()
    {
        string caminho = Caminho();
        File.WriteAllBytes(caminho, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });

        Assert.Null(CalibrationStore.Load(64, 48, caminho));
    }

    /// <summary>
    /// Arquivo cortado ao meio — o cenário de queda de energia durante a gravação numa
    /// versão que não usasse temporário. Precisa devolver null, nunca dados parciais.
    /// </summary>
    [Fact]
    public void ArquivoTruncadoDevolveNull()
    {
        string caminho = Caminho();
        CalibrationStore.Save(Exemplo(), caminho);

        var bytes = File.ReadAllBytes(caminho);
        File.WriteAllBytes(caminho, bytes[..(bytes.Length / 2)]);

        Assert.Null(CalibrationStore.Load(64, 48, caminho));
    }

    [Fact]
    public void ArquivoVazioDevolveNull()
    {
        string caminho = Caminho();
        File.WriteAllBytes(caminho, []);

        Assert.Null(CalibrationStore.Load(64, 48, caminho));
    }

    /// <summary>
    /// Os booleanos de validade são empacotados em bits — 307.200 bytes viram 38.400 numa
    /// calibração real. O empacotamento precisa sobreviver ao ciclo, inclusive quando a
    /// contagem não é múltipla de 8.
    /// </summary>
    [Theory]
    [InlineData(8, 1)]
    [InlineData(17, 3)]
    [InlineData(33, 7)]
    public void EmpacotamentoDeBitsSobreviveAoCicloComTamanhoNaoMultiploDe8(int w, int h)
    {
        var plano = new float[w * h];
        var valido = new bool[w * h];
        for (int i = 0; i < valido.Length; i++)
        {
            plano[i] = i * 1.5f;
            valido[i] = i % 3 == 0;
        }

        var dados = new CalibrationData
        {
            Width = w, Height = h,
            BasePlaneMm = plano, BaseValid = valido,
            SavedAt = DateTime.Now, SourceName = "t",
        };

        string caminho = Caminho($"{w}x{h}.dat");
        CalibrationStore.Save(dados, caminho);
        var lido = CalibrationStore.Load(w, h, caminho);

        Assert.NotNull(lido);
        Assert.Equal(valido, lido!.BaseValid);
        Assert.Equal(plano, lido.BasePlaneMm);
    }

    [Fact]
    public void GravarPorCimaSubstituiOConteudoAnterior()
    {
        string caminho = Caminho();

        CalibrationStore.Save(Exemplo(), caminho);

        var novo = Exemplo();
        novo.BasePlaneMm[0] = 1234.5f;
        CalibrationStore.Save(novo, caminho);

        var lido = CalibrationStore.Load(64, 48, caminho);
        Assert.Equal(1234.5f, lido!.BasePlaneMm[0]);
    }
}
