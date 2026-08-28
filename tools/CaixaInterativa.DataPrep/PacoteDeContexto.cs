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

namespace CaixaInterativa.DataPrep;

/// <summary>
/// O pacote que esta ferramenta produz e o aplicativo consome.
///
/// <b>Estes tipos são duplicados no aplicativo, de propósito.</b> O contrato entre as
/// duas pontas é o <b>arquivo JSON</b>, não uma classe compartilhada — é o que permite ao
/// projeto WPF não depender desta ferramenta, como exigido. A garantia de que os dois
/// lados concordam vem de um teste que serializa aqui e desserializa lá, não do
/// compilador.
///
/// <see cref="SchemaVersion"/> é o que torna essa duplicação segura: o leitor recusa um
/// pacote de versão diferente em vez de interpretá-lo torto.
/// </summary>
public sealed record PacoteDeContexto(
    int SchemaVersion,
    Proveniencia Proveniencia,
    IReadOnlyList<ContextoTerritorial> Contextos)
{
    /// <summary>Versão atual do formato. Mudou a forma, mudou o número.</summary>
    public const int VersaoAtual = 1;
}

/// <summary>
/// De onde veio cada número, e o que foi feito com ele.
///
/// Existe para responder a uma pergunta que o professor pode fazer em sala: "de onde saiu
/// isso?". Sem ela, um dado externo na tela é indistinguível de um número inventado.
/// </summary>
public sealed record Proveniencia(
    string Fonte,
    string Organizacao,
    string Conjunto,
    string Recurso,
    string Url,
    string FormatoOriginal,
    string PeriodoObservado,
    string DataDeAcesso,
    string ComandoParaRegenerar,
    IReadOnlyList<string> Filtros,
    string MetodoDeAgregacao,
    string MetodoDeClassificacao,
    IReadOnlyList<string> Observacoes);

/// <summary>Uma combinação bioma + UF + período, com o que se observou e como se classificou.</summary>
public sealed record ContextoTerritorial(
    string Bioma,
    string Uf,
    string Periodo,
    ObservacoesDoRecorte Observado,
    ClassesDidaticas ClassesDidaticas);

/// <summary>
/// Os números como saíram do dado, depois de descartada a sentinela.
///
/// <b>Mediana e quartis, não média.</b> Medido numa amostra real de 28.519 focos: a média
/// de <c>frp</c> é 43,7 MW e a mediana 11,5 — a cauda de incêndios enormes puxa a média
/// para longe do que é típico. E antes de filtrar o -999, a média de <c>risco_fogo</c>
/// saía negativa. Estatística robusta aqui não é preciosismo, é a diferença entre um
/// número que descreve o conjunto e um que descreve os outliers.
/// </summary>
public sealed record ObservacoesDoRecorte(
    int Focos,
    double? RiscoFogoMediano,
    double? RiscoFogoP25,
    double? RiscoFogoP75,
    double? DiasSemChuvaMediano,
    double? DiasSemChuvaP75,
    double? PrecipitacaoMedianaMm,
    double? FrpMedianoMw,
    AmostrasValidas Amostras);

/// <summary>
/// Quantos focos tinham valor utilizável em cada campo.
///
/// Sem isto, uma mediana calculada sobre três focos pareceria tão sólida quanto uma
/// calculada sobre três mil.
/// </summary>
public sealed record AmostrasValidas(int RiscoFogo, int DiasSemChuva, int Precipitacao, int Frp);

/// <summary>
/// A tradução para linguagem de aula.
///
/// <b>O aplicativo trabalha com estas classes, não com os números.</b> Os números ficam no
/// pacote para quem quiser auditar, mas quem decide o que aparece na tela é a classe —
/// é o que impede um valor observado de virar coeficiente de simulação por descuido.
/// </summary>
public sealed record ClassesDidaticas(
    string Risco,
    string Secura,
    string Classificacao);
