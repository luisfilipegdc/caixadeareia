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
/// Uma linha do CSV de focos de calor do INPE, com só o que interessa à agregação.
///
/// Os campos numéricos são <c>double?</c> porque o INPE usa <b>-999 como sentinela de
/// dado inválido</b> — confirmado no FAQ do Programa Queimadas: acontece em área urbana
/// e corpo d'água, "onde não faz sentido calcular o Risco de Fogo". O parser converte
/// essa sentinela em <c>null</c> na entrada, para que ela nunca chegue a uma estatística.
///
/// Isso não é detalhe: numa amostra de 28.519 focos de um único dia, a média de
/// <c>risco_fogo</c> deu <b>-2,06</b> por causa dos -999. Um número entre 0 e 1 que sai
/// negativo é o tipo de erro que passa despercebido num relatório.
/// </summary>
public sealed record FocoDeCalor(
    DateTime DataHoraGmt,
    string Municipio,
    string Estado,
    string Bioma,
    double? DiasSemChuva,
    double? PrecipitacaoMm,
    double? RiscoFogo,
    double? FrpMw)
{
    /// <summary>Valor que o INPE usa para "não se aplica / não calculado".</summary>
    public const double Sentinela = -999d;
}
