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
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// Trava de regressão visual do renderizador.
///
/// Os hashes abaixo foram capturados no commit 4d68a8e, **antes** da refatoração que
/// substituiu os parâmetros por camada (água, velocidade, sismo, dano, fogo) por uma
/// coleção genérica de <see cref="CamadaVisual"/>.
///
/// Eles não devem ser reescritos para fazer um teste passar. Se um deles mudar, ou a
/// composição visual mudou — e aí é regressão — ou a mudança é intencional e precisa de
/// uma justificativa registrada junto com o novo valor.
/// </summary>
public class RegressaoVisualTests
{
    /// <summary>
    /// SHA-256 do buffer BGRA de cada cenário, na ordem de
    /// <see cref="CenariosDeRegressao.Nomes"/>. Capturado em 4d68a8e.
    /// </summary>
    private static readonly string[] BaselineAntesDaRefatoracao =
    [
        "57C8D5B775C9EF9BF1D9E8AABA90ADC8D826B0AB721A3FF4C4809825C88C6E08", // 1. topografia (sem simulações)
        "4B6C9B174A965CB1B7CE5849BA42072C0350D857F3ADEC2458F2AE0CEDE17525", // 2. topografia + água
        "C29A9558AE91BAA98FDC3AC426A7021F3DC8411DFB04D30CD2D6E8270E38C5C8", // 3. topografia + terremoto
        "BCB40255C6A7BE12FE13F5B88D7B2B271C6DA8A139407C8F29CA1E7DD275A092", // 4. topografia + fogo
        "ECACD3EAB9064A83C480C3C175E5B0D04AEF054DA710C49622D6A36B368D0039", // 5. água + terremoto
        "A5540EFD29D9717353B7D1867308A3B7B936A604374976A0625DD7425509D86C", // 6. água + fogo
        "E0A8606C3839EEFADBD07759D53EAFFDF5A82C573347FC511342C5F58A3F3291", // 7. terremoto + fogo
        "DDD354088088E25FEF56029355E3C09BE6903FB1C49FD9AEAFA75119858222C1", // 8. todos ativos
    ];

    /// <summary>Tamanho esperado do buffer: 640×480 pixels × 4 bytes BGRA.</summary>
    private const int BytesEsperados = 640 * 480 * 4;

    public static TheoryData<int> Cenarios()
    {
        var dados = new TheoryData<int>();
        for (int i = 0; i < CenariosDeRegressao.Combinacoes.Length; i++) dados.Add(i);
        return dados;
    }

    [Theory]
    [MemberData(nameof(Cenarios))]
    public void ImagemRenderizadaNaoMudou(int cenario)
    {
        byte[] buffer = Renderizar(cenario);

        Assert.Equal(BytesEsperados, buffer.Length);
        Assert.Equal(BaselineAntesDaRefatoracao[cenario], CenariosDeRegressao.Hash(buffer));
    }

    /// <summary>
    /// Os oito cenários precisam produzir imagens diferentes entre si. Sem isto, um
    /// renderizador que ignorasse todas as camadas passaria em sete dos oito testes.
    /// </summary>
    [Fact]
    public void OitoCenariosProduzemOitoImagensDiferentes()
    {
        var vistos = new HashSet<string>();
        for (int i = 0; i < CenariosDeRegressao.Combinacoes.Length; i++)
            Assert.True(vistos.Add(CenariosDeRegressao.Hash(Renderizar(i))),
                        $"O cenário “{CenariosDeRegressao.Nomes[i]}” produziu uma imagem repetida.");
    }

    /// <summary>
    /// Renderizar duas vezes a mesma entrada precisa dar exatamente o mesmo resultado.
    /// O laço é paralelo; se houvesse dependência entre linhas, apareceria aqui.
    /// </summary>
    [Fact]
    public void RenderizacaoEDeterministica()
    {
        for (int i = 0; i < CenariosDeRegressao.Combinacoes.Length; i++)
            Assert.Equal(CenariosDeRegressao.Hash(Renderizar(i)),
                         CenariosDeRegressao.Hash(Renderizar(i)));
    }

    private static byte[] Renderizar(int cenario)
    {
        var terreno = CenariosDeRegressao.Terreno();

        // Renderizador novo por cenário: o buffer interno é reutilizado entre chamadas,
        // e queremos cada cenário isolado do anterior.
        var renderer = new TopographicRenderer();

        return renderer.Render(
            terreno,
            CenariosDeRegressao.LarguraSensor,
            CenariosDeRegressao.AlturaSensor,
            new ProjectionSettings(),
            new ProcessingSettings(),
            new RenderSettings(),
            CenariosDeRegressao.Camadas(cenario));
    }
}
