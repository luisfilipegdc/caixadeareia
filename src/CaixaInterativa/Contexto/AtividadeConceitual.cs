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

namespace CaixaInterativa.Contexto;

/// <summary>
/// A descrição de uma atividade que usa contexto externo — <b>e só a descrição</b>.
///
/// Ela não configura nada, não escolhe cobertura, não mexe em vento nem em chuva. Existe
/// para deixar explícito, na tela, de onde vem cada parte do que a turma está vendo:
/// o que foi observado por satélite, o que foi construído com as mãos, e o que é modelo
/// de sala de aula.
///
/// <b>Por que não automatizar os parâmetros.</b> Ligar "risco alto observado" a um número
/// dentro do solver criaria a impressão de que a caixa reproduz aquele risco — e ela não
/// reproduz. O professor lê o contexto, decide o que configurar, e a responsabilidade
/// pedagógica continua sendo dele. Automatizar isso é uma decisão que precisa ser tomada
/// de propósito, não como efeito colateral de uma integração de dados.
/// </summary>
public sealed record AtividadeConceitual(
    string Titulo,
    string PerguntaInvestigativa,
    string DeOndeVemOContexto,
    string DeOndeVemORelevo,
    string DeOndeVemAPropagacao)
{
    /// <summary>
    /// A primeira atividade desenhada sobre contexto externo.
    ///
    /// Escolhida porque as três peças já existem e nenhuma precisou mudar: a queimada está
    /// implementada, a água já barra o fogo, e o pacote do INPE traz o bioma.
    /// </summary>
    public static readonly AtividadeConceitual QueimadasNoCerrado = new(
        Titulo: "Queimadas no Cerrado",

        PerguntaInvestigativa:
            "Como o relevo, a cobertura do solo e as barreiras de água podem alterar a " +
            "propagação do fogo num cenário de risco elevado?",

        DeOndeVemOContexto:
            "Dado externo observado: focos de calor detectados por satélite, publicados " +
            "pelo INPE. Diz o que foi medido no território real, no período indicado.",

        DeOndeVemORelevo:
            "Medição da caixa: o relevo é o que os estudantes moldaram na areia, lido pelo " +
            "sensor de profundidade.",

        DeOndeVemAPropagacao:
            "Modelo didático: a propagação do fogo é uma simulação de sala de aula, " +
            "calibrada para o fenômeno aparecer numa aula. Não é previsão, e não reproduz " +
            "os focos observados.");
}
