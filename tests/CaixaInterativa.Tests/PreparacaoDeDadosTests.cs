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

using System.Globalization;
using System.IO;
using CaixaInterativa.DataPrep;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// Fixtures pequenas e locais. <b>Nenhum teste aqui toca a rede</b> — a ferramenta baixa
/// arquivo, mas quem baixa é o <c>Program</c>; parser e agregador recebem um
/// <see cref="TextReader"/> e não sabem de onde ele veio. Foi por isso que o desenho
/// separou as duas coisas.
/// </summary>
public class LeitorDeFocosCsvTests
{
    private const string Cabecalho =
        "id,lat,lon,data_hora_gmt,satelite,municipio,estado,pais,municipio_id,estado_id," +
        "pais_id,numero_dias_sem_chuva,precipitacao,risco_fogo,bioma,frp";

    private static string Linha(string municipio, string estado, string bioma,
                                string dias, string chuva, string risco, string frp,
                                string data = "2026-08-27 12:00:00") =>
        $"abc,-14.09,-46.24,{data},GOES-19,{municipio},{estado},Brasil,2917359,29,33," +
        $"{dias},{chuva},{risco},{bioma},{frp}";

    private static ResultadoDaLeitura Ler(params string[] linhas) =>
        LeitorDeFocosCsv.Ler(new StringReader(
            string.Join("\n", new[] { Cabecalho }.Concat(linhas))));

    [Fact]
    public void LeUmaLinhaCompleta()
    {
        var r = Ler(Linha("JABORANDI", "BAHIA", "Cerrado", "68.0", "0.0", "1.0", "88.6"));

        var f = Assert.Single(r.Focos);
        Assert.Empty(r.Rejeitadas);
        Assert.Equal("JABORANDI", f.Municipio);
        Assert.Equal("BAHIA", f.Estado);
        Assert.Equal("Cerrado", f.Bioma);
        Assert.Equal(68.0, f.DiasSemChuva);
        Assert.Equal(0.0, f.PrecipitacaoMm);
        Assert.Equal(1.0, f.RiscoFogo);
        Assert.Equal(88.6, f.FrpMw);
    }

    /// <summary>
    /// O caso que motivou o desenho. O INPE usa -999 para "não se aplica"; sem filtrar,
    /// a média de um campo entre 0 e 1 sai negativa.
    /// </summary>
    [Theory]
    [InlineData("-999")]
    [InlineData("-999.0")]
    public void SentinelaMenos999ViraNulo(string sentinela)
    {
        var f = Assert.Single(Ler(Linha("X", "GO", "Cerrado", sentinela, sentinela, sentinela, "10")).Focos);

        Assert.Null(f.DiasSemChuva);
        Assert.Null(f.PrecipitacaoMm);
        Assert.Null(f.RiscoFogo);
        Assert.Equal(10, f.FrpMw);
    }

    [Fact]
    public void CampoVazioOuIlegivelViraNuloSemRejeitarALinha()
    {
        var r = Ler(Linha("X", "GO", "Cerrado", "", "n/a", "1.0", ""));

        var f = Assert.Single(r.Focos);
        Assert.Empty(r.Rejeitadas);
        Assert.Null(f.DiasSemChuva);
        Assert.Null(f.PrecipitacaoMm);
        Assert.Null(f.FrpMw);
        Assert.Equal(1.0, f.RiscoFogo);
    }

    /// <summary>
    /// O INPE publica com ponto decimal. Numa máquina em pt-BR, <c>double.Parse</c> sem
    /// cultura leria <c>0.77</c> como <c>77</c> — sem exceção, sem aviso. O teste força a
    /// cultura da thread para provar que o parser não depende dela.
    /// </summary>
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("")]
    public void CulturaDecimalDaMaquinaNaoAfetaALeitura(string cultura)
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultura);
            var f = Assert.Single(Ler(Linha("X", "GO", "Cerrado", "7", "0.25", "0.77", "11.5")).Focos);

            Assert.Equal(0.77, f.RiscoFogo);
            Assert.Equal(0.25, f.PrecipitacaoMm);
            Assert.Equal(11.5, f.FrpMw);
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }

    [Fact]
    public void DataEInterpretadaComoUtc()
    {
        var f = Assert.Single(Ler(Linha("X", "GO", "Cerrado", "1", "0", "1", "1",
                                        data: "2026-08-27 23:45:00")).Focos);

        Assert.Equal(2026, f.DataHoraGmt.Year);
        Assert.Equal(8, f.DataHoraGmt.Month);
        Assert.Equal(27, f.DataHoraGmt.Day);
        Assert.Equal(DateTimeKind.Utc, f.DataHoraGmt.Kind);
    }

    [Fact]
    public void LinhaComDataInvalidaERejeitadaComMotivo()
    {
        var r = Ler(Linha("X", "GO", "Cerrado", "1", "0", "1", "1", data: "ontem"));

        Assert.Empty(r.Focos);
        var rej = Assert.Single(r.Rejeitadas);
        Assert.Equal(2, rej.Numero);
        Assert.Contains("data", rej.Motivo);
    }

    [Fact]
    public void LinhaSemBiomaERejeitada()
    {
        var r = Ler(Linha("X", "GO", "", "1", "0", "1", "1"));

        Assert.Empty(r.Focos);
        Assert.Contains("bioma", Assert.Single(r.Rejeitadas).Motivo);
    }

    [Fact]
    public void LinhaTruncadaERejeitadaSemDerrubarOResto()
    {
        var r = LeitorDeFocosCsv.Ler(new StringReader(string.Join("\n",
        [
            Cabecalho,
            Linha("BOM", "GO", "Cerrado", "1", "0", "1", "1"),
            "abc,-14,-46",
            Linha("OUTRO", "GO", "Cerrado", "2", "0", "1", "2"),
        ])));

        Assert.Equal(2, r.Focos.Count);
        Assert.Single(r.Rejeitadas);
        Assert.Equal(3, r.Rejeitadas[0].Numero);
    }

    [Fact]
    public void ColunaObrigatoriaAusenteInterrompeALeitura()
    {
        // Sem risco_fogo: se a origem mudar o formato, é melhor falhar alto do que
        // produzir um pacote silenciosamente vazio.
        string cabecalhoIncompleto = "data_hora_gmt,municipio,estado,bioma,numero_dias_sem_chuva,precipitacao,frp";

        var erro = Assert.Throws<FormatException>(() =>
            LeitorDeFocosCsv.Ler(new StringReader(cabecalhoIncompleto + "\n")));

        Assert.Contains("risco_fogo", erro.Message);
    }

    [Fact]
    public void ArquivoVazioInterrompeALeitura()
    {
        Assert.Throws<FormatException>(() => LeitorDeFocosCsv.Ler(new StringReader("")));
    }

    [Fact]
    public void ArquivoSoComCabecalhoNaoProduzFocos()
    {
        var r = LeitorDeFocosCsv.Ler(new StringReader(Cabecalho + "\n"));

        Assert.Empty(r.Focos);
        Assert.Empty(r.Rejeitadas);
    }

    [Fact]
    public void LinhasEmBrancoSaoIgnoradas()
    {
        var r = LeitorDeFocosCsv.Ler(new StringReader(string.Join("\n",
        [
            Cabecalho,
            "",
            Linha("X", "GO", "Cerrado", "1", "0", "1", "1"),
            "   ",
        ])));

        Assert.Single(r.Focos);
        Assert.Empty(r.Rejeitadas);
    }
}

/// <summary>
/// A agregação é onde o dado bruto vira algo que cabe numa aula. Estes testes travam as
/// escolhas estatísticas — não porque haja uma resposta certa universal, mas porque a
/// escolha foi deliberada e mudá-la sem querer alteraria o que o professor lê.
/// </summary>
public class AgregadorTests
{
    private static FocoDeCalor Foco(string bioma, string uf, double? risco, double? dias,
                                    double? frp = 10, double? chuva = 0, int dia = 27) =>
        new(new DateTime(2026, 8, dia, 12, 0, 0, DateTimeKind.Utc),
            "MUNICIPIO", uf, bioma, dias, chuva, risco, frp);

    private static List<FocoDeCalor> Repetir(string bioma, string uf, int quantidade,
                                             Func<int, FocoDeCalor> gerar) =>
        Enumerable.Range(0, quantidade).Select(gerar).ToList();

    [Fact]
    public void RecorteComPoucosFocosEDescartado()
    {
        var poucos = Repetir("Cerrado", "GO", Agregador.MinimoDeFocosPorRecorte - 1,
                             i => Foco("Cerrado", "GO", 0.5, i));

        Assert.Empty(Agregador.Agregar(poucos));
    }

    [Fact]
    public void RecorteComFocosSuficientesEMantido()
    {
        var bastantes = Repetir("Cerrado", "GO", Agregador.MinimoDeFocosPorRecorte,
                                i => Foco("Cerrado", "GO", 0.5, i));

        var c = Assert.Single(Agregador.Agregar(bastantes));
        Assert.Equal("Cerrado", c.Bioma);
        Assert.Equal("GO", c.Uf);
        Assert.Equal("2026-08", c.Periodo);
        Assert.Equal(Agregador.MinimoDeFocosPorRecorte, c.Observado.Focos);
    }

    [Fact]
    public void AgrupaPorBiomaUfEMes()
    {
        var focos = new List<FocoDeCalor>();
        focos.AddRange(Repetir("Cerrado", "GO", 40, i => Foco("Cerrado", "GO", 0.5, i)));
        focos.AddRange(Repetir("Cerrado", "MT", 40, i => Foco("Cerrado", "MT", 0.9, i)));
        focos.AddRange(Repetir("Caatinga", "BA", 40, i => Foco("Caatinga", "BA", 0.2, i)));

        var r = Agregador.Agregar(focos);

        Assert.Equal(3, r.Count);
        // Ordem estável: o pacote é versionado, e diff só é útil com ordem fixa.
        Assert.Equal(["Caatinga", "Cerrado", "Cerrado"], r.Select(c => c.Bioma));
        Assert.Equal(["BA", "GO", "MT"], r.Select(c => c.Uf));
    }

    /// <summary>
    /// Mediana em vez de média, e este teste mostra por quê: um único foco gigante
    /// arrastaria a média para longe do que é típico.
    /// </summary>
    [Fact]
    public void MedianaResisteAOutlier()
    {
        var focos = Repetir("Cerrado", "GO", 40, i => Foco("Cerrado", "GO", 0.5, 10, frp: 10));
        focos.Add(Foco("Cerrado", "GO", 0.5, 10, frp: 100_000));

        var c = Assert.Single(Agregador.Agregar(focos));

        Assert.Equal(10, c.Observado.FrpMedianoMw);   // média seria ~2.450
    }

    [Fact]
    public void SentinelaNaoEntraNaEstatisticaENemNaContagemDeAmostras()
    {
        var focos = Repetir("Cerrado", "GO", 30, i => Foco("Cerrado", "GO", 0.8, 5));
        focos.AddRange(Repetir("Cerrado", "GO", 10, i => Foco("Cerrado", "GO", null, null)));

        var c = Assert.Single(Agregador.Agregar(focos));

        Assert.Equal(40, c.Observado.Focos);              // todos contam como foco
        Assert.Equal(30, c.Observado.Amostras.RiscoFogo); // só 30 têm risco utilizável
        Assert.Equal(0.8, c.Observado.RiscoFogoMediano);
    }

    [Fact]
    public void QuartisSaoCalculadosPorInterpolacao()
    {
        var ordenados = new List<double> { 0, 10, 20, 30, 40 };

        Assert.Equal(0, Agregador.Percentil(ordenados, 0.0));
        Assert.Equal(10, Agregador.Percentil(ordenados, 0.25));
        Assert.Equal(20, Agregador.Percentil(ordenados, 0.50));
        Assert.Equal(30, Agregador.Percentil(ordenados, 0.75));
        Assert.Equal(40, Agregador.Percentil(ordenados, 1.0));
        Assert.Null(Agregador.Percentil([], 0.5));
    }

    [Fact]
    public void ClassificacaoDistribuiEmQuatroClassesQuandoHaVariacao()
    {
        // Quatro recortes com secura bem separada.
        var focos = new List<FocoDeCalor>();
        var seca = new[] { 1.0, 10.0, 30.0, 60.0 };
        var ufs = new[] { "AA", "BB", "CC", "DD" };
        for (int i = 0; i < 4; i++)
            focos.AddRange(Repetir("Cerrado", ufs[i], 40, _ => Foco("Cerrado", ufs[i], 0.5, seca[i])));

        var r = Agregador.Agregar(focos);

        Assert.Equal(4, r.Count);
        Assert.Equal(["Úmido", "Normal", "Seco", "Muito seco"], r.Select(c => c.ClassesDidaticas.Secura));
        Assert.All(r, c => Assert.Equal("relativa_ao_recorte", c.ClassesDidaticas.Classificacao));
    }

    /// <summary>
    /// O comportamento que evita inventar ciência: quando os quartis empatam, não há
    /// fronteira para quatro classes. Acontece de verdade com o risco de fogo, que satura
    /// perto de 1 nos focos detectados.
    /// </summary>
    [Fact]
    public void SemVariacaoNaoInventaQuatroClasses()
    {
        var focos = new List<FocoDeCalor>();
        foreach (var uf in new[] { "AA", "BB", "CC", "DD" })
            focos.AddRange(Repetir("Cerrado", uf, 40, _ => Foco("Cerrado", uf, 1.0, 10)));

        var r = Agregador.Agregar(focos);

        Assert.All(r, c => Assert.Equal(Agregador.SemVariacao, c.ClassesDidaticas.Risco));
        Assert.All(r, c => Assert.Equal(Agregador.SemVariacao, c.ClassesDidaticas.Secura));
    }

    [Fact]
    public void CortesPrecisamSerEstritamenteCrescentes()
    {
        Assert.True(Agregador.CortesDiscriminam([1, 2, 3]));
        Assert.False(Agregador.CortesDiscriminam([1, 1, 3]));
        Assert.False(Agregador.CortesDiscriminam([1, 2, 2]));
        Assert.False(Agregador.CortesDiscriminam([1, 2]));
        Assert.False(Agregador.CortesDiscriminam([]));
    }

    [Fact]
    public void CampoTodoNuloProduzClasseSemDado()
    {
        var focos = Repetir("Cerrado", "GO", 40, i => Foco("Cerrado", "GO", null, null));

        var c = Assert.Single(Agregador.Agregar(focos));

        Assert.Null(c.Observado.RiscoFogoMediano);
        Assert.Equal(Agregador.SemDado, c.ClassesDidaticas.Risco);
        Assert.Equal(0, c.Observado.Amostras.RiscoFogo);
    }

    [Fact]
    public void EntradaVaziaProduzPacoteVazio()
    {
        Assert.Empty(Agregador.Agregar([]));
    }

    /// <summary>
    /// A mesma entrada precisa gerar exatamente a mesma saída. Sem isso o pacote
    /// versionado produziria ruído no Git a cada regeneração, e a comparação entre duas
    /// aulas deixaria de ser confiável.
    /// </summary>
    [Fact]
    public void AgregacaoEDeterministica()
    {
        var focos = new List<FocoDeCalor>();
        foreach (var uf in new[] { "AA", "BB", "CC" })
            focos.AddRange(Repetir("Cerrado", uf, 40, i => Foco("Cerrado", uf, 0.1 * (i % 9), i % 50)));

        var a = Agregador.Agregar(focos);
        var b = Agregador.Agregar(focos.AsEnumerable().Reverse().ToList());

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++) Assert.Equal(a[i], b[i]);
    }
}
