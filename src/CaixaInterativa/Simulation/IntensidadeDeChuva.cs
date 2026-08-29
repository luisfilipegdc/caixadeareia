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

namespace CaixaInterativa.Simulation;

/// <summary>Uma chuva que o professor pode escolher, com o nome que aparece na tela.</summary>
public readonly record struct IntensidadeDeChuva(string Nome, float MmPorSegundo);

/// <summary>
/// As chuvas disponíveis, num lugar só.
///
/// <b>Por que isto foi extraído.</b> Os mesmos três presets viviam em três lugares: os
/// nomes no preenchimento do combo, os milímetros por segundo num <c>switch</c>, e o
/// índice da chuva oficial da atividade numa constante à parte. Três verdades sobre a
/// mesma coisa é uma a mais do que cabe — e a atividade oficial depende de que a chuva de
/// A seja exatamente a de B, o que um número duplicado não garante.
///
/// Os valores não mudaram: 3, 8 e 18 mm/s são os mesmos de antes. São <b>didáticos</b>,
/// escolhidos para o fenômeno aparecer numa aula, não medidos em campo.
/// </summary>
public static class IntensidadesDeChuva
{
    public static readonly IntensidadeDeChuva[] Todas =
    [
        new("Garoa", 3f),
        new("Chuva forte", 8f),
        new("Tempestade", 18f),
    ];

    /// <summary>A chuva que o programa abre selecionada, e que a atividade oficial usa.</summary>
    public const int IndicePadrao = 1;

    /// <summary>Índice fora da faixa devolve o mais próximo, em vez de estourar.</summary>
    public static IntensidadeDeChuva De(int indice) =>
        Todas[Math.Clamp(indice, 0, Todas.Length - 1)];
}
