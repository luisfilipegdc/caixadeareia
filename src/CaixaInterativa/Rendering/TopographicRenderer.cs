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

namespace CaixaInterativa.Rendering;

/// <summary>
/// Transforma o campo de alturas num mapa topografico BGRA: rampa de cor por altitude,
/// curvas de nivel e sombreamento de relevo.
///
/// Renderizacao em CPU, nao em GPU. A 640x480 sao ~307k pixels por quadro, o que a 30fps
/// cabe folgado num nucleo moderno com Parallel.For. Trocar por um shader so se justifica
/// quando entrar a simulacao de agua, que e' iterativa e realmente pede GPU.
/// </summary>
public sealed class TopographicRenderer
{
    private readonly record struct Stop(float T, byte R, byte G, byte B);

    /// <summary>
    /// Rampa hipsometrica classica: azul profundo nas escavacoes, areia na linha d'agua,
    /// verde nas planicies, marrom nas encostas, branco nos picos. E' a convencao dos atlas
    /// escolares justamente porque o aluno ja chega sabendo ler.
    /// </summary>
    private static readonly Stop[] Palette =
    [
        new(0.00f,   4,  20,  70),   // fundo de oceano
        new(0.12f,  10,  75, 175),   // agua profunda
        new(0.22f,  40, 150, 220),   // agua rasa
        new(0.27f, 120, 200, 235),   // linha d'agua
        new(0.30f, 238, 226, 168),   // praia
        new(0.36f,  90, 165,  75),   // planicie
        new(0.50f, 130, 190,  80),   // colina baixa
        new(0.63f, 220, 205,  95),   // colina alta
        new(0.74f, 178, 130,  65),   // encosta
        new(0.86f, 135, 115, 105),   // rocha
        new(0.94f, 205, 200, 198),   // rocha clara
        new(1.00f, 255, 255, 255),   // neve
    ];

    private byte[] _buffer = [];
    private int _bufferWidth;
    private int _bufferHeight;

    public int Width => _bufferWidth;
    public int Height => _bufferHeight;
    public int Stride => _bufferWidth * 4;
    public byte[] Buffer => _buffer;

    public byte[] Render(
        float[] heightsMm,
        int fieldWidth,
        int fieldHeight,
        ProjectionSettings projection,
        ProcessingSettings processing,
        RenderSettings render,
        float[]? waterMm = null,
        int waterWidth = 0,
        int waterHeight = 0,
        float[]? waterSpeed = null)
    {
        int left = Math.Clamp(projection.RoiLeft, 0, fieldWidth - 1);
        int top = Math.Clamp(projection.RoiTop, 0, fieldHeight - 1);
        int right = Math.Clamp(projection.RoiRight, left + 1, fieldWidth);
        int bottom = Math.Clamp(projection.RoiBottom, top + 1, fieldHeight);

        int w = right - left;
        int h = bottom - top;

        if (_bufferWidth != w || _bufferHeight != h)
        {
            _buffer = new byte[w * h * 4];
            _bufferWidth = w;
            _bufferHeight = h;
        }

        float minH = processing.MinHeightMm;
        float range = Math.Max(1f, processing.MaxHeightMm - minH);
        float interval = render.ContourIntervalMm;
        bool contours = interval > 0.01f;
        int majorEvery = Math.Max(1, render.MajorContourEvery);

        var buffer = _buffer;

        Parallel.For(0, h, y =>
        {
            int srcRow = (y + top) * fieldWidth + left;
            int srcRowBelow = (Math.Min(y + 1, h - 1) + top) * fieldWidth + left;
            int dstRow = y * w * 4;

            for (int x = 0; x < w; x++)
            {
                float hm = heightsMm[srcRow + x];
                float t = Math.Clamp((hm - minH) / range, 0f, 1f);

                Sample(t, out float r, out float g, out float b);

                if (render.HillshadeEnabled)
                {
                    // Gradiente por diferencas finitas simples. Nao precisa ser exato:
                    // o objetivo e' dar leitura de inclinacao, nao fotorrealismo.
                    int xr = Math.Min(x + 1, w - 1);
                    float dzdx = heightsMm[srcRow + xr] - hm;
                    float dzdy = heightsMm[srcRowBelow + x] - hm;

                    // Luz vinda do noroeste, a convencao cartografica.
                    float shade = (-dzdx - dzdy) * 0.04f;
                    shade = Math.Clamp(shade, -1f, 1f) * render.HillshadeStrength;

                    float factor = 1f + shade;
                    r *= factor;
                    g *= factor;
                    b *= factor;
                }

                if (contours)
                {
                    int band = (int)MathF.Floor(hm / interval);
                    int xr = Math.Min(x + 1, w - 1);
                    int bandRight = (int)MathF.Floor(heightsMm[srcRow + xr] / interval);
                    int bandDown = (int)MathF.Floor(heightsMm[srcRowBelow + x] / interval);

                    if (band != bandRight || band != bandDown)
                    {
                        // Curva mestra a cada N intervalos: mais escura e mais visivel,
                        // para o aluno contar altitude sem precisar contar cada linha.
                        bool major = band % majorEvery == 0;
                        float opacity = render.ContourOpacity * (major ? 1.0f : 0.6f);
                        r *= 1f - opacity;
                        g *= 1f - opacity;
                        b *= 1f - opacity;
                    }
                }

                // A água entra por cima do terreno, não substituindo a cor: assim o
                // aluno continua vendo o relevo por baixo e entende que a água está
                // *sobre* o que ele construiu.
                if (waterMm is not null && waterWidth > 0 && waterHeight > 0)
                {
                    float prof = AmostrarBilinear(waterMm, waterWidth, waterHeight,
                                                  (x + left) / (float)fieldWidth,
                                                  (y + top) / (float)fieldHeight);

                    if (prof > 0.25f)
                    {
                        // Opacidade cresce rápido nos primeiros milímetros e satura:
                        // uma poça rasa precisa ser visível, e depois de ~35mm mais
                        // profundidade não muda o que se enxerga.
                        float cobertura = MathF.Min(1f, prof / 35f);
                        float alfa = 0.30f + 0.55f * cobertura;

                        // Raso esverdeado, fundo azul-escuro — a mesma leitura de um
                        // mapa náutico.
                        float wr = 40f - 32f * cobertura;
                        float wg = 150f - 92f * cobertura;
                        float wb = 210f - 40f * cobertura;

                        if (waterSpeed is not null)
                        {
                            // Correnteza clareia: distingue um rio correndo de um
                            // lago parado, que é a diferença que a aula quer mostrar.
                            float v = AmostrarBilinear(waterSpeed, waterWidth, waterHeight,
                                                       (x + left) / (float)fieldWidth,
                                                       (y + top) / (float)fieldHeight);
                            float espuma = MathF.Min(1f, v / 260f);
                            wr += 150f * espuma;
                            wg += 80f * espuma;
                            wb += 35f * espuma;
                        }

                        r = r * (1f - alfa) + wr * alfa;
                        g = g * (1f - alfa) + wg * alfa;
                        b = b * (1f - alfa) + wb * alfa;
                    }
                }

                int i = dstRow + x * 4;
                buffer[i + 0] = ClampByte(b);
                buffer[i + 1] = ClampByte(g);
                buffer[i + 2] = ClampByte(r);
                buffer[i + 3] = 255;
            }
        });

        return buffer;
    }

    private static void Sample(float t, out float r, out float g, out float b)
    {
        var palette = Palette;

        for (int i = 1; i < palette.Length; i++)
        {
            if (t > palette[i].T) continue;

            var a = palette[i - 1];
            var c = palette[i];
            float span = c.T - a.T;
            float k = span <= 0f ? 0f : (t - a.T) / span;

            r = a.R + (c.R - a.R) * k;
            g = a.G + (c.G - a.G) * k;
            b = a.B + (c.B - a.B) * k;
            return;
        }

        var last = palette[^1];
        r = last.R;
        g = last.G;
        b = last.B;
    }

    /// <summary>
    /// Amostra a grade da simulação, que roda em metade da resolução do sensor.
    /// Bilinear, e não vizinho mais próximo: com nearest a borda de uma poça vira uma
    /// escada visível de blocos de 2x2 pixels na projeção.
    /// </summary>
    private static float AmostrarBilinear(float[] campo, int w, int h, float u, float v)
    {
        float fx = u * w - 0.5f;
        float fy = v * h - 0.5f;

        int x0 = (int)MathF.Floor(fx);
        int y0 = (int)MathF.Floor(fy);
        float tx = fx - x0;
        float ty = fy - y0;

        int x1 = Math.Clamp(x0 + 1, 0, w - 1);
        int y1 = Math.Clamp(y0 + 1, 0, h - 1);
        x0 = Math.Clamp(x0, 0, w - 1);
        y0 = Math.Clamp(y0, 0, h - 1);

        float a = campo[y0 * w + x0], b = campo[y0 * w + x1];
        float c = campo[y1 * w + x0], d = campo[y1 * w + x1];

        return (a + (b - a) * tx) * (1f - ty) + (c + (d - c) * tx) * ty;
    }

    private static byte ClampByte(float v) => (byte)Math.Clamp(v, 0f, 255f);
}
