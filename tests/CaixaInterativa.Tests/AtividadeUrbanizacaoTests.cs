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
/// As invariantes da primeira atividade oficial.
///
/// Cada uma existe por um caminho concreto pelo qual a comparação poderia mentir. Não são
/// testes de "a classe funciona": são testes de "a conclusão não pode ser produzida em
/// condições que não a sustentam".
/// </summary>
public class AtividadeUrbanizacaoTests
{
    private const int W = 64, H = 48;
    private const int Sessao = 7;

    private static float[] Relevo(float escala = 1f)
    {
        var t = new float[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                t[y * W + x] = escala * 80f * ((x - W / 2f) * (x - W / 2f) / (W * W / 4f));
        return t;
    }

    private static AssinaturaDoRelevo Assinatura(float escala = 1f) =>
        AssinaturaDoRelevo.De(Relevo(escala), W, H)!;

    /// <summary>Leva a atividade até o fim, do jeito certo.</summary>
    private static AtividadeUrbanizacao Concluida(double picoA = 47, double picoB = 68)
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(picoA);
        Assert.True(a.PodePrepararB(Assinatura(), Sessao));
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(picoB);
        return a;
    }

    // ───────────────── condições oficiais ─────────────────

    [Fact]
    public void AsCoberturasOficiaisSaoMataEAreaUrbana()
    {
        Assert.Equal(TipoDeSolo.Mata, AtividadeUrbanizacao.CoberturaA);
        Assert.Equal(TipoDeSolo.Impermeavel, AtividadeUrbanizacao.CoberturaB);

        Assert.Equal("Mata", AtividadeUrbanizacao.NomeDaCobertura(AtividadeUrbanizacao.CoberturaA));
        Assert.Equal("Área urbana", AtividadeUrbanizacao.NomeDaCobertura(AtividadeUrbanizacao.CoberturaB));
    }

    /// <summary>
    /// A duração precisa caber no controle que já existe (4 a 30 s) — se sair da faixa, a
    /// atividade pediria algo que a interface não sabe representar.
    /// </summary>
    [Fact]
    public void ADuracaoOficialCabeNaFaixaDoControle()
    {
        Assert.InRange(AtividadeUrbanizacao.DuracaoSegundos, 4f, 30f);
    }

    /// <summary>
    /// A intensidade é congelada em <c>Iniciar</c> e não muda depois. É assim que B não
    /// pode receber outra chuva sem que a atividade seja recomeçada.
    /// </summary>
    [Fact]
    public void AIntensidadeECongeladaNoInicioEReutilizadaEmB()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);
        Assert.Equal(8f, a.IntensidadeMmPorSegundo);

        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(47);
        a.PodePrepararB(Assinatura(), Sessao);

        Assert.Equal(8f, a.IntensidadeMmPorSegundo);
    }

    [Fact]
    public void OPassoOficialEODaTaxaDoSensor()
    {
        Assert.Equal(1f / 30f, AtividadeUrbanizacao.PassoSegundos);
    }

    // ───────────────── a cobertura de cada passo ─────────────────

    [Fact]
    public void OPassoAAplicaMata()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);

        Assert.Equal(TipoDeSolo.Mata, a.CoberturaDoPassoAtual);
    }

    [Fact]
    public void OPassoBAplicaAreaUrbana()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(47);
        a.PodePrepararB(Assinatura(), Sessao);

        Assert.Equal(TipoDeSolo.Impermeavel, a.CoberturaDoPassoAtual);
    }

    // ───────────────── resultados não são sobrescritos ─────────────────

    /// <summary>
    /// O histórico antigo trocava o valor de Mata de 47% para 53% quando alguém rodava de
    /// novo, e a conclusão mudava junto sem aviso. Aqui o primeiro resultado é final.
    /// </summary>
    [Fact]
    public void OResultadoANaoESobrescrito()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(47);

        a.MarcarExecucaoIniciada();     // não volta para ExecutandoA
        a.RegistrarResultado(53);

        Assert.Equal(47, a.PicoA);
    }

    [Fact]
    public void OResultadoBNaoESobrescrito()
    {
        var a = Concluida(picoA: 47, picoB: 68);

        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(99);

        Assert.Equal(68, a.PicoB);
    }

    // ───────────────── invariantes que bloqueiam ─────────────────

    /// <summary>Relevo alterado além da tolerância existente: não compara.</summary>
    [Fact]
    public void RelevoAlteradoInvalidaAAtividade()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(47);

        Assert.False(a.PodePrepararB(Assinatura(escala: 1.5f), Sessao));

        Assert.Equal(FaseDaAtividade.Invalidada, a.Fase);
        Assert.Equal(MotivoDeInvalidacao.RelevoMudou, a.Motivo);
        Assert.False(a.ComparacaoDisponivel);
    }

    /// <summary>Sem assinatura não dá para afirmar que o relevo ficou igual.</summary>
    [Fact]
    public void SemAssinaturaDeRelevoNaoCompara()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, relevo: null, Sessao);
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(47);

        Assert.False(a.PodePrepararB(Assinatura(), Sessao));
        Assert.Equal(MotivoDeInvalidacao.RelevoMudou, a.Motivo);
    }

    /// <summary>
    /// Reinício da fonte invalida. Cada <c>StartSource</c> cria uma simulação nova, com
    /// solo e saturação zerados — comparar através dessa fronteira é comparar duas caixas.
    /// </summary>
    [Fact]
    public void ReinicioDaFonteInvalidaAAtividade()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(47);

        Assert.False(a.PodePrepararB(Assinatura(), sessaoAtual: Sessao + 1));

        Assert.Equal(FaseDaAtividade.Invalidada, a.Fase);
        Assert.Equal(MotivoDeInvalidacao.SensorReiniciado, a.Motivo);
    }

    /// <summary>A troca de sessão é notada mesmo fora do momento de preparar B.</summary>
    [Fact]
    public void AVerificacaoDeSessaoPegaOReinicioNoMeioDeA()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);
        a.MarcarExecucaoIniciada();

        a.VerificarSessao(Sessao + 1);

        Assert.Equal(FaseDaAtividade.Invalidada, a.Fase);
        Assert.Equal(MotivoDeInvalidacao.SensorReiniciado, a.Motivo);
    }

    [Fact]
    public void AtividadeNaoIniciadaNaoEAfetadaPelaTrocaDeSessao()
    {
        var a = new AtividadeUrbanizacao();
        a.VerificarSessao(Sessao + 99);

        Assert.Equal(FaseDaAtividade.NaoIniciada, a.Fase);
    }

    [Fact]
    public void NaoDaParaPularDeANaoExecutadoParaB()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);

        Assert.False(a.PodePrepararB(Assinatura(), Sessao));
        Assert.Equal(FaseDaAtividade.PreparadaA, a.Fase);
    }

    // ───────────────── a comparação ─────────────────

    [Fact]
    public void AComparacaoSoFicaDisponivelComOsDoisPicos()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);
        Assert.False(a.ComparacaoDisponivel);

        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(47);
        Assert.False(a.ComparacaoDisponivel);

        a.PodePrepararB(Assinatura(), Sessao);
        a.MarcarExecucaoIniciada();
        a.RegistrarResultado(68);
        Assert.True(a.ComparacaoDisponivel);
    }

    [Fact]
    public void ADiferencaEEmPontosDeAreaAlagada()
    {
        Assert.Equal(21, Concluida(47, 68).DiferencaEmPontos);
    }

    [Theory]
    [InlineData(47, 68, "maior")]
    [InlineData(68, 47, "menor")]
    [InlineData(47, 47.5, "semelhante")]
    public void AObservacaoDescreveOQueFoiMedido(double a, double b, string esperado)
    {
        Assert.Contains(esperado, Concluida(a, b).Observacao());
    }

    /// <summary>
    /// A observação é sobre o modelo, e diz isso. Sem esse enquadramento, a frase vira
    /// afirmação sobre cidades reais — que é o que a caixa não pode sustentar.
    /// </summary>
    [Fact]
    public void AObservacaoSeDeclaraSobreOModelo()
    {
        string texto = Concluida().Observacao();

        Assert.Contains("Neste modelo", texto);
        Assert.Contains("mantendo o relevo e a chuva constantes", texto);
    }

    [Fact]
    public void SemComparacaoNaoHaObservacao()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);

        Assert.Equal("", a.Observacao());
        Assert.Null(a.DiferencaEmPontos);
    }

    // ───────────────── o que a atividade não usa ─────────────────

    /// <summary>
    /// A métrica oficial é o pico da área alagada. Litros dependem da largura física
    /// ainda não medida; erosão daria a Área urbana a mesma resistência da rocha; o
    /// contexto do INPE fala de queimada, não de enchente.
    /// </summary>
    [Fact]
    public void NenhumTextoDaAtividadeMencionaMetricaProibida()
    {
        string[] proibidos = ["litro", "erosão", "erosao", "INPE", "foco de calor", "risco de fogo"];

        foreach (string t in TodosOsTextos())
            foreach (string p in proibidos)
                Assert.DoesNotContain(p, t, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nenhum texto pode afirmar causa sobre o mundo real. A caixa mede um modelo; a
    /// ponte para a cidade é discussão de aula, e a discussão é uma pergunta, não uma
    /// conclusão.
    /// </summary>
    [Fact]
    public void NenhumTextoDaAtividadeAfirmaCausa()
    {
        string[] proibidos =
        [
            "causou", "provocou", "impediu", "isso prova", "isso demonstra",
            "prevê", "representa uma cidade real", "a diferença veio da cobertura",
        ];

        foreach (string t in TodosOsTextos())
            foreach (string p in proibidos)
                Assert.DoesNotContain(p, t, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OsTextosDizemOsLimitesDoModelo()
    {
        Assert.Contains("isola uma variável", AtividadeUrbanizacao.LimiteDoModelo);
        Assert.Contains("não representa uma cidade real", AtividadeUrbanizacao.RelevoNaoERepresentacao);
    }

    private static IEnumerable<string> TodosOsTextos()
    {
        yield return AtividadeUrbanizacao.Titulo;
        yield return AtividadeUrbanizacao.PerguntaInvestigativa;
        yield return AtividadeUrbanizacao.Experimento;
        yield return AtividadeUrbanizacao.PerguntaDeDiscussao;
        yield return AtividadeUrbanizacao.InstrucaoPassoA;
        yield return AtividadeUrbanizacao.InstrucaoPassoB;
        yield return AtividadeUrbanizacao.AvisoRelevoMudou;
        yield return AtividadeUrbanizacao.AvisoSensorReiniciado;
        yield return Concluida(47, 68).Observacao();
        yield return Concluida(68, 47).Observacao();
        yield return Concluida(47, 47.2).Observacao();
    }

    // ───────────────── recomeçar ─────────────────

    [Fact]
    public void EncerrarDevolveAAtividadeAoInicio()
    {
        var a = Concluida();
        a.Encerrar();

        Assert.Equal(FaseDaAtividade.NaoIniciada, a.Fase);
        Assert.Null(a.PicoA);
        Assert.Null(a.PicoB);
        Assert.False(a.ComparacaoDisponivel);
        Assert.False(a.EmAndamento);
    }

    [Fact]
    public void ReiniciarDepoisDeInvalidadaLimpaOMotivo()
    {
        var a = new AtividadeUrbanizacao();
        a.Iniciar(8f, Assinatura(), Sessao);
        a.Invalidar(MotivoDeInvalidacao.RelevoMudou);

        a.Iniciar(8f, Assinatura(), Sessao);

        Assert.Equal(FaseDaAtividade.PreparadaA, a.Fase);
        Assert.Equal(MotivoDeInvalidacao.Nenhum, a.Motivo);
    }
}
