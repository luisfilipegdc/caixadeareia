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
using CaixaInterativa.Simulation;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// O que acontece quando a atividade é interrompida, abandonada ou usada errado.
///
/// <b>A regra que todos estes testes verificam é uma só:</b> nunca pode sobrar uma
/// comparação de aparência válida depois de uma invariante quebrada. Prefere-se recusar a
/// comparação a apresentá-la com uma ressalva que ninguém lê.
///
/// O que é bloqueio de interface — desabilitar cobertura, intensidade e duração enquanto
/// a atividade corre — não cabe aqui: depende de WPF e foi verificado em tela. O que cabe
/// aqui é a máquina de estados, que é quem decide se a comparação existe.
/// </summary>
public class InterrupcoesDaAtividadeTests
{
    private const int W = 64, H = 48;
    private const int Sessao = 11;

    private static float[] Relevo(float escala = 1f)
    {
        var t = new float[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                t[y * W + x] = escala * 70f * ((y - H / 2f) * (y - H / 2f) / (H * H / 4f));
        return t;
    }

    private static AssinaturaDoRelevo Assinatura(float escala = 1f) =>
        AssinaturaDoRelevo.De(Relevo(escala), W, H)!;

    private static AtividadeUrbanizacao Iniciada(int sessao = Sessao)
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(AtividadeUrbanizacao.ChuvaOficial.MmPorSegundo, Assinatura(), sessao);
        return a;
    }

    private static AtividadeUrbanizacao ComResultadoA(double pico = 50)
    {
        var a = Iniciada();
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(pico);
        return a;
    }

    private static AtividadeUrbanizacao Concluida(double a1 = 50, double b1 = 60)
    {
        var a = ComResultadoA(a1);
        Assert.True(a.PodePrepararB(Assinatura(), Sessao));
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(b1);
        return a;
    }

    // ───────────────── abandonar e recomeçar ─────────────────

    /// <summary>
    /// Fechar a atividade no meio devolve tudo ao início. Um passo A pendurado de uma
    /// atividade abandonada é a forma mais fácil de produzir uma comparação entre
    /// condições diferentes.
    /// </summary>
    [Fact]
    public void FecharNoMeioDescartaOResultadoParcial()
    {
        var a = ComResultadoA(47);
        a.Encerrar();

        Assert.Equal(FaseDaAtividade.NaoIniciada, a.Fase);
        Assert.Null(a.PicoA);
        Assert.False(a.EmAndamento);
        Assert.False(a.ComparacaoDisponivel);
    }

    /// <summary>Começar de novo depois de abandonar não herda nada da tentativa anterior.</summary>
    [Fact]
    public void NovaAtividadeDepoisDeAbandonarComecaLimpa()
    {
        var a = ComResultadoA(47);
        a.Encerrar();
        a.Iniciar(AtividadeUrbanizacao.ChuvaOficial.MmPorSegundo, Assinatura(), Sessao);

        Assert.Equal(FaseDaAtividade.PreparadaA, a.Fase);
        Assert.Null(a.PicoA);
        Assert.Null(a.PicoB);
        Assert.Equal(MotivoDeInvalidacao.Nenhum, a.Motivo);
    }

    /// <summary>Recomeçar depois de invalidada também limpa o resultado A antigo.</summary>
    [Fact]
    public void NovaAtividadeDepoisDeInvalidadaNaoHerdaOPicoAntigo()
    {
        var a = ComResultadoA(47);
        Assert.False(a.PodePrepararB(Assinatura(escala: 2f), Sessao));
        Assert.Equal(FaseDaAtividade.Invalidada, a.Fase);

        a.Iniciar(AtividadeUrbanizacao.ChuvaOficial.MmPorSegundo, Assinatura(), Sessao);

        Assert.Null(a.PicoA);
        Assert.Equal(FaseDaAtividade.PreparadaA, a.Fase);
    }

    // ───────────────── executar duas vezes ─────────────────

    /// <summary>Disparar a chuva de novo durante A não reabre o passo nem troca o número.</summary>
    [Fact]
    public void ExecutarADuasVezesNaoTrocaOResultado()
    {
        var a = Iniciada();
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(47);

        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(99);

        Assert.Equal(47, a.PicoA);
        Assert.Equal(FaseDaAtividade.ResultadoA, a.Fase);
    }

    [Fact]
    public void ExecutarBDuasVezesNaoTrocaOResultado()
    {
        var a = Concluida(47, 68);

        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(1);

        Assert.Equal(68, a.PicoB);
        Assert.Equal(FaseDaAtividade.Concluida, a.Fase);
    }

    /// <summary>Depois de concluída, nada mais registra — nem um A tardio.</summary>
    [Fact]
    public void DepoisDeConcluidaNenhumResultadoNovoEntra()
    {
        var a = Concluida(47, 68);

        a.RegistrarResultado(12);

        Assert.Equal(47, a.PicoA);
        Assert.Equal(68, a.PicoB);
    }

    // ───────────────── sessão e fonte ─────────────────

    /// <summary>
    /// Cada início de fonte cria uma <c>WaterSimulation</c> nova, com solo e saturação
    /// zerados — o número de A foi medido noutra caixa, na prática. A sessão é o que
    /// permite perceber isso sem guardar referência a objeto nenhum.
    /// </summary>
    [Fact]
    public void NovaSimulacaoDeAguaInvalidaAComparacao()
    {
        var a = ComResultadoA(52);

        // O que o motor faz ao reiniciar a fonte: nova simulação, sessão nova.
        a.VerificarSessao(Sessao + 1);

        Assert.Equal(FaseDaAtividade.Invalidada, a.Fase);
        Assert.Equal(MotivoDeInvalidacao.SensorReiniciado, a.Motivo);
        Assert.False(a.ComparacaoDisponivel);
    }

    [Fact]
    public void SessaoNovaBloqueiaOPassoBMesmoComRelevoIgual()
    {
        var a = ComResultadoA(52);

        Assert.False(a.PodePrepararB(Assinatura(), sessaoAtual: Sessao + 1));
        Assert.Equal(MotivoDeInvalidacao.SensorReiniciado, a.Motivo);
    }

    /// <summary>Uma atividade já concluída também é invalidada por troca de sessão.</summary>
    [Fact]
    public void TrocaDeSessaoDepoisDeConcluidaInvalidaAComparacao()
    {
        var a = Concluida();
        Assert.True(a.ComparacaoDisponivel);

        a.VerificarSessao(Sessao + 1);

        Assert.False(a.ComparacaoDisponivel);
        Assert.Equal(MotivoDeInvalidacao.SensorReiniciado, a.Motivo);
    }

    // ───────────────── relevo ─────────────────

    [Fact]
    public void RelevoAlteradoEntreAEBBloqueiaAComparacao()
    {
        var a = ComResultadoA(52);

        Assert.False(a.PodePrepararB(Assinatura(escala: 1.6f), Sessao));

        Assert.Equal(MotivoDeInvalidacao.RelevoMudou, a.Motivo);
        Assert.False(a.ComparacaoDisponivel);
    }

    /// <summary>
    /// Variação dentro da tolerância que já existia não bloqueia. Sem isto, o ruído do
    /// sensor tornaria a atividade impossível de terminar na caixa real.
    /// </summary>
    [Fact]
    public void VariacaoMinimaDoRelevoNaoBloqueia()
    {
        var a = ComResultadoA(52);

        var quaseIgual = Relevo();
        for (int i = 0; i < quaseIgual.Length; i++) quaseIgual[i] += 1f;   // 1 mm

        Assert.True(a.PodePrepararB(AssinaturaDoRelevo.De(quaseIgual, W, H), Sessao));
        Assert.Equal(FaseDaAtividade.PreparadaB, a.Fase);
    }

    // ───────────────── o que a atividade não usa ─────────────────

    /// <summary>
    /// Os cenários legados não participam da atividade. Eles pintam cobertura por cotas
    /// absolutas de altitude, que não foram validadas para relevo construído à mão.
    /// </summary>
    [Fact]
    public void AAtividadeNaoDependeDosCenariosLegados()
    {
        var tipo = typeof(AtividadeUrbanizacao);

        Assert.DoesNotContain(tipo.GetFields(), f => f.FieldType == typeof(Cenario));
        Assert.DoesNotContain(tipo.GetProperties(), p => p.PropertyType == typeof(Cenario));
        Assert.DoesNotContain(tipo.GetMethods(),
            m => m.ReturnType == typeof(Cenario)
                 || m.GetParameters().Any(p => p.ParameterType == typeof(Cenario)));
    }

    /// <summary>
    /// A comparação é feita sobre porcentagem de área, não sobre litros nem erosão. A
    /// máquina de estados só guarda dois números, e eles são picos.
    /// </summary>
    [Fact]
    public void AComparacaoGuardaSomenteOsDoisPicos()
    {
        var a = Concluida(47, 68);

        Assert.Equal(47, a.PicoA);
        Assert.Equal(68, a.PicoB);
        Assert.Equal(21, a.DiferencaEmPontos);

        // Nada de litros nem erosão na superfície pública da atividade.
        var nomes = typeof(AtividadeUrbanizacao).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain(nomes, n => n.Contains("litro") || n.Contains("erosao") || n.Contains("erosão"));
    }
}
