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

namespace CaixaInterativa.Rendering;

/// <summary>Um ponto do campo do sensor, em coordenadas normalizadas de 0 a 1.</summary>
public readonly record struct PontoDoCampo(float U, float V);

/// <summary>
/// Converte um clique na prévia para um ponto do campo do sensor.
///
/// <b>Por que isto existe como função pura.</b> A conversão atravessa quatro
/// transformações, e errar qualquer uma acende o fogo no lugar errado sem que nada
/// avise. Fora de um manipulador de mouse ela pode ser testada com números.
///
/// A cadeia, na ordem em que precisa ser desfeita:
///
/// <list type="number">
/// <item><b>Encaixe uniforme.</b> A prévia usa <c>Stretch="Uniform"</c>: a imagem cabe
/// dentro do controle preservando o aspecto, com tarjas nas sobras. Clique na tarja não
/// é clique no mapa.</item>
/// <item><b>Espelhamento.</b> A projeção física é espelhada para casar com a caixa, e a
/// prévia passou a espelhar junto — sem isso a prévia mostraria o inverso do que a turma
/// vê, e foi exatamente esse o defeito. O espelho precisa ser desfeito aqui.</item>
/// <item><b>Recorte da ROI.</b> O que a prévia mostra é um recorte do campo do sensor,
/// não o campo inteiro.</item>
/// <item><b>Normalização.</b> A simulação trabalha em 0 a 1 sobre o campo inteiro.</item>
/// </list>
///
/// <b>O que deliberadamente não é desfeito:</b> rotação, escala e deslocamento do
/// alinhamento da projeção. Eles são ajuste fino de alguns pixels e graus para casar a
/// imagem com a moldura de madeira; a prévia não os aplica, e aplicá-los aqui produziria
/// um ponto que não corresponde ao que está desenhado na prévia. O espelho é diferente:
/// não é ajuste fino, é uma inversão que troca esquerda por direita.
/// </summary>
public static class PontoNaPrevia
{
    /// <summary>
    /// Converte, ou devolve <c>false</c> quando o clique não caiu sobre o mapa.
    /// </summary>
    /// <param name="cliqueX">Posição do clique dentro do controle, em pixels.</param>
    /// <param name="cliqueY">Posição do clique dentro do controle, em pixels.</param>
    /// <param name="larguraDoControle">Largura visual do controle.</param>
    /// <param name="alturaDoControle">Altura visual do controle.</param>
    /// <param name="larguraDaImagem">Largura do bitmap exibido, em pixels.</param>
    /// <param name="alturaDaImagem">Altura do bitmap exibido, em pixels.</param>
    /// <param name="espelhadoNaHorizontal">A prévia está espelhada na horizontal.</param>
    /// <param name="espelhadoNaVertical">A prévia está espelhada na vertical.</param>
    /// <param name="roiEsquerda">Coluna do campo em que o recorte começa.</param>
    /// <param name="roiTopo">Linha do campo em que o recorte começa.</param>
    /// <param name="larguraCampo">Largura do campo do sensor, em pixels.</param>
    /// <param name="alturaCampo">Altura do campo do sensor, em pixels.</param>
    public static bool TentarConverter(
        double cliqueX, double cliqueY,
        double larguraDoControle, double alturaDoControle,
        int larguraDaImagem, int alturaDaImagem,
        bool espelhadoNaHorizontal, bool espelhadoNaVertical,
        int roiEsquerda, int roiTopo,
        int larguraCampo, int alturaCampo,
        out PontoDoCampo ponto)
    {
        ponto = default;

        if (larguraDaImagem <= 0 || alturaDaImagem <= 0) return false;
        if (larguraCampo <= 0 || alturaCampo <= 0) return false;
        if (larguraDoControle <= 0 || alturaDoControle <= 0) return false;

        // 1. Desfaz o encaixe uniforme.
        double escala = Math.Min(larguraDoControle / larguraDaImagem,
                                 alturaDoControle / alturaDaImagem);
        if (escala <= 0) return false;

        double margemX = (larguraDoControle - larguraDaImagem * escala) / 2;
        double margemY = (alturaDoControle - alturaDaImagem * escala) / 2;

        double x = (cliqueX - margemX) / escala;
        double y = (cliqueY - margemY) / escala;

        // Clique na tarja não é clique no mapa: recusa em vez de saturar na borda, para
        // não acender fogo numa quina só porque alguém errou o alvo.
        if (x < 0 || y < 0 || x >= larguraDaImagem || y >= alturaDaImagem) return false;

        // 2. Desfaz o espelho. A imagem exibida é o buffer espelhado, então o pixel do
        //    buffer sob o dedo é o refletido em torno do meio da imagem.
        if (espelhadoNaHorizontal) x = larguraDaImagem - x;
        if (espelhadoNaVertical) y = alturaDaImagem - y;

        // O reflexo de 0 é a largura, que já está fora do buffer por um pixel.
        x = Math.Clamp(x, 0, larguraDaImagem - 1);
        y = Math.Clamp(y, 0, alturaDaImagem - 1);

        // 3. Do recorte de volta para o campo inteiro.
        double campoX = roiEsquerda + x;
        double campoY = roiTopo + y;

        // 4. Normaliza. O clamp cobre uma ROI configurada além do campo — a prévia
        //    mostraria o recorte que o renderizador limitou, e sem isto o ponto sairia
        //    fora de 0 a 1.
        ponto = new PontoDoCampo(
            (float)Math.Clamp(campoX / larguraCampo, 0.0, 0.999999),
            (float)Math.Clamp(campoY / alturaCampo, 0.0, 0.999999));

        return true;
    }
}
