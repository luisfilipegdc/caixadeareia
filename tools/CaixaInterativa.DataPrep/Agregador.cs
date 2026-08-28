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

namespace CaixaInterativa.DataPrep;

/// <summary>
/// Reduz centenas de milhares de focos a algumas dezenas de linhas legíveis.
///
/// A granularidade é <b>bioma + UF + período</b>. Bioma porque é a unidade pedagógica que
/// o projeto já usa; UF porque dá ao professor um recorte que a turma reconhece; período
/// porque um dado sem data insinua que é de hoje.
/// </summary>
public static class Agregador
{
    /// <summary>
    /// Recortes com menos focos que isto são descartados.
    ///
    /// Uma mediana sobre cinco focos não descreve um território — descreve cinco pontos.
    /// Trinta é um piso conservador: mantém o pacote pequeno e evita publicar estatística
    /// que o próprio número de amostras desmente.
    /// </summary>
    public const int MinimoDeFocosPorRecorte = 30;

    /// <summary>
    /// Agrega e classifica. A classificação é feita depois da agregação, sobre os
    /// recortes — ver <see cref="Classificar"/> para o motivo.
    /// </summary>
    public static IReadOnlyList<ContextoTerritorial> Agregar(IEnumerable<FocoDeCalor> focos)
    {
        ArgumentNullException.ThrowIfNull(focos);

        var grupos = focos
            .GroupBy(f => (f.Bioma, f.Estado, Periodo: f.DataHoraGmt.ToString("yyyy-MM", CultureInfo.InvariantCulture)))
            .Where(g => g.Count() >= MinimoDeFocosPorRecorte)
            // Ordem fixa: o pacote é versionado no Git, e um diff só é útil se a ordem
            // das linhas não mudar entre execuções com a mesma entrada.
            .OrderBy(g => g.Key.Bioma, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Estado, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Periodo, StringComparer.Ordinal)
            .ToList();

        var observados = grupos
            .Select(g => (g.Key, Obs: Observar(g)))
            .ToList();

        // Cortes de classe derivados do próprio conjunto de recortes.
        var cortesRisco = Cortes(observados.Select(o => o.Obs.RiscoFogoMediano));
        var cortesSecura = Cortes(observados.Select(o => o.Obs.DiasSemChuvaMediano));

        return observados
            .Select(o => new ContextoTerritorial(
                o.Key.Bioma,
                o.Key.Estado,
                o.Key.Periodo,
                o.Obs,
                new ClassesDidaticas(
                    Risco: Classificar(o.Obs.RiscoFogoMediano, cortesRisco, EscalaDeRisco),
                    Secura: Classificar(o.Obs.DiasSemChuvaMediano, cortesSecura, EscalaDeSecura),
                    Classificacao: "relativa_ao_recorte")))
            .ToList();
    }

    private static readonly string[] EscalaDeRisco = ["Baixo", "Moderado", "Alto", "Muito alto"];
    private static readonly string[] EscalaDeSecura = ["Úmido", "Normal", "Seco", "Muito seco"];

    private static ObservacoesDoRecorte Observar(IEnumerable<FocoDeCalor> grupo)
    {
        var lista = grupo.ToList();

        var risco = Validos(lista.Select(f => f.RiscoFogo));
        var secura = Validos(lista.Select(f => f.DiasSemChuva));
        var chuva = Validos(lista.Select(f => f.PrecipitacaoMm));
        var frp = Validos(lista.Select(f => f.FrpMw));

        return new ObservacoesDoRecorte(
            Focos: lista.Count,
            RiscoFogoMediano: Percentil(risco, 0.50),
            RiscoFogoP25: Percentil(risco, 0.25),
            RiscoFogoP75: Percentil(risco, 0.75),
            DiasSemChuvaMediano: Percentil(secura, 0.50),
            DiasSemChuvaP75: Percentil(secura, 0.75),
            PrecipitacaoMedianaMm: Percentil(chuva, 0.50),
            FrpMedianoMw: Percentil(frp, 0.50),
            Amostras: new AmostrasValidas(risco.Count, secura.Count, chuva.Count, frp.Count));
    }

    private static List<double> Validos(IEnumerable<double?> valores)
    {
        var v = valores.Where(x => x.HasValue).Select(x => x!.Value).ToList();
        v.Sort();
        return v;
    }

    /// <summary>
    /// Percentil por interpolação linear, arredondado a três casas.
    ///
    /// O arredondamento existe para o pacote ser determinístico e diff-amigável: sem ele,
    /// a última casa flutuaria entre execuções e todo regenerar produziria ruído no Git.
    /// Três casas é mais precisão do que qualquer uso pedagógico exige.
    /// </summary>
    public static double? Percentil(IReadOnlyList<double> ordenados, double q)
    {
        if (ordenados.Count == 0) return null;
        if (ordenados.Count == 1) return Math.Round(ordenados[0], 3);

        double pos = q * (ordenados.Count - 1);
        int baixo = (int)Math.Floor(pos);
        int alto = (int)Math.Ceiling(pos);
        double peso = pos - baixo;

        return Math.Round(ordenados[baixo] * (1 - peso) + ordenados[alto] * peso, 3);
    }

    /// <summary>
    /// Os três cortes que separam quatro classes, tirados dos quartis dos próprios
    /// recortes do pacote.
    /// </summary>
    private static double[] Cortes(IEnumerable<double?> valores)
    {
        var v = Validos(valores);
        if (v.Count == 0) return [];
        return [Percentil(v, 0.25)!.Value, Percentil(v, 0.50)!.Value, Percentil(v, 0.75)!.Value];
    }

    /// <summary>
    /// Traduz um número em classe comparando-o com os quartis do conjunto.
    ///
    /// <b>Por que relativo, e não a escala nomeada do INPE.</b> O INPE publica classes
    /// nomeadas para o Risco de Fogo, mas não consegui confirmar os valores de corte numa
    /// fonte primária legível — o FAQ do Programa Queimadas diz apenas que "os valores são
    /// válidos de 0 a 1". Codificar cortes que não consegui verificar seria inventar
    /// ciência, que é justamente o que este projeto não faz.
    ///
    /// Há um segundo motivo, empírico: nos focos <i>detectados</i>, o risco satura perto
    /// de 1 — medido num dia real, p25 = 0,77 e mediana = 1,00. Faz sentido, porque o fogo
    /// acontece onde o risco é alto. Uma escala absoluta colocaria quase todo bioma em
    /// "crítico", o que é tecnicamente defensável e pedagogicamente inútil.
    ///
    /// Comparar recortes entre si responde a pergunta que a aula faz — "onde está mais
    /// seco que o resto?" — e o pacote registra que a classificação é relativa.
    /// </summary>
    private static string Classificar(double? valor, double[] cortes, string[] escala)
    {
        if (valor is null) return SemDado;
        if (!CortesDiscriminam(cortes)) return SemVariacao;

        double v = valor.Value;
        if (v <= cortes[0]) return escala[0];
        if (v <= cortes[1]) return escala[1];
        if (v <= cortes[2]) return escala[2];
        return escala[3];
    }

    /// <summary>Não havia valor para classificar.</summary>
    public const string SemDado = "Sem dado";

    /// <summary>
    /// O campo existe, mas não separa os recortes uns dos outros.
    ///
    /// Acontece quando os quartis empatam — e acontece de verdade com o risco de fogo:
    /// medido num dia real, <b>15 dos 29 recortes tinham mediana exatamente 1,0</b>, o
    /// topo da escala. Com p50 = p75 = 1,0 não existe fronteira que separe "alto" de
    /// "muito alto", e qualquer divisão em quatro níveis seria inventada.
    ///
    /// Dizer "sem variação suficiente" é menos vistoso que exibir quatro classes coloridas,
    /// e é o que os dados sustentam.
    /// </summary>
    public const string SemVariacao = "Sem variação suficiente";

    /// <summary>
    /// Os cortes só servem se forem estritamente crescentes. Empate em qualquer par
    /// significa que a distribuição está concentrada e não comporta quatro classes.
    /// </summary>
    public static bool CortesDiscriminam(double[] cortes) =>
        cortes.Length == 3 && cortes[0] < cortes[1] && cortes[1] < cortes[2];
}
