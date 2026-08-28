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

using CaixaInterativa.Contexto;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// O que a auditoria pedagógica encontrou, transformado em teste.
///
/// Estes não verificam pixel nem layout — verificam as promessas que o texto faz a quem
/// dá aula. São frágeis de propósito num único sentido: se alguém reescrever um texto e
/// perder a separação entre dado, hipótese e modelo, o teste acusa antes da sala.
/// </summary>
public class AuditoriaPedagogicaTests
{
    public static TheoryData<AtividadeConceitual> Atividades =>
    [
        AtividadeConceitual.QueimadasNoCerrado,
        AtividadeConceitual.MesmoTerritorioPeriodosDiferentes,
    ];

    // ───────────────── os quatro blocos existem e são distintos ─────────────────

    [Theory]
    [MemberData(nameof(Atividades))]
    public void AAtividadeTemOsQuatroBlocosPreenchidos(AtividadeConceitual a)
    {
        Assert.False(string.IsNullOrWhiteSpace(a.Pergunta));
        Assert.False(string.IsNullOrWhiteSpace(a.Observacao));
        Assert.False(string.IsNullOrWhiteSpace(a.Hipotese));
        Assert.False(string.IsNullOrWhiteSpace(a.Experimento));

        // Quatro blocos com o mesmo texto seriam quatro rótulos e um parágrafo só.
        var blocos = new[] { a.Pergunta, a.Observacao, a.Hipotese, a.Experimento };
        Assert.Equal(blocos.Length, blocos.Distinct().Count());
    }

    /// <summary>
    /// As três naturezas continuam nomeadas e separadas. Se alguém fundir duas delas num
    /// texto só, a tela perde a distinção que é o ponto inteiro desta capacidade.
    /// </summary>
    [Theory]
    [MemberData(nameof(Atividades))]
    public void AsTresNaturezasDaInformacaoContinuamSeparadas(AtividadeConceitual a)
    {
        Assert.False(string.IsNullOrWhiteSpace(a.DeOndeVemOContexto));
        Assert.False(string.IsNullOrWhiteSpace(a.DeOndeVemORelevo));
        Assert.False(string.IsNullOrWhiteSpace(a.DeOndeVemAPropagacao));

        var origens = new[] { a.DeOndeVemOContexto, a.DeOndeVemORelevo, a.DeOndeVemAPropagacao };
        Assert.Equal(origens.Length, origens.Distinct().Count());
    }

    // ───────────────── território real ≠ relevo da areia ─────────────────

    /// <summary>
    /// O mal-entendido mais provável da tela: "Cerrado · Goiás" ao lado de uma caixa de
    /// areia projetada. Toda atividade precisa negar isso explicitamente.
    /// </summary>
    [Theory]
    [MemberData(nameof(Atividades))]
    public void AAtividadeNegaQueAAreiaRepresenteOTerritorio(AtividadeConceitual a)
    {
        Assert.Contains(AtividadeConceitual.RelevoNaoRepresentaOTerritorio, a.DeOndeVemORelevo);
    }

    [Fact]
    public void OAvisoSobreORelevoDizOQuePrecisaDizer()
    {
        string aviso = AtividadeConceitual.RelevoNaoRepresentaOTerritorio;

        Assert.Contains("não representa o território real", aviso);
        Assert.Contains("alunos", aviso);
    }

    // ───────────────── a hipótese é sobre a caixa ─────────────────

    /// <summary>
    /// A hipótese não pode ser sobre o território observado — isso viraria previsão. Ela
    /// é sobre o que o modelo faz, e o texto precisa dizer que é hipotética.
    /// </summary>
    [Theory]
    [MemberData(nameof(Atividades))]
    public void AHipoteseEHipoteticaESobreOModelo(AtividadeConceitual a)
    {
        Assert.Contains("acham que aconteceria", a.Hipotese);
        Assert.Contains("caixa", a.Hipotese.ToLowerInvariant());
    }

    /// <summary>
    /// O experimento é escolhido à mão. Se o texto sugerir que a caixa se configura a
    /// partir do dado observado, a ponte que o professor deveria fazer conscientemente
    /// passa a ser feita pelo aplicativo.
    /// </summary>
    [Theory]
    [MemberData(nameof(Atividades))]
    public void OExperimentoDeixaAEscolhaComQuemDaAula(AtividadeConceitual a)
    {
        string t = a.Experimento.ToLowerInvariant();
        Assert.True(t.Contains("escolh"), $"O bloco EXPERIMENTO não fala em escolher: {a.Experimento}");
    }

    // ───────────────── nenhum texto afirma causa ─────────────────

    /// <summary>
    /// Vocabulário causal afirmativo não entra nos blocos que descrevem o dado.
    ///
    /// A verificação é sobre PERGUNTA e OBSERVAÇÃO, que são os textos lidos junto da
    /// tabela. Os blocos de origem podem — e devem — usar a palavra "causa" para negá-la,
    /// e por isso ficam fora desta varredura.
    /// </summary>
    [Theory]
    [MemberData(nameof(Atividades))]
    public void OsBlocosDeLeituraDoDadoNaoAfirmamCausa(AtividadeConceitual a)
    {
        string[] proibidas = ["porque", "provocou", "causou", "por isso", "resultou"];

        foreach (string texto in new[] { a.Pergunta, a.Observacao })
            foreach (string p in proibidas)
                Assert.DoesNotContain(p, texto.ToLowerInvariant());
    }

    /// <summary>
    /// A negação da causa continua explícita em algum lugar da atividade — não basta
    /// evitar as palavras, é preciso dizer que a coexistência não é causa.
    /// </summary>
    [Fact]
    public void AComparacaoTemporalNegaACausaComTodasAsLetras()
    {
        var a = AtividadeConceitual.MesmoTerritorioPeriodosDiferentes;

        Assert.Contains("não uma relação de causa", a.DeOndeVemOContexto);
        Assert.Contains("não estabelece causa",
                        ComparacaoDeContextos.AvisoDeNaoCausalidade.ToLowerInvariant());
    }

    /// <summary>
    /// O bloco OBSERVAÇÃO da comparação faz a pergunta que bloqueia a conclusão causal
    /// precoce, em vez de enunciar uma regra de epistemologia que ninguém aplica na hora.
    /// </summary>
    [Fact]
    public void AObservacaoPerguntaOQueNaoEstaNaTabela()
    {
        Assert.Contains("não aparece nestes números",
                        AtividadeConceitual.MesmoTerritorioPeriodosDiferentes.Observacao);
    }

    // ───────────────── linguagem: significado antes de estatística ─────────────────

    /// <summary>
    /// A leitura principal não usa vocabulário de estatística. "Mediana", "quartil" e
    /// "poder discriminante" continuam válidos — e continuam no painel de procedência,
    /// que é onde quem quiser auditar vai olhar.
    /// </summary>
    [Fact]
    public void OsRotulosDaComparacaoNaoUsamJargaoEstatistico()
    {
        string[] jargao = ["mediana", "quartil", "percentil", "discriminante", "recorte", "frp"];

        string[] rotulos =
        [
            ComparadorDeContextos.CampoFocos,
            ComparadorDeContextos.CampoDiasSemChuva,
            ComparadorDeContextos.CampoPrecipitacao,
            ComparadorDeContextos.CampoFrp,
            ComparadorDeContextos.CampoRisco,
        ];

        foreach (string r in rotulos)
            foreach (string j in jargao)
                Assert.DoesNotContain(j, r.ToLowerInvariant());
    }

    /// <summary>
    /// Sozinho, "1,00" é lido como "100% de chance de incêndio". O rótulo carrega a
    /// escala para que não seja.
    /// </summary>
    [Fact]
    public void ORotuloDoRiscoCarregaAEscala()
    {
        Assert.Contains("0 a 1", ComparadorDeContextos.CampoRisco);
    }

    // ───────────────── período legível ─────────────────

    [Theory]
    [InlineData("2026-06", "junho de 2026")]
    [InlineData("2026-07", "julho de 2026")]
    [InlineData("2025-01", "janeiro de 2025")]
    [InlineData("2025-12", "dezembro de 2025")]
    public void OPeriodoAparecePorExtenso(string iso, string esperado)
    {
        Assert.Equal(esperado, ContextoTerritorial.PeriodoPorExtenso(iso));
    }

    /// <summary>
    /// Formato inesperado devolve o texto original. Inventar um mês seria pior do que
    /// mostrar o valor cru.
    /// </summary>
    [Theory]
    [InlineData("2026")]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("sem-data")]
    [InlineData("")]
    public void PeriodoForaDoFormatoNaoEInventado(string entrada)
    {
        Assert.Equal(entrada, ContextoTerritorial.PeriodoPorExtenso(entrada));
    }
}
