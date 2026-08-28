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

using CaixaInterativa.Rendering;
using CaixaInterativa.Simulation;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// As três regras que a queimada ganhou: começar num ponto escolhido, não atravessar o
/// mar, e deixar marca por onde passou.
/// </summary>
public class FogoNoMarTests
{
    private const int W = 64, H = 48;
    private const float MinMm = -100f, MaxMm = 200f;

    /// <summary>A cota em que o mapa deixa de ser azul, na mesma conta do renderizador.</summary>
    private static float LinhaDagua => MinMm + (MaxMm - MinMm) * TopographicRenderer.FracaoDaLinhaDagua;

    /// <summary>
    /// Um relevo com mar à esquerda e terra à direita, na metade exata.
    ///
    /// O mar fica bem abaixo da linha d'água e a terra bem acima: o teste é sobre a regra,
    /// não sobre o comportamento em cima da fronteira.
    /// </summary>
    private static float[] MeioAMeio()
    {
        var t = new float[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                t[y * W + x] = x < W / 2 ? MinMm : MaxMm;
        return t;
    }

    private static FireSimulation Preparado(TipoDeSolo cobertura = TipoDeSolo.Mata)
    {
        var solo = new SoilMap(W, H);
        solo.Preencher(cobertura);

        var fogo = new FireSimulation(W, H, semente: 7)
        {
            Solo = solo,
            AlturaMinimaMm = MinMm,
            AlturaMaximaMm = MaxMm,
        };

        // Um quadro para o relevo chegar: Atear lê o terreno do último Atualizar.
        fogo.Ativo = true;
        fogo.Atualizar(MeioAMeio(), W, H, 0.016f);
        return fogo;
    }

    // ───────────────── a linha d'água ─────────────────

    [Fact]
    public void ACotaDaLinhaDaguaSegueAFracaoDoRenderizador()
    {
        var fogo = new FireSimulation(W, H) { AlturaMinimaMm = MinMm, AlturaMaximaMm = MaxMm };

        Assert.Equal(LinhaDagua, fogo.CotaDaLinhaDaguaMm, precision: 3);
    }

    /// <summary>
    /// Sem faixa de alturas informada, a regra fica desligada em vez de inventar uma
    /// escala. Com mínima e máxima em zero, "abaixo da linha d'água" viraria "abaixo de
    /// zero", e metade de um relevo qualquer viraria oceano sem ninguém ter pedido.
    /// </summary>
    [Fact]
    public void SemEscalaDeAlturaARegraDoMarNaoSeAplica()
    {
        var solo = new SoilMap(W, H);
        solo.Preencher(TipoDeSolo.Mata);

        var fogo = new FireSimulation(W, H, semente: 7) { Solo = solo };
        fogo.Ativo = true;
        fogo.Atualizar(MeioAMeio(), W, H, 0.016f);

        // Um ponto no "mar" continua aceitando fogo, porque não há mar declarado.
        Assert.True(fogo.Atear(0.10f, 0.5f));
    }

    // ───────────────── o ponto escolhido ─────────────────

    [Fact]
    public void AtearNoMarERecusadoComMotivoProprio()
    {
        var fogo = Preparado();

        Assert.False(fogo.Atear(0.10f, 0.5f));
        Assert.Equal(FireSimulation.MotivoDaRecusa.NoMar, fogo.PontoRecusado);
        Assert.False(fogo.EmAndamento);
    }

    [Fact]
    public void AtearEmTerraFirmeAcendeNoPontoEscolhido()
    {
        var fogo = Preparado();

        Assert.True(fogo.Atear(0.80f, 0.5f));
        Assert.Equal(FireSimulation.MotivoDaRecusa.Nenhum, fogo.PontoRecusado);
        Assert.True(fogo.EmAndamento);

        // O foco caiu onde foi pedido, e não num sorteio.
        Assert.True(fogo.FocoU > 0.5f, $"FocoU={fogo.FocoU} caiu no lado do mar.");
    }

    /// <summary>
    /// Ponto sem combustível não cai para o sorteio. Acender do outro lado do mapa depois
    /// de alguém apontar um lugar específico pareceria defeito, não escolha.
    /// </summary>
    [Fact]
    public void PontoSemCombustivelERecusadoEmVezDeSorteado()
    {
        var fogo = Preparado(TipoDeSolo.Rocha);

        Assert.False(fogo.Atear(0.80f, 0.5f));
        Assert.Equal(FireSimulation.MotivoDaRecusa.SemCombustivel, fogo.PontoRecusado);
        Assert.False(fogo.EmAndamento);
    }

    /// <summary>
    /// <b>Encontrado na caixa de verdade.</b> Com o relevo inteiro submerso, o sorteio não
    /// achava candidato e a tela culpava a cobertura — mandava escolher Pastagem com
    /// Pastagem já escolhida. A cobertura queima; o que falta é terra seca, e o conselho
    /// para isso é o oposto.
    /// </summary>
    [Fact]
    public void RelevoTodoSubmersoNaoCulpaACobertura()
    {
        var solo = new SoilMap(W, H);
        solo.Preencher(TipoDeSolo.Pastagem);   // queima: combustível 0,75

        var fogo = new FireSimulation(W, H, semente: 7)
        {
            Solo = solo,
            AlturaMinimaMm = MinMm,
            AlturaMaximaMm = MaxMm,
        };

        var afogado = new float[W * H];
        Array.Fill(afogado, MinMm);            // tudo abaixo da linha d'água
        fogo.Ativo = true;
        fogo.Atualizar(afogado, W, H, 0.016f);

        Assert.False(fogo.Atear());
        Assert.Equal(FireSimulation.MotivoDaRecusa.TudoNoMar, fogo.PontoRecusado);
    }

    /// <summary>Sem combustível em lugar nenhum, aí sim o problema é a cobertura.</summary>
    [Fact]
    public void CoberturaSemCombustivelContinuaSendoCulpaDaCobertura()
    {
        var fogo = Preparado(TipoDeSolo.Rocha);

        Assert.False(fogo.Atear());
        Assert.Equal(FireSimulation.MotivoDaRecusa.SemCombustivel, fogo.PontoRecusado);
    }

    /// <summary>Sem ponto, o sorteio continua existindo — e nunca sorteia no mar.</summary>
    [Fact]
    public void OSorteioNuncaCaiNoMar()
    {
        for (int tentativa = 0; tentativa < 25; tentativa++)
        {
            var fogo = Preparado();
            Assert.True(fogo.Atear());
            Assert.True(fogo.FocoU >= 0.5f,
                        $"Sorteio caiu em FocoU={fogo.FocoU}, que é mar.");
        }
    }

    // ───────────────── o fogo não atravessa o mar ─────────────────

    /// <summary>
    /// <b>O pedido central.</b> Fogo aceso em terra queima a terra inteira e para na
    /// praia. Se atravessasse, o incêndio apareceria sobre o azul.
    /// </summary>
    [Fact]
    public void OFogoQueimaATerraEParaNaLinhaDagua()
    {
        var fogo = Preparado();
        var relevo = MeioAMeio();

        Assert.True(fogo.Atear(0.99f, 0.5f));

        // Tempo de sobra para o fogo varrer tudo o que consegue alcançar.
        for (int i = 0; i < 4000 && fogo.EmAndamento; i++)
            fogo.Atualizar(relevo, W, H, 0.05f);

        Assert.False(fogo.EmAndamento);

        // Nenhuma célula do lado do mar chegou a queimar.
        int larguraFogo = fogo.Width, alturaFogo = fogo.Height;
        for (int y = 0; y < alturaFogo; y++)
            for (int x = 0; x < larguraFogo / 2; x++)
                Assert.Equal(0f, fogo.Cicatriz[y * larguraFogo + x]);

        // E o lado seco queimou de verdade — senão o teste passaria com o fogo morto.
        Assert.True(fogo.AreaQueimadaPercent > 20,
                    $"Só {fogo.AreaQueimadaPercent:F1}% queimou; o incêndio nem se espalhou.");
    }

    // ───────────────── a cicatriz ─────────────────

    /// <summary>
    /// Onde a queimada passa, degrada — e a degradação continua na tela depois que a
    /// última chama apaga. Antes, o calor zerava e o mapa voltava a ser o que era.
    /// </summary>
    [Fact]
    public void ACicatrizFicaDepoisQueOFogoApaga()
    {
        var fogo = Preparado();
        var relevo = MeioAMeio();

        Assert.True(fogo.Atear(0.99f, 0.5f));

        for (int i = 0; i < 4000 && fogo.EmAndamento; i++)
            fogo.Atualizar(relevo, W, H, 0.05f);

        Assert.False(fogo.EmAndamento);
        Assert.All(fogo.Calor, c => Assert.Equal(0f, c));       // a chama passou
        Assert.Contains(fogo.Cicatriz, c => c > 0.5f);          // a marca ficou
    }

    /// <summary>O solo degradado também é gravado no mapa que a chuva seguinte vai ler.</summary>
    [Fact]
    public void OSoloQueimadoEGravadoNoMapaDeCobertura()
    {
        var fogo = Preparado();
        var relevo = MeioAMeio();

        Assert.True(fogo.Atear(0.99f, 0.5f));
        for (int i = 0; i < 4000 && fogo.EmAndamento; i++)
            fogo.Atualizar(relevo, W, H, 0.05f);

        Assert.Contains(fogo.Solo!.Celulas, c => c == TipoDeSolo.Queimado);
    }

    /// <summary>Um segundo incêndio não apaga o rastro do primeiro.</summary>
    [Fact]
    public void UmNovoIncendioNaoApagaACicatrizAnterior()
    {
        var fogo = Preparado();
        var relevo = MeioAMeio();

        Assert.True(fogo.Atear(0.99f, 0.5f));
        for (int i = 0; i < 4000 && fogo.EmAndamento; i++)
            fogo.Atualizar(relevo, W, H, 0.05f);

        int marcadas = fogo.Cicatriz.Count(c => c > 0.5f);
        Assert.True(marcadas > 0);

        fogo.Atear();   // pode nem pegar; o que importa é que Preparar não zerou nada
        Assert.Equal(marcadas, fogo.Cicatriz.Count(c => c > 0.5f));
    }

    /// <summary>Limpar a simulação é o botão de recomeçar a aula — aí sim a marca some.</summary>
    [Fact]
    public void LimparApagaACicatriz()
    {
        var fogo = Preparado();
        var relevo = MeioAMeio();

        Assert.True(fogo.Atear(0.99f, 0.5f));
        for (int i = 0; i < 4000 && fogo.EmAndamento; i++)
            fogo.Atualizar(relevo, W, H, 0.05f);

        Assert.Contains(fogo.Cicatriz, c => c > 0.5f);

        fogo.Limpar();
        Assert.All(fogo.Cicatriz, c => Assert.Equal(0f, c));
    }
}
