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

using CaixaInterativa.Processing;

namespace CaixaInterativa.Simulation;

/// <summary>Em que ponto da atividade estamos.</summary>
public enum FaseDaAtividade
{
    /// <summary>Ainda não começou.</summary>
    NaoIniciada,

    /// <summary>Condições de A aplicadas; falta executar.</summary>
    PreparadaA,
    ExecutandoA,

    /// <summary>A terminou e o pico está congelado.</summary>
    ResultadoA,

    /// <summary>Condições de B aplicadas; falta executar.</summary>
    PreparadaB,
    ExecutandoB,

    /// <summary>Os dois picos estão congelados e a comparação pode ser lida.</summary>
    Concluida,

    /// <summary>Alguma invariante caiu. A comparação não pode ser apresentada.</summary>
    Invalidada,
}

/// <summary>Por que a atividade foi invalidada.</summary>
public enum MotivoDeInvalidacao
{
    Nenhum,
    RelevoMudou,
    SensorReiniciado,
}

/// <summary>
/// A primeira atividade pedagógica oficial: <b>Urbanização e Enchentes</b>.
///
/// <b>É um experimento controlado dentro de um modelo didático.</b> Uma variável muda — a
/// cobertura do solo. Todo o resto é congelado: o relevo que a turma moldou, a chuva, a
/// duração, o estado hídrico de partida, o passo de tempo e a sessão do sensor.
///
/// <b>Por que isto não é um framework de atividades.</b> Há exatamente uma atividade, e
/// ela tem um consumidor. Uma abstração para "qualquer atividade" precisaria adivinhar o
/// que a segunda vai precisar, e adivinhar errado custa mais que reescrever. Quando a
/// segunda existir, o que for comum entre as duas aparece sozinho.
///
/// <b>O que esta classe não faz.</b> Não toca na simulação, não lê sensor, não desenha.
/// Ela guarda o estado do experimento e responde se a comparação é apresentável. Quem
/// aplica cobertura e dispara chuva é a interface — e é por isso que esta classe pode ser
/// testada sem WPF e sem hardware.
/// </summary>
public sealed class AtividadeUrbanizacao
{
    // ───────────────── identidade e textos ─────────────────

    public const string Titulo = "Urbanização e Enchentes";

    public const string PerguntaInvestigativa =
        "Mantendo o mesmo relevo e a mesma chuva, o que muda no caminho da água quando " +
        "trocamos a cobertura do solo?";

    public const string Experimento =
        "Mesmo relevo. Mesma chuva. Coberturas diferentes.";

    public const string PerguntaDeDiscussao =
        "Que outros fatores também influenciam uma enchente numa cidade real?";

    /// <summary>O limite do modelo, dito onde quem dá aula lê antes de concluir.</summary>
    public const string LimiteDoModelo =
        "A caixa isola uma variável: a cobertura do solo. Uma cidade real envolve muitos " +
        "outros fatores ao mesmo tempo.";

    public const string RelevoNaoERepresentacao =
        "O relevo foi construído pelos alunos. Ele não representa uma cidade real específica.";

    public const string InstrucaoPassoA =
        "Observe como a água se comporta nesta cobertura.";

    public const string InstrucaoPassoB =
        "Mesma chuva. Mesmo relevo. Outra cobertura.";

    public const string AvisoRelevoMudou =
        "O relevo mudou entre as execuções. Para manter a comparação justa, restaure o " +
        "relevo ou recomece a atividade.";

    public const string AvisoSensorReiniciado =
        "O sensor foi reiniciado. Para manter a comparação justa, comece a atividade " +
        "novamente.";

    // ───────────────── as condições oficiais ─────────────────

    /// <summary>Cobertura do passo A.</summary>
    public const TipoDeSolo CoberturaA = TipoDeSolo.Mata;

    /// <summary>Cobertura do passo B.</summary>
    public const TipoDeSolo CoberturaB = TipoDeSolo.Impermeavel;

    /// <summary>
    /// Duração oficial do episódio, em segundos.
    ///
    /// Vinte segundos foi o que a investigação física usou, e está dentro da faixa que o
    /// controle de duração já aceitava (4 a 30). O número não foi escolhido para produzir
    /// diferença: é longo o suficiente para a água chegar ao fundo do vale e curto o
    /// suficiente para caber duas vezes numa aula.
    /// </summary>
    public const float DuracaoSegundos = 20f;

    /// <summary>
    /// Passo de tempo das execuções, em segundos.
    ///
    /// Trinta por segundo é a taxa do Kinect v1. Fixá-lo não muda a física: muda o fato de
    /// A e B receberem a mesma sequência de passos, sem o que a diferença entre as duas
    /// deixa de ser atribuível à cobertura.
    /// </summary>
    public const float PassoSegundos = 1f / 30f;

    /// <summary>
    /// A chuva oficial da atividade, lida da tabela única de presets.
    ///
    /// Não é um número escrito aqui: é o mesmo item que o professor veria no controle de
    /// intensidade. A atividade escolhe qual preset usa; quanto ele chove é assunto da
    /// tabela, e há uma só.
    /// </summary>
    public static IntensidadeDeChuva ChuvaOficial =>
        IntensidadesDeChuva.De(IntensidadesDeChuva.IndicePadrao);

    /// <summary>
    /// Intensidade da chuva em mm/s, congelada quando A começa e reutilizada em B sem
    /// passar pela interface, para que ninguém possa trocá-la no meio do experimento.
    /// </summary>
    public float IntensidadeMmPorSegundo { get; private set; }

    // ───────────────── estado ─────────────────

    public FaseDaAtividade Fase { get; private set; } = FaseDaAtividade.NaoIniciada;
    public MotivoDeInvalidacao Motivo { get; private set; } = MotivoDeInvalidacao.Nenhum;

    /// <summary>Pico da área alagada em A, congelado. Nulo até A terminar.</summary>
    public double? PicoA { get; private set; }

    /// <summary>Pico da área alagada em B, congelado. Nulo até B terminar.</summary>
    public double? PicoB { get; private set; }

    /// <summary>O relevo de referência, tirado antes de A e comparado antes de B.</summary>
    public AssinaturaDoRelevo? RelevoDeReferencia { get; private set; }

    /// <summary>A sessão da fonte em que a atividade começou.</summary>
    public int SessaoDaFonte { get; private set; }

    /// <summary>A comparação só pode ser apresentada quando as duas execuções fecharam.</summary>
    public bool ComparacaoDisponivel => Fase == FaseDaAtividade.Concluida
                                        && PicoA is not null && PicoB is not null;

    /// <summary>Verdadeiro enquanto uma das duas execuções está em curso.</summary>
    public bool Executando => Fase is FaseDaAtividade.ExecutandoA or FaseDaAtividade.ExecutandoB;

    /// <summary>Verdadeiro enquanto a atividade ocupa a caixa.</summary>
    public bool EmAndamento => Fase is not (FaseDaAtividade.NaoIniciada or FaseDaAtividade.Invalidada);

    /// <summary>A cobertura que o passo atual usa. Nula fora dos passos.</summary>
    public TipoDeSolo? CoberturaDoPassoAtual => Fase switch
    {
        FaseDaAtividade.PreparadaA or FaseDaAtividade.ExecutandoA or FaseDaAtividade.ResultadoA
            => CoberturaA,
        FaseDaAtividade.PreparadaB or FaseDaAtividade.ExecutandoB or FaseDaAtividade.Concluida
            => CoberturaB,
        _ => null,
    };

    // ───────────────── transições ─────────────────

    /// <summary>
    /// Começa a atividade e congela as condições oficiais.
    ///
    /// A intensidade vem de fora porque a fonte de verdade dela é o controle de
    /// intensidade que já existe — duplicar o número aqui criaria duas verdades.
    /// </summary>
    public void Iniciar(float intensidadeMmPorSegundo, AssinaturaDoRelevo? relevo, int sessao)
    {
        IntensidadeMmPorSegundo = intensidadeMmPorSegundo;
        RelevoDeReferencia = relevo;
        SessaoDaFonte = sessao;

        PicoA = null;
        PicoB = null;
        Motivo = MotivoDeInvalidacao.Nenhum;
        Fase = FaseDaAtividade.PreparadaA;
    }

    /// <summary>Marca o início da execução do passo atual.</summary>
    public void MarcarExecucaoIniciada()
    {
        Fase = Fase switch
        {
            FaseDaAtividade.PreparadaA => FaseDaAtividade.ExecutandoA,
            FaseDaAtividade.PreparadaB => FaseDaAtividade.ExecutandoB,
            _ => Fase,
        };
    }

    /// <summary>
    /// Congela o pico do passo que acabou.
    ///
    /// <b>Não sobrescreve.</b> Um resultado já congelado não muda porque alguém disparou
    /// outra chuva — era exatamente assim que o histórico antigo trocava o valor de Mata
    /// de 47% para 53% sem avisar, e mudava a conclusão junto.
    /// </summary>
    public void RegistrarResultado(double picoAlagamentoPercent)
    {
        if (Fase == FaseDaAtividade.ExecutandoA && PicoA is null)
        {
            PicoA = picoAlagamentoPercent;
            Fase = FaseDaAtividade.ResultadoA;
        }
        else if (Fase == FaseDaAtividade.ExecutandoB && PicoB is null)
        {
            PicoB = picoAlagamentoPercent;
            Fase = FaseDaAtividade.Concluida;
        }
    }

    /// <summary>
    /// Verifica as invariantes antes de liberar o passo B.
    ///
    /// Prefere segurança a conveniência: qualquer dúvida invalida em vez de comparar.
    /// </summary>
    public bool PodePrepararB(AssinaturaDoRelevo? relevoAtual, int sessaoAtual)
    {
        if (Fase != FaseDaAtividade.ResultadoA) return false;

        if (sessaoAtual != SessaoDaFonte)
        {
            Invalidar(MotivoDeInvalidacao.SensorReiniciado);
            return false;
        }

        // Sem assinatura de um dos lados não dá para afirmar que o relevo ficou igual, e
        // afirmar sem verificar é o erro que esta atividade existe para não cometer.
        if (RelevoDeReferencia is null || relevoAtual is null
            || !RelevoDeReferencia.Comparar(relevoAtual).MesmoRelevo)
        {
            Invalidar(MotivoDeInvalidacao.RelevoMudou);
            return false;
        }

        Fase = FaseDaAtividade.PreparadaB;
        return true;
    }

    /// <summary>Verifica se a sessão continua a mesma, a qualquer momento.</summary>
    public void VerificarSessao(int sessaoAtual)
    {
        if (EmAndamento && sessaoAtual != SessaoDaFonte)
            Invalidar(MotivoDeInvalidacao.SensorReiniciado);
    }

    public void Invalidar(MotivoDeInvalidacao motivo)
    {
        Motivo = motivo;
        Fase = FaseDaAtividade.Invalidada;
    }

    public void Encerrar()
    {
        Fase = FaseDaAtividade.NaoIniciada;
        Motivo = MotivoDeInvalidacao.Nenhum;
        PicoA = null;
        PicoB = null;
        RelevoDeReferencia = null;
    }

    // ───────────────── leitura do resultado ─────────────────

    public static string NomeDaCobertura(TipoDeSolo tipo) => PropriedadesDoSolo.De(tipo).Nome;

    /// <summary>
    /// A frase de observação, construída a partir do que foi medido.
    ///
    /// <b>Descreve o modelo, não o mundo.</b> "Neste modelo" não é ressalva decorativa: é
    /// a diferença entre relatar uma medição e afirmar uma causa. A caixa não prova que
    /// urbanizar causa enchente — ela mostra o que este modelo faz quando só a cobertura
    /// muda, e a ponte para a cidade real é conversa de aula, não conclusão do software.
    /// </summary>
    public string Observacao()
    {
        if (!ComparacaoDisponivel) return "";

        double a = PicoA!.Value, b = PicoB!.Value;
        string na = NomeDaCobertura(CoberturaA), nb = NomeDaCobertura(CoberturaB);

        // "Semelhante" existe porque o modelo pode não separar as duas coberturas neste
        // relevo. Forçar um vencedor onde a medição não distingue seria inventar resultado.
        string relacao = Math.Abs(a - b) < 1.0
            ? $"as duas coberturas apresentaram área alagada semelhante"
            : b > a
                ? $"a cobertura {nb} apresentou área alagada maior que a {na}"
                : $"a cobertura {nb} apresentou área alagada menor que a {na}";

        return $"Neste modelo, mantendo o relevo e a chuva constantes, {relacao}.";
    }

    /// <summary>Diferença em pontos percentuais de área, de A para B.</summary>
    public double? DiferencaEmPontos =>
        ComparacaoDisponivel ? PicoB!.Value - PicoA!.Value : null;
}
