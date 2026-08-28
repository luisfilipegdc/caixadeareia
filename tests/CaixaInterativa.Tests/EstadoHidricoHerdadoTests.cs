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
/// A variável oculta que a investigação encontrou na caixa física.
///
/// Duas execuções idênticas — mesma cobertura, mesma chuva, mesma duração, mesmo relevo —
/// deram picos diferentes: <b>48% e 53%</b>. Nada visível havia mudado. O que mudou foi o
/// que ficou: a água e a saturação da execução anterior atravessaram para a seguinte.
///
/// Estes testes descrevem esse comportamento em laboratório, antes de qualquer correção.
/// Eles não o condenam — memória hídrica é fenômeno real e tem valor de aula. O que eles
/// fixam é que ela **existe**, para que a atividade oficial não possa ignorá-la em
/// silêncio enquanto afirma controlar as demais variáveis.
/// </summary>
public class EstadoHidricoHerdadoTests
{
    private const int W = 64, H = 48;

    /// <summary>Uma bacia simples: fundo no centro, bordas altas. Sempre a mesma.</summary>
    private static float[] Bacia()
    {
        var t = new float[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = (x - W / 2f) / (W / 2f);
                float dy = (y - H / 2f) / (H / 2f);
                t[y * W + x] = 80f * (dx * dx + dy * dy);
            }
        return t;
    }

    /// <summary>
    /// Roda um episódio de chuva até o fim, com passo fixo. Devolve o pico do episódio.
    /// </summary>
    private static double Episodio(WaterSimulation agua, float[] terreno,
                                   float mmPorSegundo = 8f, float segundos = 20f)
    {
        agua.IniciarChuva(mmPorSegundo, segundos);

        const float dt = 1f / 30f;
        int quadros = (int)(segundos / dt) + 60;   // dois segundos extras de escoamento
        for (int i = 0; i < quadros; i++)
            agua.Atualizar(terreno, W, H, dt);

        return agua.PicoAlagamentoPercent;
    }

    // ───────────────── a memória hídrica existe ─────────────────

    /// <summary>
    /// <b>O defeito, em laboratório.</b> A segunda execução parte de um solo que a
    /// primeira encharcou, e o pico muda sem que nenhuma variável declarada tenha mudado.
    /// </summary>
    [Fact]
    public void SegundaExecucaoIdenticaNaoRepeteOPicoDaPrimeira()
    {
        var agua = new WaterSimulation(W, H);
        agua.Solo.Preencher(TipoDeSolo.Mata);
        var terreno = Bacia();

        double primeiro = Episodio(agua, terreno);
        double segundo = Episodio(agua, terreno);   // sem preparar nada

        Assert.True(primeiro > 0, "A primeira execução nem alagou; o cenário não serve.");
        Assert.NotEqual(primeiro, segundo, precision: 1);
    }

    /// <summary>A saturação sobrevive ao fim do episódio — é ela que atravessa.</summary>
    [Fact]
    public void ASaturacaoSobreviveAoFimDaChuva()
    {
        var agua = new WaterSimulation(W, H);
        agua.Solo.Preencher(TipoDeSolo.Mata);

        Episodio(agua, Bacia());

        Assert.True(agua.SaturacaoMediaPercent > 0,
                    "Sem saturação residual não há o que herdar, e o teste não descreve nada.");
    }

    /// <summary>
    /// Os acumuladores de litros somam entre execuções. Medido na caixa física: 68,3 L
    /// depois da primeira chuva, 123,4 L depois da segunda — a mesma chuva.
    /// </summary>
    [Fact]
    public void OsLitrosAcumulamEntreExecucoes()
    {
        var agua = new WaterSimulation(W, H);
        agua.Solo.Preencher(TipoDeSolo.Mata);
        var terreno = Bacia();

        Episodio(agua, terreno);
        double depoisDoPrimeiro = agua.InfiltradoLitros;

        Episodio(agua, terreno);

        Assert.True(agua.InfiltradoLitros > depoisDoPrimeiro,
                    "InfiltradoLitros deveria estar somando os dois episódios.");
    }

    /// <summary>
    /// O pico, ao contrário, já é por episódio: <c>IniciarChuva</c> o zera. É o que torna
    /// esta métrica a única candidata a comparação sem correção adicional.
    /// </summary>
    [Fact]
    public void OPicoJaEPorEpisodio()
    {
        var agua = new WaterSimulation(W, H);
        agua.Solo.Preencher(TipoDeSolo.Mata);

        Episodio(agua, Bacia());
        Assert.True(agua.PicoAlagamentoPercent > 0);

        agua.IniciarChuva(8f, 20f);
        Assert.Equal(0, agua.PicoAlagamentoPercent);
    }
}
