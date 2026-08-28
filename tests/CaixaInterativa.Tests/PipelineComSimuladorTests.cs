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
using CaixaInterativa.Depth;
using CaixaInterativa.Processing;
using CaixaInterativa.Rendering;
using CaixaInterativa.Simulation;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// Percorre a pipeline inteira com o <see cref="SimulatedDepthSource"/>: captura,
/// calibração, simulações e composição das camadas até o buffer BGRA.
///
/// É tudo o que o <c>SandboxEngine</c> faz, menos a parte de WPF — que precisa de um
/// <c>Dispatcher</c> e não cabe num teste. Serve para provar que a montagem de camadas
/// funciona com estado real de simulação, e não só com os campos sintéticos.
/// </summary>
public class PipelineComSimuladorTests
{
    [Fact]
    public void PipelineCompletaProduzImagemComAsCamadasDosModulos()
    {
        using var fonte = new SimulatedDepthSource();
        var quadros = new List<RawDepthFrame>();
        using var chegou = new ManualResetEventSlim(false);

        void AoChegar(RawDepthFrame f)
        {
            lock (quadros)
            {
                if (quadros.Count < 40) quadros.Add(f);
                if (quadros.Count >= 40) chegou.Set();
            }
        }

        fonte.FrameArrived += AoChegar;
        fonte.Start();
        bool recebeu = chegou.Wait(TimeSpan.FromSeconds(15));
        fonte.Stop();
        fonte.FrameArrived -= AoChegar;

        Assert.True(recebeu, "O simulador não entregou quadros suficientes.");

        var processor = new DepthProcessor(fonte.Width, fonte.Height);
        var alturas = new float[fonte.Width * fonte.Height];

        // Calibra com os primeiros 30 quadros, como o app faz ao ligar.
        processor.BeginBaseCalibration(30);
        for (int i = 0; i < 30; i++) processor.ProcessFrame(quadros[i], alturas);

        Assert.True(processor.IsCalibrated);
        Assert.True(processor.CoveragePercent > 90,
                    $"Cobertura de {processor.CoveragePercent:F1}% no simulador.");

        processor.ProcessFrame(quadros[35], alturas);

        // Estado real de simulação: chuva de verdade sobre o relevo lido.
        var agua = new WaterSimulation(fonte.Width, fonte.Height);
        agua.IniciarChuva(12f, 6f);
        for (int i = 0; i < 10; i++) agua.Atualizar(alturas, fonte.Width, fonte.Height, 0.033f);

        Assert.True(agua.VolumeLitros > 0, "A chuva não colocou água na caixa.");

        // Coleta na mesma ordem do SandboxEngine.
        var camadas = new List<CamadaVisual>(4);
        camadas.AddRange(agua.Camadas);

        var renderer = new TopographicRenderer();
        byte[] pixels = renderer.Render(
            alturas, fonte.Width, fonte.Height,
            new ProjectionSettings(), new ProcessingSettings(), new RenderSettings(),
            camadas);

        Assert.Equal(fonte.Width * fonte.Height * 4, pixels.Length);

        // A imagem com água tem de ser diferente da mesma cena sem camada nenhuma —
        // senão a camada não chegou ao renderizador.
        byte[] semAgua = new TopographicRenderer().Render(
            alturas, fonte.Width, fonte.Height,
            new ProjectionSettings(), new ProcessingSettings(), new RenderSettings(),
            null);

        Assert.NotEqual(CenariosDeRegressao.Hash(semAgua), CenariosDeRegressao.Hash(pixels));

        // Todo pixel opaco: alfa é o quarto byte de cada grupo BGRA.
        for (int i = 3; i < pixels.Length; i += 4) Assert.Equal(255, pixels[i]);
    }
}
