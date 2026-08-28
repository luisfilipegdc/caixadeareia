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
///
/// <b>Os quatro blocos.</b> A auditoria pedagógica trocou o parágrafo único por
/// PERGUNTA → OBSERVAÇÃO → HIPÓTESE → EXPERIMENTO. Antes, a pergunta hipotética vinha
/// grudada na pergunta sobre o dado, na mesma frase, e o salto de "o que foi medido lá
/// fora" para "o que a caixa faz" acontecia sem que ninguém percebesse a travessia. Os
/// blocos separados obrigam a travessia a ser consciente — que é justamente o ponto.
/// </summary>
public sealed record AtividadeConceitual(
    string Titulo,

    /// <summary>PERGUNTA — o que a turma vai investigar no dado observado.</summary>
    string Pergunta,

    /// <summary>OBSERVAÇÃO — o que olhar, e o que a tabela não responde.</summary>
    string Observacao,

    /// <summary>HIPÓTESE — a pergunta hipotética sobre o modelo. Nunca sobre o dado.</summary>
    string Hipotese,

    /// <summary>EXPERIMENTO — o que o professor faz na caixa, à mão.</summary>
    string Experimento,

    string DeOndeVemOContexto,
    string DeOndeVemORelevo,
    string DeOndeVemAPropagacao)
{
    /// <summary>
    /// O aviso que separa o território selecionado do relevo da areia.
    ///
    /// <b>É o mal-entendido mais provável desta tela inteira.</b> Um painel que diz
    /// "Cerrado · Goiás" ao lado de uma caixa de areia projetada convida à leitura de que
    /// a areia é Goiás. Nenhum texto dizia o contrário: o mais próximo era "o relevo é o
    /// que os estudantes moldaram", que descreve a origem sem negar a representação.
    /// </summary>
    public const string RelevoNaoRepresentaOTerritorio =
        "O relevo da caixa foi construído pelos alunos e não representa o território real " +
        "selecionado. O território aparece aqui só como contexto de leitura.";

    /// <summary>
    /// A primeira atividade desenhada sobre contexto externo.
    ///
    /// Escolhida porque as três peças já existem e nenhuma precisou mudar: a queimada está
    /// implementada, a água já barra o fogo, e o pacote do INPE traz o bioma.
    /// </summary>
    public static readonly AtividadeConceitual QueimadasNoCerrado = new(
        Titulo: "Queimadas no Cerrado",

        Pergunta:
            "O que os satélites registraram neste território, neste período?",

        Observacao:
            "Leiam os números do quadro acima: quantos focos de calor, quantos dias sem " +
            "chuva, quanto calor liberado. São medições de um território real.",

        Hipotese:
            "Agora uma pergunta sobre a caixa, não sobre o território: o que vocês acham " +
            "que aconteceria com o fogo se o relevo tivesse um vale no meio? E se houvesse " +
            "água atravessando?",

        Experimento:
            "Moldem o relevo na areia, escolham a cobertura do solo e a força do vento, e " +
            "toquem em iniciar. Quem escolhe as condições é a turma — nada aqui é ajustado " +
            "pelos dados do satélite.",

        DeOndeVemOContexto:
            "Focos de calor detectados por satélite, publicados pelo INPE. Dizem o que foi " +
            "medido no território real, no período indicado.",

        DeOndeVemORelevo:
            "O relevo é o que os estudantes moldaram na areia, lido pelo sensor. " +
            RelevoNaoRepresentaOTerritorio,

        DeOndeVemAPropagacao:
            "A propagação do fogo é uma simulação de sala de aula, calibrada para o " +
            "fenômeno aparecer numa aula. Não é previsão, e não reproduz os focos " +
            "observados.");

    /// <summary>
    /// A atividade que usa dois períodos do mesmo território.
    ///
    /// A hipótese é <b>sobre a caixa</b>, de propósito, e vive num bloco próprio. Ela
    /// convida a mexer numa condição e ver o que muda — mas quem escolhe a condição é o
    /// professor. O dado observado não configura nada: se ele escolhesse a cobertura ou a
    /// força do vento, a aula passaria a sugerir que a caixa está reproduzindo aquele
    /// período, e ela não está.
    /// </summary>
    public static readonly AtividadeConceitual MesmoTerritorioPeriodosDiferentes = new(
        Titulo: "O mesmo território em períodos diferentes",

        Pergunta:
            "O que mudou nas condições observadas entre estes dois períodos?",

        // Bloqueia a conclusão causal com uma pergunta, e não com uma regra de
        // epistemologia. "Correlação não implica causalidade" é verdade e não muda o que
        // a pessoa vai concluir; "o que mais mudou e não está aqui?" muda.
        Observacao:
            "Comparem os dois períodos lado a lado e listem o que mudou. Antes de dizer " +
            "por que mudou, vale a pergunta que a tabela não responde: o que mais pode ter " +
            "mudado entre junho e julho e não aparece nestes números?",

        Hipotese:
            "Esta pergunta é sobre a caixa, não sobre o território: mantendo o mesmo " +
            "relevo, o que vocês acham que aconteceria se mudássemos apenas uma condição " +
            "no modelo — a cobertura do solo, por exemplo?",

        Experimento:
            "Escolham essa condição à mão e executem. Depois mudem só ela e executem de " +
            "novo. Nenhum ajuste vem dos dados do satélite: a caixa não está reproduzindo " +
            "nenhum dos dois períodos.",

        DeOndeVemOContexto:
            "Os dois períodos vêm do mesmo conjunto do INPE, com a mesma agregação. A " +
            "comparação descreve o que foi medido em cada um — e só isso. Um período com " +
            "mais dias sem chuva e também mais focos são duas observações, não uma " +
            "relação de causa.",

        DeOndeVemORelevo:
            "O relevo é o que os estudantes moldaram, e não muda entre os dois períodos — " +
            "é justamente o que permite investigar uma condição de cada vez. " +
            RelevoNaoRepresentaOTerritorio,

        DeOndeVemAPropagacao:
            "A caixa não reproduz nenhum dos dois períodos. Ela mostra o que o modelo faz " +
            "com a condição que o professor escolher.");
}
