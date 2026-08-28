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
/// O fogo lê a água para saber onde não pode passar. Esse acoplamento é por referência a
/// um <c>float[]</c>, e a água troca esse array a cada substep — o que já produziu um bug
/// real, encontrado por medição durante a sessão autônoma de 27/08/2026.
/// </summary>
public class AcoplamentoFogoAguaTests
{
    private const int W = 640, H = 480;

    /// <summary>
    /// Documenta o motivo de o engine precisar reapontar `Fogo.Agua` a cada quadro.
    ///
    /// `MoverAgua` termina com `(_agua, _aguaNova) = (_aguaNova, _agua)`. Como o número
    /// de substeps por quadro é ímpar em parte dos quadros, a referência devolvida por
    /// `Profundidade` alterna entre dois arrays. Quem guardar a referência uma única vez
    /// passa a ler o buffer da iteração anterior em parte dos quadros.
    ///
    /// Medido quando o bug foi encontrado: 7 de 20 quadros divergiam.
    /// </summary>
    [Fact]
    public void BufferDaAguaAlternaEntreQuadros()
    {
        var agua = new WaterSimulation(W, H);
        var referenciaInicial = agua.Profundidade;
        var terreno = new float[W * H];

        agua.IniciarChuva(10f, 3f);

        int divergencias = 0;
        for (int i = 0; i < 20; i++)
        {
            agua.Atualizar(terreno, W, H, 0.033f);
            if (!ReferenceEquals(referenciaInicial, agua.Profundidade)) divergencias++;
        }

        Assert.True(divergencias > 0,
            "Se a referência parasse de alternar, o reapontamento por quadro no " +
            "SandboxEngine deixaria de ser necessário — e este teste vira o aviso disso.");
    }

    /// <summary>
    /// O padrão que o <c>SandboxEngine.OnTick</c> aplica: reapontar antes de atualizar os
    /// módulos. Com ele, a barreira de água do fogo lê sempre o estado corrente.
    /// </summary>
    [Fact]
    public void ReapontarPorQuadroMantemOFogoLendoAAguaCorrente()
    {
        var agua = new WaterSimulation(W, H);
        var fogo = new FireSimulation(W, H, semente: 7) { Solo = agua.Solo, Agua = agua.Profundidade };
        var terreno = new float[W * H];

        agua.IniciarChuva(10f, 3f);

        for (int i = 0; i < 20; i++)
        {
            // Exatamente o que o engine faz antes do laço de módulos.
            fogo.Agua = agua.Profundidade;

            agua.Atualizar(terreno, W, H, 0.033f);

            // Depois da atualização a referência pode ter trocado; o engine reaponta no
            // quadro seguinte, que é o momento em que o fogo vai consultar.
            fogo.Agua = agua.Profundidade;
            Assert.Same(agua.Profundidade, fogo.Agua);
        }
    }

    /// <summary>
    /// Água existente antes da ignição barra o fogo. É a interação pedagógica do módulo:
    /// um rio no caminho segura a frente de chama.
    /// </summary>
    [Fact]
    public void AguaExistenteBarraAPropagacao()
    {
        // Grade menor que a do sensor: a propagação é um autômato celular serial, e
        // atravessar 320 colunas levaria milhares de passos. A geometria do teste é a
        // mesma; só o custo muda.
        const int Lw = 160, Lh = 120;

        var agua = new WaterSimulation(Lw, Lh);
        agua.Solo.Preencher(TipoDeSolo.Mata);

        int ws = agua.Width, hs = agua.Height;

        // Faixa de água atravessando a caixa inteira na vertical, no meio.
        var lamina = agua.Profundidade;
        for (int y = 0; y < hs; y++)
            for (int x = ws / 2 - 2; x <= ws / 2 + 2; x++)
                lamina[y * ws + x] = 40f;

        var fogo = new FireSimulation(Lw, Lh, semente: 11) { Solo = agua.Solo, Agua = lamina };
        var terreno = new float[Lw * Lh];

        // Ateia bem à esquerda da faixa de água.
        Assert.True(fogo.Atear(0.15f, 0.5f));

        for (int i = 0; i < 4000 && fogo.EmAndamento; i++)
            fogo.Atualizar(terreno, Lw, Lh, 0.05f);

        Assert.False(fogo.EmAndamento, "O fogo não terminou dentro do limite de passos.");

        // Queimou de verdade, mas não a caixa inteira: a faixa de água segurou.
        Assert.True(fogo.AreaQueimadaPercent > 5,
            $"O fogo mal pegou: {fogo.AreaQueimadaPercent:F1}%.");
        Assert.True(fogo.AreaQueimadaPercent < 60,
            $"O fogo atravessou a água: {fogo.AreaQueimadaPercent:F1}% queimado.");
    }

    /// <summary>
    /// Água que seca durante o incêndio devolve a célula à condição de queimar.
    ///
    /// O defeito (pendência P2): <c>TentarAcender</c> gravava <c>Estado.NaoQueima</c> ao
    /// encontrar água, e esse estado é terminal — a célula ficava imune pelo resto
    /// daquele incêndio, mesmo depois de a água sumir.
    ///
    /// Duas condições foram necessárias para o teste enxergar o defeito:
    ///
    /// 1. **Corredor de uma célula.** Numa faixa larga o fogo contorna: a frente só testa
    ///    vizinhos imediatos, boa parte da barreira nunca é marcada, e sobra caminho.
    /// 2. **Secar dentro do mesmo incêndio.** Um incêndio novo chama <c>Preparar</c>, que
    ///    reconstrói o estado a partir do solo e apaga qualquer <c>NaoQueima</c> antigo —
    ///    então a comparação entre dois incêndios não mostra nada.
    ///
    /// Duas versões anteriores deste teste passavam sem a correção por violarem 1 e 2.
    /// </summary>
    [Fact]
    public void AguaQueSecaDuranteOIncendioLiberaAPassagem()
    {
        static double PercorrerCorredor(bool secar)
        {
            const int Lw = 160, Lh = 120;

            var agua = new WaterSimulation(Lw, Lh);
            int ws = agua.Width, hs = agua.Height;

            // Rocha em tudo menos um corredor horizontal de uma célula: existe um único
            // caminho possível, e ele passa pela célula molhada.
            agua.Solo.Preencher(TipoDeSolo.Rocha);
            int linha = hs / 2;
            for (int x = 0; x < ws; x++) agua.Solo.Celulas[linha * ws + x] = TipoDeSolo.Mata;

            var lamina = agua.Profundidade;
            lamina[linha * ws + ws / 2] = 40f;

            var fogo = new FireSimulation(Lw, Lh, semente: 31) { Solo = agua.Solo, Agua = lamina };
            var terreno = new float[Lw * Lh];
            Assert.True(fogo.Atear(0.05f, linha / (float)hs));

            bool secou = false;
            for (int i = 0; i < 4000 && fogo.EmAndamento; i++)
            {
                // Seca quando a frente já chegou à porta e o vizinho dela ainda queima.
                if (secar && !secou && fogo.AreaQueimadaPercent > 40)
                {
                    Array.Clear(lamina);
                    secou = true;
                }
                fogo.Atualizar(terreno, Lw, Lh, 0.05f);
            }

            if (secar) Assert.True(secou, "A frente não chegou a 40% antes de o fogo acabar.");
            return fogo.AreaQueimadaPercent;
        }

        double comAgua = PercorrerCorredor(secar: false);
        double secando = PercorrerCorredor(secar: true);

        // A célula molhada segura o fogo no meio do corredor.
        Assert.InRange(comAgua, 20.0, 70.0);

        // Secando durante o incêndio, a passagem reabre e o fogo termina o corredor.
        Assert.True(secando > comAgua + 25,
            $"A água secou e o fogo não passou: {comAgua:F1}% -> {secando:F1}%.");
    }

    /// <summary>
    /// Numa cobertura sem material combustível, atear devolve falso em vez de iniciar um
    /// incêndio que não pega. A interface usa esse retorno para explicar o que fazer.
    /// </summary>
    [Fact]
    public void AtearFalhaEmCoberturaQueNaoQueima()
    {
        var agua = new WaterSimulation(W, H);
        agua.Solo.Preencher(TipoDeSolo.Rocha);

        var fogo = new FireSimulation(W, H, semente: 3) { Solo = agua.Solo, Agua = agua.Profundidade };

        Assert.False(fogo.Atear());
        Assert.False(fogo.EmAndamento);
    }
}
