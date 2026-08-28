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

namespace CaixaInterativa.Contexto;

/// <summary>Como um campo se comportou entre dois períodos.</summary>
public enum Direcao
{
    /// <summary>A diferença não passou do critério: os dois períodos são parecidos nisso.</summary>
    Semelhante,
    Aumentou,
    Diminuiu,

    /// <summary>Um dos períodos não tem valor utilizável neste campo.</summary>
    SemDado,

    /// <summary>
    /// O campo existe nos dois, mas não separa períodos — é o caso do risco de fogo,
    /// que satura perto de 1 nos focos detectados.
    /// </summary>
    NaoDiscrimina,
}

/// <summary>Um campo comparado, com os dois valores e o veredito.</summary>
public sealed record CampoComparado(
    string Nome,
    double? ValorA,
    double? ValorB,
    Direcao Direcao,
    string Unidade = "")
{
    /// <summary>Texto pronto para a tela: "1 234 → 2 100 (aumentou)".</summary>
    public string Descrever()
    {
        string Formatar(double? v) =>
            v is null ? "—" : v.Value.ToString(Math.Abs(v.Value) >= 100 ? "N0" : "0.##",
                                               CultureInfo.CurrentCulture) +
                              (Unidade.Length > 0 ? " " + Unidade : "");

        string veredito = Direcao switch
        {
            Direcao.Aumentou => "aumentou",
            Direcao.Diminuiu => "diminuiu",
            Direcao.Semelhante => "parecido nos dois",
            // Antes: "sem poder discriminante neste recorte". Correto e ilegível para quem
            // dá aula — "poder discriminante" e "recorte" são vocabulário de estatística.
            // A frase nova diz a mesma coisa: o dado existe, e não separa os períodos.
            Direcao.NaoDiscrimina => "os dois ficaram no mesmo patamar; este dado não separa os períodos",
            _ => "sem dado",
        };

        return Direcao is Direcao.NaoDiscrimina or Direcao.SemDado
            ? $"{Nome}: {veredito}"
            : $"{Nome}: {Formatar(ValorA)} → {Formatar(ValorB)} ({veredito})";
    }
}

/// <summary>O resultado inteiro de comparar dois períodos do mesmo território.</summary>
public sealed record ComparacaoDeContextos(
    string Bioma,
    string Uf,
    string PeriodoA,
    string PeriodoB,
    IReadOnlyList<CampoComparado> Campos)
{
    /// <summary>
    /// A ressalva que acompanha toda comparação, sem exceção.
    ///
    /// Não é decoração. Duas observações lado a lado convidam à conclusão de que uma
    /// explica a outra, e este dado não sustenta isso.
    /// </summary>
    /// <remarks>
    /// Reescrito na auditoria pedagógica. A frase anterior — "Comparação de observações
    /// externas. Não estabelece causa." — está correta e é abstrata demais: quem lê a
    /// tabela já formou a conclusão causal antes de chegar nela. A frase nova nomeia o
    /// erro que a pessoa está prestes a cometer, em vez de descrever uma categoria.
    /// </remarks>
    public const string AvisoDeNaoCausalidade =
        "São duas medições postas lado a lado. Não estabelece causa: duas coisas terem " +
        "mudado juntas não quer dizer que uma tenha mudado a outra.";
}

/// <summary>
/// Compara dois recortes do mesmo território em períodos diferentes.
///
/// <b>Função pura.</b> Recebe dois <see cref="ContextoTerritorial"/> e devolve o
/// resultado; não lê arquivo, não guarda estado, não conhece a interface.
///
/// <b>O que ela descreve, e o que ela recusa a dizer.</b> Ela responde "o que mudou entre
/// A e B" — nunca "por que mudou". Mais dias sem chuva e mais focos no mesmo período são
/// duas observações; tratá-las como causa e efeito seria afirmar o que este dado não
/// mostra. O texto que vai à tela é sempre da forma "no período B houve mais X e também
/// mais Y".
/// </summary>
public static class ComparadorDeContextos
{
    /// <summary>
    /// Diferença relativa a partir da qual deixamos de chamar dois valores de semelhantes.
    ///
    /// <b>Dez por cento, por convenção declarada — não derivada dos dados.</b> Não tenho
    /// base para afirmar qual variação é estatisticamente significativa aqui: o dado é uma
    /// contagem de detecções por satélite, sujeita a nuvem e a horário de passagem, e
    /// estimar o ruído disso exigiria um estudo que este projeto não fez.
    ///
    /// O que dez por cento faz é evitar os dois erros grosseiros: chamar de "aumentou" uma
    /// diferença de 2%, e chamar de "semelhante" uma de 40%. Está documentado como escolha
    /// para que ninguém o leia como resultado.
    /// </summary>
    public const double VariacaoMinimaRelativa = 0.10;

    /// <summary>
    /// Abaixo deste piso, a diferença é ignorada mesmo que passe dos 10%.
    ///
    /// Sem ele, "2 focos → 3 focos" viraria "aumentou 50%". Os valores são por campo,
    /// escolhidos como o menor degrau que ainda significa alguma coisa naquele campo:
    /// dez focos, um dia, meio milímetro, dois megawatts.
    /// </summary>
    public static double PisoDe(string campo) => campo switch
    {
        CampoFocos => 10,
        CampoDiasSemChuva => 1,
        CampoPrecipitacao => 0.5,
        CampoFrp => 2,
        _ => 0,
    };

    // Os rótulos que vão à tela.
    //
    // Trocaram de "(mediana)" para "(valor típico)" na auditoria pedagógica. A conta é a
    // mesma — continua sendo a mediana, e a procedência diz isso com todas as letras. O
    // que mudou é quem lê: "mediana" é vocabulário de estatística, e a leitura principal
    // é de quem dá aula. "Valor típico" é o que a mediana significa, sem prometer menos.
    //
    // "Potência radiativa" virou "calor liberado" pelo mesmo motivo, e o risco de fogo
    // passou a carregar a escala no próprio nome: sem ela, 1,00 é lido como "100% de
    // chance de incêndio", que não é o que o índice do INPE diz.
    public const string CampoFocos = "Focos de calor";
    public const string CampoDiasSemChuva = "Dias sem chuva (valor típico)";
    public const string CampoPrecipitacao = "Chuva registrada (valor típico)";
    public const string CampoFrp = "Calor liberado pelos focos (valor típico)";
    public const string CampoRisco = "Risco de fogo (índice de 0 a 1)";

    /// <summary>
    /// Dois contextos são comparáveis quando descrevem o mesmo território em períodos
    /// diferentes. Mesmo período não é comparação temporal; território diferente não é
    /// o mesmo território.
    /// </summary>
    public static bool SaoCompativeis(ContextoTerritorial? a, ContextoTerritorial? b) =>
        a is not null && b is not null
        && string.Equals(a.Bioma, b.Bioma, StringComparison.Ordinal)
        && string.Equals(a.Uf, b.Uf, StringComparison.Ordinal)
        && !string.Equals(a.Periodo, b.Periodo, StringComparison.Ordinal);

    /// <summary>
    /// Compara. Devolve <c>null</c> quando os contextos não são comparáveis — quem chama
    /// decide o que dizer, mas não recebe uma comparação sem sentido.
    /// </summary>
    public static ComparacaoDeContextos? Comparar(ContextoTerritorial? a, ContextoTerritorial? b)
    {
        if (!SaoCompativeis(a, b)) return null;

        var oa = a!.Observado;
        var ob = b!.Observado;

        var campos = new List<CampoComparado>
        {
            Avaliar(CampoFocos, oa?.Focos, ob?.Focos),
            Avaliar(CampoDiasSemChuva, oa?.DiasSemChuvaMediano, ob?.DiasSemChuvaMediano, "dias"),
            Avaliar(CampoPrecipitacao, oa?.PrecipitacaoMedianaMm, ob?.PrecipitacaoMedianaMm, "mm"),
            Avaliar(CampoFrp, oa?.FrpMedianoMw, ob?.FrpMedianoMw, "MW"),
            AvaliarRisco(a, b),
        };

        return new ComparacaoDeContextos(a.Bioma, a.Uf, a.Periodo, b.Periodo, campos);
    }

    private static CampoComparado Avaliar(string nome, double? a, double? b, string unidade = "")
    {
        if (a is null || b is null) return new CampoComparado(nome, a, b, Direcao.SemDado, unidade);

        double diferenca = b.Value - a.Value;
        double escala = Math.Max(Math.Abs(a.Value), Math.Abs(b.Value));

        // O maior entre o piso do campo e a fração da escala. Dois valores minúsculos não
        // produzem veredito só porque a razão entre eles é grande.
        double limiar = Math.Max(PisoDe(nome), escala * VariacaoMinimaRelativa);

        var direcao = Math.Abs(diferenca) <= limiar
            ? Direcao.Semelhante
            : diferenca > 0 ? Direcao.Aumentou : Direcao.Diminuiu;

        return new CampoComparado(nome, a, b, direcao, unidade);
    }

    /// <summary>
    /// O risco de fogo tem regra própria.
    ///
    /// Quando a classificação já marcou o campo como sem variação suficiente — o que
    /// acontece porque ele satura perto de 1 nos focos detectados —, comparar dois valores
    /// iguais a 1,00 não produz informação. Dizer "semelhante" seria tecnicamente verdade e
    /// pedagogicamente enganoso: sugere que a comparação foi feita e não encontrou
    /// diferença, quando na verdade o campo não tem poder para encontrar.
    /// </summary>
    private static CampoComparado AvaliarRisco(ContextoTerritorial a, ContextoTerritorial b)
    {
        const string SemVariacao = "Sem variação suficiente";

        double? va = a.Observado?.RiscoFogoMediano;
        double? vb = b.Observado?.RiscoFogoMediano;

        // Caso 1: a classificação do pacote já declarou que o campo não separa recortes.
        bool classeSemPoder =
            string.Equals(a.ClassesDidaticas?.Risco, SemVariacao, StringComparison.Ordinal) ||
            string.Equals(b.ClassesDidaticas?.Risco, SemVariacao, StringComparison.Ordinal);

        // Caso 2: os dois períodos estão no teto da escala.
        //
        // Encontrado na validação em tela: com dados mensais as classes se espalham, então
        // o caso 1 deixa de disparar — mas um par pode continuar saturado. Cerrado/GOIÁS
        // deu 1,00 nos dois meses e a comparação respondeu "semelhante", que é o veredito
        // tecnicamente correto e pedagogicamente enganoso: sugere que se mediu e não se
        // achou diferença, quando no teto de uma escala limitada não há como achar.
        bool ambosNoTeto = va >= TetoDoRiscoDeFogo && vb >= TetoDoRiscoDeFogo;

        return classeSemPoder || ambosNoTeto
            ? new CampoComparado(CampoRisco, va, vb, Direcao.NaoDiscrimina)
            : Avaliar(CampoRisco, va, vb);
    }

    /// <summary>
    /// Topo da escala do risco de fogo do INPE.
    ///
    /// Não é escolha nossa: o FAQ do Programa Queimadas declara que "os valores são
    /// válidos de 0 a 1". Um campo limitado em 1 não consegue subir a partir do teto, e
    /// é isso que torna a comparação entre dois valores máximos vazia de direção.
    /// </summary>
    public const double TetoDoRiscoDeFogo = 1.0;
}
