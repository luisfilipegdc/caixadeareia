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

public class LeitorDeContextoTests : IDisposable
{
    private readonly string _pasta;

    public LeitorDeContextoTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "caixa-contexto-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
    }

    public void Dispose()
    {
        try { Directory.Delete(_pasta, recursive: true); } catch { /* limpeza best-effort */ }
    }

    private string Gravar(string conteudo, string nome = "pacote.json")
    {
        string caminho = Path.Combine(_pasta, nome);
        File.WriteAllText(caminho, conteudo);
        return caminho;
    }

    private const string PacoteMinimo = """
        {
          "schemaVersion": 1,
          "proveniencia": {
            "fonte": "INPE — Programa Queimadas",
            "periodoObservado": "2026-08",
            "dataDeAcesso": "2026-08-28"
          },
          "contextos": [
            {
              "bioma": "Cerrado",
              "uf": "GOIÁS",
              "periodo": "2026-08",
              "observado": { "focos": 1234, "riscoFogoMediano": 1.0, "diasSemChuvaMediano": 43 },
              "classesDidaticas": { "risco": "Sem variação suficiente", "secura": "Muito seco", "classificacao": "relativa_ao_recorte" }
            }
          ]
        }
        """;

    [Fact]
    public void CarregaUmPacoteValido()
    {
        var r = LeitorDeContexto.Carregar(Gravar(PacoteMinimo));

        Assert.True(r.Carregou);
        Assert.Null(r.Erro);

        var c = Assert.Single(r.Contextos);
        Assert.Equal("Cerrado", c.Bioma);
        Assert.Equal("GOIÁS", c.Uf);
        Assert.Equal(1234, c.Observado!.Focos);
        Assert.Equal("Muito seco", c.ClassesDidaticas!.Secura);
        Assert.Equal("relativa_ao_recorte", c.ClassesDidaticas.Classificacao);
    }

    [Fact]
    public void AcentosSobrevivemAoCarregamento()
    {
        var r = LeitorDeContexto.Carregar(Gravar(PacoteMinimo));

        Assert.Contains("Queimadas", r.Pacote!.Proveniencia!.Fonte);
        Assert.Equal("GOIÁS", r.Contextos[0].Uf);
        Assert.Contains("variação", r.Contextos[0].ClassesDidaticas!.Risco);
    }

    /// <summary>
    /// Versão diferente é recusada em vez de interpretada. Um campo que mudou de
    /// significado entre versões viraria número errado na tela, sem aviso.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public void VersaoDeSchemaDiferenteERecusada(int versao)
    {
        var r = LeitorDeContexto.Carregar(
            Gravar(PacoteMinimo.Replace("\"schemaVersion\": 1", $"\"schemaVersion\": {versao}")));

        Assert.False(r.Carregou);
        Assert.Contains(versao.ToString(), r.Erro);
        Assert.Empty(r.Contextos);
    }

    [Fact]
    public void PacoteSemProcedenciaERecusado()
    {
        string sem = """
            { "schemaVersion": 1, "contextos": [] }
            """;

        var r = LeitorDeContexto.Carregar(Gravar(sem));

        Assert.False(r.Carregou);
        Assert.Contains("procedência", r.Erro);
    }

    [Fact]
    public void PacoteSemContextosCarregaMasVemVazio()
    {
        string vazio = """
            { "schemaVersion": 1, "proveniencia": { "fonte": "x" }, "contextos": [] }
            """;

        var r = LeitorDeContexto.Carregar(Gravar(vazio));

        Assert.True(r.Carregou);
        Assert.Empty(r.Contextos);
    }

    [Fact]
    public void ArquivoInexistenteNaoLanca()
    {
        var r = LeitorDeContexto.Carregar(Path.Combine(_pasta, "nao-existe.json"));

        Assert.False(r.Carregou);
        Assert.NotNull(r.Erro);
        Assert.Empty(r.Contextos);
    }

    [Fact]
    public void JsonCorrompidoNaoLanca()
    {
        var r = LeitorDeContexto.Carregar(Gravar("{ isso não é json"));

        Assert.False(r.Carregou);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public void ArquivoVazioNaoLanca()
    {
        var r = LeitorDeContexto.Carregar(Gravar(""));

        Assert.False(r.Carregou);
        Assert.NotNull(r.Erro);
    }

    [Fact]
    public void RotuloEResumoSaoLegiveis()
    {
        var r = LeitorDeContexto.Carregar(Gravar(PacoteMinimo));

        Assert.Equal("Cerrado · GOIÁS · 2026-08", r.Contextos[0].Rotulo);
        Assert.Contains("2026-08", r.Pacote!.Proveniencia!.Resumo);
        Assert.Contains("acesso em", r.Pacote.Proveniencia.Resumo);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // O contrato entre as duas pontas
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Este é o teste que justifica a duplicação dos tipos.</b>
    ///
    /// A ferramenta e o aplicativo não compartilham classe nenhuma — o acordo entre eles é
    /// o arquivo JSON. Aqui a ferramenta serializa e o aplicativo desserializa, o que
    /// prova que os dois lados concordam sem precisar de referência de projeto.
    ///
    /// Se alguém renomear um campo de um lado só, este teste quebra — que é exatamente o
    /// aviso que o compilador não pode dar.
    /// </summary>
    [Fact]
    public void OQueAFerramentaEscreveOAplicativoConsegueLer()
    {
        var focos = Enumerable.Range(0, 40)
            .Select(i => new Prep.FocoDeCalor(
                new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc),
                "JATAÍ", "GOIÁS", "Cerrado",
                DiasSemChuva: i, PrecipitacaoMm: 0, RiscoFogo: 0.5 + i * 0.01, FrpMw: 10 + i))
            .ToList();

        var pacoteDaFerramenta = new Prep.PacoteDeContexto(
            Prep.PacoteDeContexto.VersaoAtual,
            new Prep.Proveniencia(
                "INPE — Programa Queimadas", "INPE", "Focos de calor",
                "focos_diario_br_20260827.csv", "https://exemplo/arquivo.csv", "CSV",
                "2026-08", "2026-08-28", "dotnet run --project tools/...",
                ["sentinela -999 descartada"], "mediana e quartis",
                "quartis dos próprios recortes", ["contexto, não simulação"]),
            Prep.Agregador.Agregar(focos));

        string json = JsonSerializer.Serialize(pacoteDaFerramenta, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        var lido = LeitorDeContexto.Carregar(Gravar(json));

        Assert.True(lido.Carregou, lido.Erro);
        Assert.Equal(PacoteDeContexto.VersaoSuportada, lido.Pacote!.SchemaVersion);

        var c = Assert.Single(lido.Contextos);
        Assert.Equal("Cerrado", c.Bioma);
        Assert.Equal("GOIÁS", c.Uf);
        Assert.Equal("2026-08", c.Periodo);
        Assert.Equal(40, c.Observado!.Focos);
        Assert.Equal(40, c.Observado.Amostras!.RiscoFogo);
        Assert.NotNull(c.Observado.FrpMedianoMw);
        Assert.Equal("relativa_ao_recorte", c.ClassesDidaticas!.Classificacao);

        // Procedência inteira sobrevive à travessia.
        var p = lido.Pacote.Proveniencia!;
        Assert.Equal("INPE", p.Organizacao);
        Assert.Equal("focos_diario_br_20260827.csv", p.Recurso);
        Assert.Single(p.Filtros);
        Assert.Contains("quartis", p.MetodoDeClassificacao);
    }

    /// <summary>
    /// O pacote versionado no repositório precisa ser legível por este leitor. Se alguém
    /// regenerar com uma ferramenta incompatível e commitar, o teste acusa antes de a
    /// aula acusar.
    /// </summary>
    [Fact]
    public void OPacoteVersionadoNoRepositorioCarrega()
    {
        string caminho = Path.Combine(AppContext.BaseDirectory, "Dados",
                                      LeitorDeContexto.NomeDoArquivo);

        if (!File.Exists(caminho))
            return;   // ambiente sem o pacote copiado; nada a verificar

        var r = LeitorDeContexto.Carregar(caminho);

        Assert.True(r.Carregou, r.Erro);
        Assert.NotEmpty(r.Contextos);
        Assert.All(r.Contextos, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Bioma));
            Assert.NotNull(c.Observado);
            Assert.NotNull(c.ClassesDidaticas);
        });

        var p = r.Pacote!.Proveniencia!;
        Assert.False(string.IsNullOrWhiteSpace(p.Url));
        Assert.False(string.IsNullOrWhiteSpace(p.ComandoParaRegenerar));
        Assert.NotEmpty(p.Observacoes);
    }
}
