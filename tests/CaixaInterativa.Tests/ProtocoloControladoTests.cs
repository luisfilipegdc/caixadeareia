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
/// O protocolo que torna duas execuções comparáveis.
///
/// São duas variáveis, e as duas foram medidas antes de serem escolhidas:
///
/// <b>1. O estado hídrico.</b> Sem zerar, a segunda execução parte do solo que a primeira
/// encharcou. Medido na caixa física: 48% e 53% para a mesma cobertura.
///
/// <b>2. O passo de tempo.</b> Medido em laboratório, mudando só o passo: numa encosta o
/// pico foi de 0,0% (1/60) a 9,9% (1/24); numa bacia fechada, de 67,6% a 79,8%. É mais
/// variação do que a troca de cobertura produz.
///
/// Com as duas congeladas o solver é exatamente reprodutível — mesma casa decimal.
/// </summary>
public class ProtocoloControladoTests
{
    private const int W = 64, H = 48;
    private const float Passo = 1f / 30f;

    /// <summary>Bacia fechada: alaga bastante, bom para medir diferenças.</summary>
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

    /// <summary>Encosta que drena: alaga pouco, e é onde o passo de tempo mais pesa.</summary>
    private static float[] Rampa()
    {
        var t = new float[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                t[y * W + x] = 120f * (1f - (float)y / H) + 8f * MathF.Sin(x * 0.7f);
        return t;
    }

    private static double EpisodioControlado(WaterSimulation agua, float[] terreno,
                                             float mm = 8f, float segundos = 20f)
    {
        agua.PrepararExecucaoControlada();
        agua.IniciarChuva(mm, segundos);

        int quadros = (int)(segundos / Passo) + (int)(2f / Passo);
        for (int i = 0; i < quadros; i++) agua.Atualizar(terreno, W, H, Passo);

        return agua.PicoAlagamentoPercent;
    }

    private static WaterSimulation Com(TipoDeSolo cobertura)
    {
        var a = new WaterSimulation(W, H);
        a.Solo.Preencher(cobertura);
        return a;
    }

    // ───────────────── o que a preparação zera ─────────────────

    [Fact]
    public void PreparacaoZeraAguaSuperficial()
    {
        var agua = Com(TipoDeSolo.Mata);
        EpisodioControlado(agua, Bacia());
        Assert.True(agua.VolumeLitros > 0 || agua.Profundidade.Any(p => p > 0));

        agua.PrepararExecucaoControlada();

        Assert.All(agua.Profundidade, p => Assert.Equal(0f, p));
        Assert.Equal(0, agua.VolumeLitros);
        Assert.Equal(0, agua.AreaAlagadaPercent);
    }

    [Fact]
    public void PreparacaoZeraSaturacao()
    {
        var agua = Com(TipoDeSolo.Mata);
        EpisodioControlado(agua, Bacia());
        Assert.True(agua.SaturacaoMediaPercent > 0);

        agua.PrepararExecucaoControlada();

        Assert.All(agua.Saturacao, s => Assert.Equal(0f, s));
        Assert.Equal(0, agua.SaturacaoMediaPercent);
    }

    [Fact]
    public void PreparacaoZeraInfiltradoAcumulado()
    {
        var agua = Com(TipoDeSolo.Mata);
        EpisodioControlado(agua, Bacia());
        Assert.True(agua.InfiltradoLitros > 0);

        agua.PrepararExecucaoControlada();

        Assert.Equal(0, agua.InfiltradoLitros);
    }

    /// <summary>
    /// <b>Achado durante a implementação: <c>EscoadoLitros</c> nunca é calculado.</b>
    ///
    /// A propriedade existe, está documentada como "volume que escoou pelas bordas" e é
    /// zerada em <c>Limpar</c> — e nenhuma linha do solver a atribui. Vale zero sempre.
    /// Ninguém percebeu porque nada a lê: a tela mostra <c>VolumeLitros</c> sob o rótulo
    /// "Escoando", que é a água ainda na superfície, não a que saiu.
    ///
    /// Não foi corrigido nesta sessão. Calcular a vazão de saída é mudança no solver, fora
    /// do protocolo controlado, e a atividade não usa litros como conclusão. Fica
    /// registrado aqui para não ser redescoberto como novidade.
    ///
    /// O teste verifica o que dá para verificar hoje: a preparação a deixa em zero.
    /// </summary>
    [Fact]
    public void PreparacaoZeraEscoadoAcumulado()
    {
        var agua = Com(TipoDeSolo.Impermeavel);
        EpisodioControlado(agua, Rampa());

        agua.PrepararExecucaoControlada();

        Assert.Equal(0, agua.EscoadoLitros);
    }

    [Fact]
    public void PreparacaoZeraOPicoAnterior()
    {
        var agua = Com(TipoDeSolo.Mata);
        EpisodioControlado(agua, Bacia());
        Assert.True(agua.PicoAlagamentoPercent > 0);

        agua.PrepararExecucaoControlada();

        Assert.Equal(0, agua.PicoAlagamentoPercent);
    }

    [Fact]
    public void PreparacaoZeraErosaoAcumulada()
    {
        var agua = Com(TipoDeSolo.Desmatado);
        EpisodioControlado(agua, Rampa());

        agua.PrepararExecucaoControlada();

        Assert.Equal(0, agua.ErosaoTotal);
    }

    /// <summary>
    /// Preparar não mexe no relevo nem na cobertura: o relevo é reamostrado do sensor a
    /// cada quadro e nunca fica guardado aqui, e a cobertura é decisão de quem dá aula.
    /// </summary>
    [Fact]
    public void PreparacaoPreservaCoberturaERelevo()
    {
        var agua = Com(TipoDeSolo.Mata);
        var terreno = Bacia();
        double antes = EpisodioControlado(agua, terreno);

        agua.PrepararExecucaoControlada();

        Assert.All(agua.Solo.Celulas, c => Assert.Equal(TipoDeSolo.Mata, c));

        // O relevo continua o mesmo: repetir o episódio devolve exatamente o mesmo pico.
        Assert.Equal(antes, EpisodioControlado(agua, terreno), precision: 6);
    }

    [Fact]
    public void PreparacaoNaoIniciaChuva()
    {
        var agua = Com(TipoDeSolo.Mata);
        agua.PrepararExecucaoControlada();

        Assert.False(agua.Chovendo);
    }

    // ───────────────── reprodutibilidade ─────────────────

    /// <summary>
    /// <b>A invariante que sustenta a atividade.</b> Com estado e passo congelados, duas
    /// execuções idênticas dão o mesmo pico — não "aproximadamente", exatamente.
    ///
    /// A tolerância não foi escolhida: foi medida. Três execuções deram 79,6875 nas três
    /// casas decimais, então a comparação é por igualdade.
    /// </summary>
    [Theory]
    [InlineData(TipoDeSolo.Mata)]
    [InlineData(TipoDeSolo.Impermeavel)]
    public void DuasExecucoesControladasDaoOMesmoPico(TipoDeSolo cobertura)
    {
        var agua = Com(cobertura);
        var terreno = Bacia();

        double primeira = EpisodioControlado(agua, terreno);
        double segunda = EpisodioControlado(agua, terreno);
        double terceira = EpisodioControlado(agua, terreno);

        Assert.Equal(primeira, segunda, precision: 6);
        Assert.Equal(primeira, terceira, precision: 6);
    }

    /// <summary>Vale também na encosta, onde o passo de tempo mais pesava.</summary>
    [Fact]
    public void ReprodutibilidadeValeNaEncostaTambem()
    {
        var agua = Com(TipoDeSolo.Mata);
        var rampa = Rampa();

        Assert.Equal(EpisodioControlado(agua, rampa),
                     EpisodioControlado(agua, rampa), precision: 6);
    }

    /// <summary>
    /// Instâncias diferentes com a mesma configuração também coincidem — o resultado é do
    /// modelo, não do objeto.
    /// </summary>
    [Fact]
    public void InstanciasDiferentesDaoOMesmoPico()
    {
        var terreno = Bacia();

        Assert.Equal(EpisodioControlado(Com(TipoDeSolo.Mata), terreno),
                     EpisodioControlado(Com(TipoDeSolo.Mata), terreno), precision: 6);
    }

    /// <summary>
    /// O passo de tempo é mesmo a variável que precisava ser congelada. Este teste mede o
    /// tamanho do problema em vez de afirmá-lo — e falha se algum dia ele sumir sozinho,
    /// porque aí a documentação acima estará desatualizada.
    /// </summary>
    [Fact]
    public void OPassoDeTempoMudaOResultado()
    {
        var rampa = Rampa();

        double a = ComPasso(1f / 60f, rampa);
        double b = ComPasso(1f / 24f, rampa);

        Assert.True(Math.Abs(a - b) > 1.0,
                    $"Passos diferentes deram {a:F2}% e {b:F2}% — se convergiram, o " +
                    "protocolo de passo fixo pode ter deixado de ser necessário.");

        static double ComPasso(float passo, float[] terreno)
        {
            var agua = Com(TipoDeSolo.Mata);
            agua.IniciarChuva(8f, 20f);
            int quadros = (int)(22f / passo);
            for (int i = 0; i < quadros; i++) agua.Atualizar(terreno, W, H, passo);
            return agua.PicoAlagamentoPercent;
        }
    }

    // ───────────────── o modo livre não mudou ─────────────────

    /// <summary>
    /// <b>Prova de não-regressão.</b> Sem pedir preparação, a memória hídrica continua
    /// existindo: chover, parar e chover de novo alaga mais na segunda vez, porque o solo
    /// já está molhado. É fenômeno de aula, e não foi removido para consertar a atividade.
    /// </summary>
    [Fact]
    public void ModoLivreContinuaAcumulandoAguaEntreChuvas()
    {
        var agua = Com(TipoDeSolo.Mata);
        var terreno = Bacia();

        double primeira = SemPreparar(agua, terreno);
        double segunda = SemPreparar(agua, terreno);

        Assert.True(segunda > primeira,
                    $"A segunda chuva deu {segunda:F2}% contra {primeira:F2}% da primeira. " +
                    "A memória hídrica do modo livre desapareceu.");

        static double SemPreparar(WaterSimulation agua, float[] terreno)
        {
            agua.IniciarChuva(8f, 20f);
            int quadros = (int)(22f / Passo);
            for (int i = 0; i < quadros; i++) agua.Atualizar(terreno, W, H, Passo);
            return agua.PicoAlagamentoPercent;
        }
    }

    /// <summary>
    /// <c>IniciarChuva</c> continua não limpando nada além do pico do episódio. Se alguém
    /// puser a limpeza lá dentro, o modo livre perde o fenômeno e este teste acusa.
    /// </summary>
    [Fact]
    public void IniciarChuvaNaoLimpaOEstadoHidrico()
    {
        var agua = Com(TipoDeSolo.Mata);
        EpisodioControlado(agua, Bacia());

        double saturacaoAntes = agua.SaturacaoMediaPercent;
        Assert.True(saturacaoAntes > 0);

        agua.IniciarChuva(8f, 5f);

        Assert.Equal(saturacaoAntes, agua.SaturacaoMediaPercent, precision: 6);
    }
}
