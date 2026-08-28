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

using CaixaInterativa.Config;
using CaixaInterativa.Rendering;
using CaixaInterativa.Simulation;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// Trava o mapeamento entre cada módulo e as camadas que ele declara.
///
/// A regressão visual monta as camadas à mão, então provaria pouco se um módulo passasse
/// a declarar o campo errado. Estes testes cobrem justamente essa costura.
/// </summary>
public class MapeamentoDeCamadasTests
{
    private const int W = 640, H = 480;
    private const int Ws = W / 2, Hs = H / 2;

    [Fact]
    public void AguaDeclaraUmaCamadaComVelocidadeComoAuxiliar()
    {
        var agua = new WaterSimulation(W, H);
        var camadas = agua.Camadas;

        Assert.Single(camadas);

        var c = camadas[0];
        Assert.Equal(ModoDeCor.Agua, c.Modo);
        Assert.Equal(CamadaVisual.OrdemAgua, c.Ordem);
        Assert.Equal(0.25f, c.Limiar);
        Assert.Equal(Ws, c.Largura);
        Assert.Equal(Hs, c.Altura);
        Assert.Same(agua.Profundidade, c.Campo);
        Assert.Same(agua.Velocidade, c.CampoAuxiliar);
        Assert.True(c.Desenhavel);
    }

    /// <summary>
    /// <c>MoverAgua</c> troca o buffer de profundidade a cada substep. Se a camada fosse
    /// montada uma vez só, passaria a apontar para o buffer antigo e a projeção
    /// congelaria a água num quadro anterior — sem erro, sem exceção, só errado.
    /// </summary>
    [Fact]
    public void CamadaDaAguaAcompanhaATrocaDeBuffer()
    {
        var agua = new WaterSimulation(W, H);
        var terreno = new float[W * H];
        agua.IniciarChuva(10f, 5f);

        for (int quadro = 0; quadro < 5; quadro++)
        {
            agua.Atualizar(terreno, W, H, 0.033f);
            Assert.Same(agua.Profundidade, agua.Camadas[0].Campo);
        }
    }

    [Fact]
    public void TerremotoDeclaraDanoDepoisOndaNessaOrdem()
    {
        var sismo = new EarthquakeSimulation(W, H);
        var camadas = sismo.Camadas;

        Assert.Equal(2, camadas.Count);

        Assert.Equal(ModoDeCor.Risco, camadas[0].Modo);
        Assert.Equal(CamadaVisual.OrdemRisco, camadas[0].Ordem);
        Assert.Equal(0.15f, camadas[0].Limiar);
        Assert.Same(sismo.Dano, camadas[0].Campo);

        Assert.Equal(ModoDeCor.Clarao, camadas[1].Modo);
        Assert.Equal(CamadaVisual.OrdemClarao, camadas[1].Ordem);
        Assert.Equal(0.04f, camadas[1].Limiar);
        Assert.Same(sismo.Intensidade, camadas[1].Campo);

        // O clarão passa por cima do dano, como antes da refatoração.
        Assert.True(camadas[0].Ordem < camadas[1].Ordem);
    }

    [Fact]
    public void FogoDeclaraUmaCamadaDeCalorAcimaDeTudo()
    {
        var fogo = new FireSimulation(W, H, semente: 42);
        var camadas = fogo.Camadas;

        // Duas: a cicatriz, que fica, e a chama, que passa. Eram uma só, e por isso o
        // mapa voltava ao que era quando o último foco apagava.
        Assert.Equal(2, camadas.Count);

        var cicatriz = camadas.Single(c => c.Modo == ModoDeCor.Cicatriz);
        Assert.Equal(CamadaVisual.OrdemCicatriz, cicatriz.Ordem);

        var chama = camadas.Single(c => c.Modo == ModoDeCor.Calor);
        Assert.Equal(CamadaVisual.OrdemCalor, chama.Ordem);
        Assert.Equal(0.03f, chama.Limiar);
        Assert.Same(fogo.Calor, chama.Campo);

        // Onde ainda há fogo, é o fogo que se vê: a chama desenha por cima da cicatriz.
        Assert.True(CamadaVisual.OrdemCalor > CamadaVisual.OrdemCicatriz);
        Assert.True(CamadaVisual.OrdemCicatriz > CamadaVisual.OrdemClarao);
        Assert.True(CamadaVisual.OrdemClarao > CamadaVisual.OrdemRisco);
        Assert.True(CamadaVisual.OrdemRisco > CamadaVisual.OrdemAgua);
    }

    /// <summary>
    /// A ordem que o <c>SandboxEngine</c> monta — água, terremoto, fogo — já sai
    /// crescente, e é por isso que ele não ordena nada por quadro. Se alguém acrescentar
    /// um módulo fora de ordem, este teste falha antes de a imagem sair errada.
    /// </summary>
    [Fact]
    public void ConcatenacaoNaOrdemDoEngineJaSaiCrescente()
    {
        var agua = new WaterSimulation(W, H);
        var sismo = new EarthquakeSimulation(W, H);
        var fogo = new FireSimulation(W, H, semente: 42);

        var todas = new List<CamadaVisual>();
        todas.AddRange(agua.Camadas);
        todas.AddRange(sismo.Camadas);
        todas.AddRange(fogo.Camadas);

        // Água, dano sísmico, frente de onda, cicatriz do fogo e chama.
        Assert.Equal(5, todas.Count);
        for (int i = 1; i < todas.Count; i++)
            Assert.True(todas[i - 1].Ordem < todas[i].Ordem,
                        $"Camada {i} tem ordem {todas[i].Ordem}, que não vem depois de {todas[i - 1].Ordem}.");
    }

    /// <summary>
    /// O ciclo de quadro do engine percorre os módulos pela interface, sem conhecer
    /// nenhum deles pelo nome. Este teste exercita esse caminho: atualizar só os ativos,
    /// coletar camadas só dos ativos, e limpar todos genericamente.
    /// </summary>
    [Fact]
    public void ModulosFuncionamPolimorficamente()
    {
        var agua = new WaterSimulation(W, H);
        var modulos = new List<ISimulationModule>
        {
            agua,
            new EarthquakeSimulation(W, H),
            new FireSimulation(W, H, semente: 42),
        };

        var terreno = new float[W * H];

        // Nenhum ativo: nada atualiza, nada é desenhado.
        var camadas = new List<CamadaVisual>();
        foreach (var m in modulos)
        {
            if (m.Ativo) m.Atualizar(terreno, W, H, 0.033f);
            if (m.Ativo) camadas.AddRange(m.Camadas);
        }
        Assert.Empty(camadas);

        // Só a água ativa: só a camada dela aparece.
        agua.IniciarChuva(12f, 4f);
        Assert.True(agua.Ativo);

        camadas.Clear();
        foreach (var m in modulos)
        {
            if (m.Ativo) m.Atualizar(terreno, W, H, 0.033f);
            if (m.Ativo) camadas.AddRange(m.Camadas);
        }

        Assert.Single(camadas);
        Assert.Equal(ModoDeCor.Agua, camadas[0].Modo);
        Assert.True(agua.VolumeLitros > 0, "A chuva devia ter colocado água na caixa.");

        // Limpeza genérica, como LimparSimulacoes faz.
        foreach (var m in modulos) { m.Limpar(); m.Ativo = false; }

        Assert.All(modulos, m => Assert.False(m.Ativo));
        Assert.Equal(0, agua.VolumeLitros);
    }

    /// <summary>
    /// A lista de camadas é reaproveitada entre quadros. Se cada acesso devolvesse um
    /// array novo, o caminho de renderização voltaria a alocar 30 vezes por segundo.
    /// </summary>
    [Fact]
    public void AcessarCamadasNaoAlocaListaNova()
    {
        var agua = new WaterSimulation(W, H);
        var sismo = new EarthquakeSimulation(W, H);
        var fogo = new FireSimulation(W, H, semente: 42);

        Assert.Same(agua.Camadas, agua.Camadas);
        Assert.Same(sismo.Camadas, sismo.Camadas);
        Assert.Same(fogo.Camadas, fogo.Camadas);
    }

    /// <summary>
    /// Camada sem campo ou com dimensão inválida é descartada em vez de derrubar o
    /// renderizador — o guard que os três <c>if</c> antigos faziam à mão.
    /// </summary>
    [Fact]
    public void CamadaNaoDesenhavelEIgnorada()
    {
        var terreno = CenariosDeRegressao.Terreno();
        var calor = CenariosDeRegressao.CalorDoFogo();

        string SoComEssas(IReadOnlyList<CamadaVisual> camadas) =>
            CenariosDeRegressao.Hash(new TopographicRenderer().Render(
                terreno, W, H,
                new ProjectionSettings(), new ProcessingSettings(), new RenderSettings(),
                camadas));

        var valida = new CamadaVisual(calor, Ws, Hs, CamadaVisual.OrdemCalor, ModoDeCor.Calor, 0.03f);
        var semDimensao = new CamadaVisual(calor, 0, 0, CamadaVisual.OrdemAgua, ModoDeCor.Agua, 0.25f);
        var semCampo = new CamadaVisual(null!, Ws, Hs, CamadaVisual.OrdemRisco, ModoDeCor.Risco, 0.15f);

        Assert.Equal(SoComEssas([valida]), SoComEssas([semDimensao, valida, semCampo]));
    }
}
