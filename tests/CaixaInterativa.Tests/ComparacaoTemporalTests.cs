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
using System.Text.Json;
using CaixaInterativa.Contexto;
using Xunit;
using Prep = CaixaInterativa.DataPrep;

namespace CaixaInterativa.Tests;

/// <summary>
/// A comparação entre períodos é função pura, e é onde mora o risco pedagógico desta
/// capacidade: dois números lado a lado convidam a concluir que um explica o outro.
/// Estes testes travam o que ela diz e, principalmente, o que ela se recusa a dizer.
/// </summary>
public class ComparacaoTemporalTests
{
    private static ContextoTerritorial Ctx(
        string bioma, string uf, string periodo,
        int focos = 1000, double? risco = 0.5, double? dias = 10,
        double? chuva = 0.5, double? frp = 20, string classeRisco = "Moderado") =>
        new()
        {
            Bioma = bioma,
            Uf = uf,
            Periodo = periodo,
            Observado = new ObservacoesDoRecorte
            {
                Focos = focos,
                RiscoFogoMediano = risco,
                DiasSemChuvaMediano = dias,
                PrecipitacaoMedianaMm = chuva,
                FrpMedianoMw = frp,
            },
            ClassesDidaticas = new ClassesDidaticas
            {
                Risco = classeRisco,
                Secura = "Normal",
                Classificacao = "relativa_ao_recorte",
            },
        };

    private static Direcao DirecaoDe(ComparacaoDeContextos c, string campo) =>
        c.Campos.Single(x => x.Nome == campo).Direcao;

    // ───────────────────────── compatibilidade ─────────────────────────

    [Fact]
    public void MesmoTerritorioEmPeriodosDiferentesEComparavel()
    {
        var a = Ctx("Cerrado", "GOIÁS", "2026-06");
        var b = Ctx("Cerrado", "GOIÁS", "2026-07");

        Assert.True(ComparadorDeContextos.SaoCompativeis(a, b));

        var c = ComparadorDeContextos.Comparar(a, b);
        Assert.NotNull(c);
        Assert.Equal("Cerrado", c!.Bioma);
        Assert.Equal("GOIÁS", c.Uf);
        Assert.Equal("2026-06", c.PeriodoA);
        Assert.Equal("2026-07", c.PeriodoB);
    }

    [Theory]
    // bioma diferente
    [InlineData("Cerrado", "GOIÁS", "2026-06", "Caatinga", "GOIÁS", "2026-07")]
    // UF diferente
    [InlineData("Cerrado", "GOIÁS", "2026-06", "Cerrado", "BAHIA", "2026-07")]
    // mesmo período: não é comparação temporal
    [InlineData("Cerrado", "GOIÁS", "2026-06", "Cerrado", "GOIÁS", "2026-06")]
    public void TerritorioOuPeriodoIncompativelNaoCompara(
        string b1, string u1, string p1, string b2, string u2, string p2)
    {
        var a = Ctx(b1, u1, p1);
        var b = Ctx(b2, u2, p2);

        Assert.False(ComparadorDeContextos.SaoCompativeis(a, b));
        Assert.Null(ComparadorDeContextos.Comparar(a, b));
    }

    [Fact]
    public void ContextoAusenteNaoCompara()
    {
        var a = Ctx("Cerrado", "GOIÁS", "2026-06");

        Assert.Null(ComparadorDeContextos.Comparar(a, null));
        Assert.Null(ComparadorDeContextos.Comparar(null, a));
        Assert.Null(ComparadorDeContextos.Comparar(null, null));
    }

    // ───────────────────────── direção ─────────────────────────

    [Fact]
    public void AumentoEDiminuicaoSaoDetectados()
    {
        var a = Ctx("Cerrado", "GOIÁS", "2026-06", focos: 1000, dias: 5);
        var b = Ctx("Cerrado", "GOIÁS", "2026-07", focos: 3000, dias: 2);

        var c = ComparadorDeContextos.Comparar(a, b)!;

        Assert.Equal(Direcao.Aumentou, DirecaoDe(c, ComparadorDeContextos.CampoFocos));
        Assert.Equal(Direcao.Diminuiu, DirecaoDe(c, ComparadorDeContextos.CampoDiasSemChuva));
    }

    /// <summary>
    /// O limiar é declarado, e estes casos ficam de cada lado dele. Com 1000 focos, o
    /// limiar relativo de 10% vale 100 (maior que o piso de 10 focos): 1090 continua
    /// semelhante, 1120 já é aumento.
    /// </summary>
    [Theory]
    [InlineData(1000, 1000, Direcao.Semelhante)]   // idêntico
    [InlineData(1000, 1090, Direcao.Semelhante)]   // 9%, abaixo do limiar
    [InlineData(1000, 1120, Direcao.Aumentou)]     // 12%, acima
    [InlineData(1000, 910, Direcao.Semelhante)]    // −9%
    [InlineData(1000, 850, Direcao.Diminuiu)]      // −15%
    public void LimiarRelativoSeparaSemelhanteDeMudanca(int a, int b, Direcao esperado)
    {
        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", focos: a),
            Ctx("Cerrado", "GOIÁS", "2026-07", focos: b))!;

        Assert.Equal(esperado, DirecaoDe(c, ComparadorDeContextos.CampoFocos));
    }

    /// <summary>
    /// Sem piso absoluto, "2 focos → 3 focos" seria um aumento de 50%. O piso de dez
    /// focos impede que ruído de contagem vire notícia.
    /// </summary>
    [Theory]
    [InlineData(2, 3)]
    [InlineData(5, 12)]
    [InlineData(1, 10)]
    public void ValoresPequenosNaoProduzemVereditoPeloPercentual(int a, int b)
    {
        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", focos: a),
            Ctx("Cerrado", "GOIÁS", "2026-07", focos: b))!;

        Assert.Equal(Direcao.Semelhante, DirecaoDe(c, ComparadorDeContextos.CampoFocos));
    }

    [Fact]
    public void PisoAbsolutoEDeclaradoPorCampo()
    {
        Assert.Equal(10, ComparadorDeContextos.PisoDe(ComparadorDeContextos.CampoFocos));
        Assert.Equal(1, ComparadorDeContextos.PisoDe(ComparadorDeContextos.CampoDiasSemChuva));
        Assert.Equal(0.5, ComparadorDeContextos.PisoDe(ComparadorDeContextos.CampoPrecipitacao));
        Assert.Equal(2, ComparadorDeContextos.PisoDe(ComparadorDeContextos.CampoFrp));
        Assert.Equal(0.10, ComparadorDeContextos.VariacaoMinimaRelativa);
    }

    [Fact]
    public void CampoSemValorEmUmDosPeriodosViraSemDado()
    {
        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", frp: null),
            Ctx("Cerrado", "GOIÁS", "2026-07", frp: 30))!;

        Assert.Equal(Direcao.SemDado, DirecaoDe(c, ComparadorDeContextos.CampoFrp));
    }

    // ───────────────────────── risco saturado ─────────────────────────

    /// <summary>
    /// Quando a classificação já marcou o risco como sem variação suficiente, comparar
    /// 1,00 com 1,00 não produz informação. Dizer "semelhante" seria tecnicamente verdade
    /// e enganoso: sugere que a comparação encontrou igualdade, quando o campo não tem
    /// poder para encontrar diferença.
    /// </summary>
    [Fact]
    public void RiscoSaturadoNaoViraSemelhante()
    {
        const string SemVariacao = "Sem variação suficiente";

        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", risco: 1.0, classeRisco: SemVariacao),
            Ctx("Cerrado", "GOIÁS", "2026-07", risco: 1.0, classeRisco: SemVariacao))!;

        var campo = c.Campos.Single(x => x.Nome == ComparadorDeContextos.CampoRisco);

        Assert.Equal(Direcao.NaoDiscrimina, campo.Direcao);
        Assert.NotEqual(Direcao.Semelhante, campo.Direcao);
        Assert.Contains("sem poder discriminante", campo.Descrever());
    }

    [Fact]
    public void RiscoSaturadoEmUmSoDosPeriodosJaBastaParaNaoDiscriminar()
    {
        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", risco: 1.0, classeRisco: "Sem variação suficiente"),
            Ctx("Cerrado", "GOIÁS", "2026-07", risco: 0.3, classeRisco: "Baixo"))!;

        Assert.Equal(Direcao.NaoDiscrimina, DirecaoDe(c, ComparadorDeContextos.CampoRisco));
    }

    /// <summary>
    /// <b>Caso encontrado na validação em tela, não em teste.</b>
    ///
    /// Com dados mensais as classes se espalham, e Cerrado/GOIÁS ficou "Alto" nos dois
    /// meses — então a regra da classe não dispara. Mas os dois valores eram 1,00, e a
    /// tela respondeu "semelhante". No teto de uma escala que vai só até 1, não existe
    /// aumento possível: chamar isso de semelhança sugere uma medida que não foi feita.
    /// </summary>
    [Fact]
    public void RiscoNoTetoDaEscalaNaoDiscriminaAindaQueAClasseTenhaVariacao()
    {
        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", risco: 1.0, classeRisco: "Alto"),
            Ctx("Cerrado", "GOIÁS", "2026-07", risco: 1.0, classeRisco: "Alto"))!;

        var campo = c.Campos.Single(x => x.Nome == ComparadorDeContextos.CampoRisco);

        Assert.Equal(Direcao.NaoDiscrimina, campo.Direcao);
        Assert.DoesNotContain("semelhante", campo.Descrever());
    }

    /// <summary>
    /// Um lado no teto e o outro abaixo continua sendo informação: o campo separou os
    /// períodos. A regra do teto só apaga o veredito quando os dois estão lá em cima.
    /// </summary>
    [Fact]
    public void UmSoLadoNoTetoAindaProduzVeredito()
    {
        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", risco: 0.40, classeRisco: "Baixo"),
            Ctx("Cerrado", "GOIÁS", "2026-07", risco: 1.00, classeRisco: "Alto"))!;

        Assert.Equal(Direcao.Aumentou, DirecaoDe(c, ComparadorDeContextos.CampoRisco));
    }

    [Fact]
    public void RiscoComVariacaoEComparadoNormalmente()
    {
        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", risco: 0.30, classeRisco: "Baixo"),
            Ctx("Cerrado", "GOIÁS", "2026-07", risco: 0.85, classeRisco: "Alto"))!;

        Assert.Equal(Direcao.Aumentou, DirecaoDe(c, ComparadorDeContextos.CampoRisco));
    }

    // ───────────────────────── não causalidade ─────────────────────────

    /// <summary>
    /// O texto da ressalva é parte do contrato desta capacidade, não enfeite. Se alguém
    /// removê-lo, o teste acusa.
    /// </summary>
    [Fact]
    public void AComparacaoCarregaARessalvaDeNaoCausalidade()
    {
        Assert.Contains("Não estabelece causa", ComparacaoDeContextos.AvisoDeNaoCausalidade);
    }

    /// <summary>
    /// Nenhum texto produzido pela comparação pode afirmar causa. O vocabulário é de
    /// observação: aumentou, diminuiu, semelhante.
    /// </summary>
    [Fact]
    public void NenhumTextoDaComparacaoAfirmaCausa()
    {
        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", focos: 500, dias: 3),
            Ctx("Cerrado", "GOIÁS", "2026-07", focos: 4000, dias: 40))!;

        string[] proibidas = ["causou", "provocou", "porque", "devido", "resultou", "por isso"];

        foreach (var campo in c.Campos)
        {
            string texto = campo.Descrever().ToLowerInvariant();
            foreach (string p in proibidas)
                Assert.DoesNotContain(p, texto);
        }
    }

    [Fact]
    public void DescricaoMostraOsDoisValoresEOVeredito()
    {
        var c = ComparadorDeContextos.Comparar(
            Ctx("Cerrado", "GOIÁS", "2026-06", focos: 1000),
            Ctx("Cerrado", "GOIÁS", "2026-07", focos: 5000))!;

        string texto = c.Campos.Single(x => x.Nome == ComparadorDeContextos.CampoFocos).Descrever();

        Assert.Contains("→", texto);
        Assert.Contains("aumentou", texto);
    }

    // ───────────────────────── determinismo ─────────────────────────

    [Fact]
    public void CompararDuasVezesDaOMesmoResultado()
    {
        var a = Ctx("Cerrado", "GOIÁS", "2026-06", focos: 1234, dias: 7, frp: 15.5);
        var b = Ctx("Cerrado", "GOIÁS", "2026-07", focos: 4321, dias: 33, frp: 42.1);

        var x = ComparadorDeContextos.Comparar(a, b)!;
        var y = ComparadorDeContextos.Comparar(a, b)!;

        Assert.Equal(x.Campos.Count, y.Campos.Count);
        for (int i = 0; i < x.Campos.Count; i++) Assert.Equal(x.Campos[i], y.Campos[i]);
    }

    [Fact]
    public void InverterAOrdemInverteADirecao()
    {
        var a = Ctx("Cerrado", "GOIÁS", "2026-06", focos: 1000);
        var b = Ctx("Cerrado", "GOIÁS", "2026-07", focos: 5000);

        Assert.Equal(Direcao.Aumentou,
            DirecaoDe(ComparadorDeContextos.Comparar(a, b)!, ComparadorDeContextos.CampoFocos));
        Assert.Equal(Direcao.Diminuiu,
            DirecaoDe(ComparadorDeContextos.Comparar(b, a)!, ComparadorDeContextos.CampoFocos));
    }
}

/// <summary>
/// O pacote multiperíodo, ponta a ponta: a ferramenta agrega dois períodos, o aplicativo
/// lê, e a comparação funciona sobre o que atravessou o JSON.
/// </summary>
public class PacoteMultiperiodoTests : IDisposable
{
    private readonly string _pasta;

    public PacoteMultiperiodoTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "caixa-multi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
    }

    public void Dispose()
    {
        try { Directory.Delete(_pasta, recursive: true); } catch { /* best-effort */ }
    }

    private static List<Prep.FocoDeCalor> Focos(int mes, int quantidade, double risco, double dias) =>
        Enumerable.Range(0, quantidade)
            .Select(i => new Prep.FocoDeCalor(
                new DateTime(2026, mes, 1 + i % 28, 12, 0, 0, DateTimeKind.Utc),
                "JATAÍ", "GOIÁS", "Cerrado",
                DiasSemChuva: dias, PrecipitacaoMm: 0, RiscoFogo: risco, FrpMw: 10 + i % 7))
            .ToList();

    [Fact]
    public void DoisPeriodosProduzemDoisRecortesDoMesmoTerritorio()
    {
        var todos = new List<Prep.FocoDeCalor>();
        todos.AddRange(Focos(mes: 6, quantidade: 60, risco: 0.3, dias: 5));
        todos.AddRange(Focos(mes: 7, quantidade: 90, risco: 0.8, dias: 25));

        var contextos = Prep.Agregador.Agregar(todos);

        Assert.Equal(2, contextos.Count);
        Assert.Equal(["2026-06", "2026-07"], contextos.Select(c => c.Periodo));
        Assert.All(contextos, c => Assert.Equal("Cerrado", c.Bioma));
        Assert.All(contextos, c => Assert.Equal("GOIÁS", c.Uf));
    }

    [Fact]
    public void PacoteMultiperiodoSobreviveAoJsonEComparaCorretamente()
    {
        var todos = new List<Prep.FocoDeCalor>();
        todos.AddRange(Focos(mes: 6, quantidade: 60, risco: 0.30, dias: 5));
        todos.AddRange(Focos(mes: 7, quantidade: 200, risco: 0.85, dias: 30));

        var pacote = new Prep.PacoteDeContexto(
            Prep.PacoteDeContexto.VersaoAtual,
            new Prep.Proveniencia(
                "INPE — Programa Queimadas", "INPE", "Focos de calor", "CSV",
                "2026-08-28", "dotnet run ...", ["-999 descartado"],
                "mediana e quartis", "quartis dos recortes", ["contexto, não simulação"],
                [
                    new Prep.PeriodoObservado("2026-06", "focos_mensal_br_202606.csv", "https://x/06.csv", 30, 60),
                    new Prep.PeriodoObservado("2026-07", "focos_mensal_br_202607.csv", "https://x/07.csv", 31, 200),
                ]),
            Prep.Agregador.Agregar(todos));

        string caminho = Path.Combine(_pasta, "pacote.json");
        File.WriteAllText(caminho, JsonSerializer.Serialize(pacote, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));

        var lido = LeitorDeContexto.Carregar(caminho);
        Assert.True(lido.Carregou, lido.Erro);
        Assert.Equal(2, lido.Contextos.Count);

        // Procedência dos DOIS períodos atravessou o JSON.
        var prov = lido.Pacote!.Proveniencia!;
        Assert.Equal(2, prov.Periodos.Count);
        Assert.Equal("focos_mensal_br_202606.csv", prov.Origem("2026-06")!.Recurso);
        Assert.Equal("focos_mensal_br_202607.csv", prov.Origem("2026-07")!.Recurso);
        Assert.False(prov.Origem("2026-06")!.AmostraParcial);
        Assert.Null(prov.Origem("2026-09"));

        var a = lido.Contextos.Single(c => c.Periodo == "2026-06");
        var b = lido.Contextos.Single(c => c.Periodo == "2026-07");

        var comparacao = ComparadorDeContextos.Comparar(a, b);
        Assert.NotNull(comparacao);

        Assert.Equal(Direcao.Aumentou,
            comparacao!.Campos.Single(x => x.Nome == ComparadorDeContextos.CampoFocos).Direcao);
        Assert.Equal(Direcao.Aumentou,
            comparacao.Campos.Single(x => x.Nome == ComparadorDeContextos.CampoDiasSemChuva).Direcao);
    }

    /// <summary>
    /// Um período representado por poucos dias precisa se anunciar. Foi assim que o
    /// primeiro pacote do projeto passou um único dia por um mês inteiro.
    /// </summary>
    [Fact]
    public void PeriodoDeUmDiaSoEMarcadoComoAmostraParcial()
    {
        var um = new PeriodoObservado { Periodo = "2026-08", DiasObservados = 1 };
        var mes = new PeriodoObservado { Periodo = "2026-07", DiasObservados = 31 };

        Assert.True(um.AmostraParcial);
        Assert.False(mes.AmostraParcial);
    }

    /// <summary>
    /// O pacote versionado no repositório precisa ter dois períodos comparáveis — é o que
    /// esta sessão foi provar.
    /// </summary>
    [Fact]
    public void OPacoteDoRepositorioPermiteCompararDoisPeriodos()
    {
        string caminho = Path.Combine(AppContext.BaseDirectory, "Dados",
                                      LeitorDeContexto.NomeDoArquivo);
        if (!File.Exists(caminho)) return;

        var r = LeitorDeContexto.Carregar(caminho);
        Assert.True(r.Carregou, r.Erro);

        var periodos = r.Contextos.Select(c => c.Periodo).Distinct().OrderBy(p => p).ToList();
        Assert.True(periodos.Count >= 2, $"O pacote tem só {periodos.Count} período(s).");

        // Existe pelo menos um território presente nos dois períodos.
        var pares = r.Contextos
            .GroupBy(c => (c.Bioma, c.Uf))
            .Where(g => g.Select(c => c.Periodo).Distinct().Count() >= 2)
            .ToList();

        Assert.NotEmpty(pares);

        var grupo = pares[0].OrderBy(c => c.Periodo, StringComparer.Ordinal).ToList();
        var comparacao = ComparadorDeContextos.Comparar(grupo[0], grupo[1]);

        Assert.NotNull(comparacao);
        Assert.Equal(5, comparacao!.Campos.Count);
    }
}
