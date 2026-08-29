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

using CaixaInterativa.Simulation;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// O controle e o experimento da atividade, rodados com as condições oficiais.
///
/// <b>O que estes testes provam, e o que não provam.</b> Eles provam que, dadas as mesmas
/// entradas discretas ao solver — mesmo relevo, mesma chuva, mesma duração, mesmo passo,
/// mesmo estado inicial —, o resultado é reproduzível, e que trocar só a cobertura produz
/// um resultado diferente e comparável.
///
/// Eles <b>não</b> substituem a validação com o Kinect. Aqui o relevo é um array constante;
/// na caixa ele vem de um sensor com ruído, sobre areia real, a 25–30 quadros por segundo.
/// A pergunta que só o hardware responde é quanto desse ruído sobra no resultado — e ela
/// continua aberta. Estes testes fecham a parte que é software.
///
/// Nenhum valor histórico virou referência: não há número esperado codificado aqui.
/// </summary>
public class ControleDaAtividadeTests
{
    private const int W = 64, H = 48;

    /// <summary>
    /// Uma bacia sintética fixa. Não representa terreno nenhum — existe para ser
    /// exatamente igual em todas as execuções, que é o que um controle precisa.
    /// </summary>
    private static float[] TerrenoDeReferencia()
    {
        var t = new float[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = (x - W / 2f) / (W / 2f);
                float dy = (y - H / 2f) / (H / 2f);
                t[y * W + x] = 80f * (dx * dx + dy * dy) + 6f * MathF.Sin(x * 0.9f);
            }
        return t;
    }

    /// <summary>
    /// Uma execução sob o protocolo oficial: prepara, aplica a cobertura, chove com a
    /// intensidade e a duração da atividade, e avança com o passo fixo dela.
    ///
    /// É deliberadamente a mesma sequência que a interface executa — se ela divergir, os
    /// números daqui deixam de dizer alguma coisa sobre o produto.
    /// </summary>
    private static double ExecucaoOficial(WaterSimulation agua, float[] terreno, TipoDeSolo cobertura)
    {
        agua.PrepararExecucaoControlada();
        agua.Solo.Preencher(cobertura);
        agua.Ativo = true;
        agua.IniciarChuva(AtividadeUrbanizacao.ChuvaOficial.MmPorSegundo,
                          AtividadeUrbanizacao.DuracaoSegundos);

        float passo = AtividadeUrbanizacao.PassoSegundos;
        int quadros = (int)((AtividadeUrbanizacao.DuracaoSegundos + 2f) / passo);
        for (int i = 0; i < quadros; i++) agua.Atualizar(terreno, W, H, passo);

        return agua.PicoAlagamentoPercent;
    }

    // ───────────────────────── controle: Mata → Mata ─────────────────────────

    /// <summary>
    /// <b>O controle.</b> Nada muda entre as duas execuções. Se o resultado mudar, a
    /// diferença que o experimento medir depois não pode ser atribuída à cobertura.
    ///
    /// A comparação é por igualdade, e não por tolerância: com passo fixo e estado
    /// preparado, o solver é determinístico. Uma tolerância aqui esconderia justamente o
    /// que este teste existe para detectar.
    /// </summary>
    [Fact]
    public void ControleMataMataRepeteOMesmoPico()
    {
        var terreno = TerrenoDeReferencia();
        var agua = new WaterSimulation(W, H);

        double primeira = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);
        double segunda = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);

        Assert.True(primeira > 0, "O terreno de referência nem alagou; o controle não diz nada.");
        Assert.Equal(primeira, segunda, precision: 6);
    }

    /// <summary>Três execuções, para o caso de a segunda coincidir por acaso.</summary>
    [Fact]
    public void ControleSeMantemAoLongoDeTresExecucoes()
    {
        var terreno = TerrenoDeReferencia();
        var agua = new WaterSimulation(W, H);

        double a = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);
        double b = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);
        double c = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);

        Assert.Equal(a, b, precision: 6);
        Assert.Equal(a, c, precision: 6);
    }

    /// <summary>
    /// O controle vale mesmo depois de a instância ter passado pela outra cobertura — que
    /// é a ordem real da aula: Mata, Área urbana, e talvez Mata de novo para conferir.
    /// </summary>
    [Fact]
    public void ControleSobreviveAUmaExecucaoIntercaladaDeOutraCobertura()
    {
        var terreno = TerrenoDeReferencia();
        var agua = new WaterSimulation(W, H);

        double antes = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);
        ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaB);
        double depois = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);

        Assert.Equal(antes, depois, precision: 6);
    }

    // ───────────────────── experimento: Mata → Área urbana ─────────────────────

    /// <summary>
    /// <b>O experimento.</b> Só a cobertura muda. O teste não afirma qual das duas alaga
    /// mais nem quanto: afirma que o resultado é diferente e reprodutível, que é o que
    /// torna a comparação apresentável a uma turma.
    ///
    /// Se algum dia o modelo passar a não separar as duas coberturas neste terreno, este
    /// teste falha — e a atividade precisa saber disso antes da aula, não durante.
    /// </summary>
    [Fact]
    public void TrocarSomenteACoberturaMudaOResultado()
    {
        var terreno = TerrenoDeReferencia();

        double a = ExecucaoOficial(new WaterSimulation(W, H), terreno, AtividadeUrbanizacao.CoberturaA);
        double b = ExecucaoOficial(new WaterSimulation(W, H), terreno, AtividadeUrbanizacao.CoberturaB);

        Assert.NotEqual(a, b, precision: 2);
    }

    /// <summary>
    /// O efeito da troca de cobertura precisa ser maior que a variação da repetição — é a
    /// condição para a comparação significar alguma coisa. Com o controle em zero, basta
    /// o efeito não ser zero; o teste afirma a relação, não um valor.
    /// </summary>
    [Fact]
    public void OEfeitoDaCoberturaSuperaAVariacaoDoControle()
    {
        var terreno = TerrenoDeReferencia();
        var agua = new WaterSimulation(W, H);

        double controle1 = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);
        double controle2 = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);
        double variacaoDoControle = Math.Abs(controle1 - controle2);

        double experimento = ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaB);
        double efeito = Math.Abs(controle1 - experimento);

        Assert.True(efeito > variacaoDoControle,
                    $"Efeito da cobertura {efeito:F3} pontos contra variação do controle " +
                    $"{variacaoDoControle:F3}. A comparação da atividade não se sustenta.");
    }

    /// <summary>A ordem em que as coberturas rodam não muda o resultado de cada uma.</summary>
    [Fact]
    public void AOrdemDasCoberturasNaoMudaOResultado()
    {
        var terreno = TerrenoDeReferencia();

        var direta = new WaterSimulation(W, H);
        double a1 = ExecucaoOficial(direta, terreno, AtividadeUrbanizacao.CoberturaA);
        double b1 = ExecucaoOficial(direta, terreno, AtividadeUrbanizacao.CoberturaB);

        var inversa = new WaterSimulation(W, H);
        double b2 = ExecucaoOficial(inversa, terreno, AtividadeUrbanizacao.CoberturaB);
        double a2 = ExecucaoOficial(inversa, terreno, AtividadeUrbanizacao.CoberturaA);

        Assert.Equal(a1, a2, precision: 6);
        Assert.Equal(b1, b2, precision: 6);
    }

    // ───────────────────── a métrica e as condições oficiais ─────────────────────

    /// <summary>
    /// A chuva oficial vem da tabela de presets, não de um número escrito na atividade.
    /// Se alguém mudar o preset, a atividade acompanha — e o teste não precisa saber
    /// quanto ele chove.
    /// </summary>
    [Fact]
    public void AChuvaOficialEUmPresetDaTabela()
    {
        Assert.Contains(AtividadeUrbanizacao.ChuvaOficial, IntensidadesDeChuva.Todas);
        Assert.True(AtividadeUrbanizacao.ChuvaOficial.MmPorSegundo > 0);
    }

    /// <summary>
    /// A métrica comparada é o pico do episódio, e ele é por episódio — não acumula entre
    /// execuções como os litros. É o que permite compará-la sem correção adicional.
    /// </summary>
    [Fact]
    public void AMetricaComparadaEOPicoDoEpisodio()
    {
        var terreno = TerrenoDeReferencia();
        var agua = new WaterSimulation(W, H);

        ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);
        double litrosDepoisDeUma = agua.InfiltradoLitros;
        double picoDepoisDeUma = agua.PicoAlagamentoPercent;

        ExecucaoOficial(agua, terreno, AtividadeUrbanizacao.CoberturaA);

        Assert.Equal(picoDepoisDeUma, agua.PicoAlagamentoPercent, precision: 6);
        Assert.Equal(litrosDepoisDeUma, agua.InfiltradoLitros, precision: 6);
    }

    /// <summary>
    /// Sem a preparação, a segunda execução herda o solo molhado da primeira e o pico
    /// muda. É o defeito que o protocolo existe para fechar, e este teste garante que a
    /// preparação continua sendo o que faz diferença — não um passo decorativo.
    /// </summary>
    [Fact]
    public void SemAPreparacaoOControleDeixaDeSeRepetir()
    {
        var terreno = TerrenoDeReferencia();
        var agua = new WaterSimulation(W, H);
        agua.Solo.Preencher(AtividadeUrbanizacao.CoberturaA);
        agua.Ativo = true;

        double primeira = SemPreparar();
        double segunda = SemPreparar();

        Assert.NotEqual(primeira, segunda, precision: 2);

        double SemPreparar()
        {
            agua.IniciarChuva(AtividadeUrbanizacao.ChuvaOficial.MmPorSegundo,
                              AtividadeUrbanizacao.DuracaoSegundos);
            float passo = AtividadeUrbanizacao.PassoSegundos;
            int quadros = (int)((AtividadeUrbanizacao.DuracaoSegundos + 2f) / passo);
            for (int i = 0; i < quadros; i++) agua.Atualizar(terreno, W, H, passo);
            return agua.PicoAlagamentoPercent;
        }
    }
}
