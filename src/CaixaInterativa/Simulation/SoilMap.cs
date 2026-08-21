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

namespace CaixaInterativa.Simulation;

/// <summary>
/// Tipos de cobertura do solo. A ordem vira o valor gravado no mapa, então novos
/// tipos entram no fim.
/// </summary>
public enum TipoDeSolo : byte
{
    Mata = 0,
    SoloArenoso = 1,
    SoloArgiloso = 2,
    SoloCompactado = 3,
    Impermeavel = 4,
    Desmatado = 5,
    Queimado = 6,
}

/// <summary>
/// Como cada superfície responde à chuva.
///
/// Os números não são medições de campo — são valores didáticos, escolhidos para que a
/// diferença entre uma bacia preservada e uma desmatada apareça numa aula de meia hora.
/// A ordem de grandeza segue a literatura (mata infiltra muito, asfalto não infiltra
/// nada), mas o objetivo é o estudante enxergar a relação, não prever uma cheia real.
/// </summary>
public readonly record struct PropriedadesDoSolo(
    string Nome,
    float InfiltracaoMmPorSegundo,
    float Rugosidade,
    float ResistenciaAErosao,
    byte CorR, byte CorG, byte CorB)
{
    private static PropriedadesDoSolo Calcular(TipoDeSolo tipo) => tipo switch
    {
        // Mata: raízes abrem caminho para a água e a serapilheira segura o escoamento.
        TipoDeSolo.Mata =>
            new("Mata", 3.2f, 0.85f, 0.95f, 34, 110, 48),

        // Areia infiltra rápido, mas não segura nada: erode com facilidade.
        TipoDeSolo.SoloArenoso =>
            new("Solo arenoso", 2.6f, 0.25f, 0.25f, 214, 190, 130),

        // Argila retém água na superfície; infiltra pouco e satura.
        TipoDeSolo.SoloArgiloso =>
            new("Solo argiloso", 0.9f, 0.45f, 0.60f, 178, 116, 72),

        // Pisoteio e maquinário fecham os poros do solo.
        TipoDeSolo.SoloCompactado =>
            new("Solo compactado", 0.35f, 0.30f, 0.50f, 146, 128, 104),

        // Asfalto e telhado: tudo o que cai vira escoamento.
        TipoDeSolo.Impermeavel =>
            new("Área urbana", 0.02f, 0.10f, 1.00f, 122, 122, 134),

        // Sem cobertura vegetal: infiltra menos e perde solo.
        TipoDeSolo.Desmatado =>
            new("Desmatado", 1.1f, 0.20f, 0.30f, 168, 150, 96),

        // Fogo cria uma crosta hidrofóbica; a água escorre por cima.
        TipoDeSolo.Queimado =>
            new("Queimado", 0.5f, 0.15f, 0.12f, 74, 62, 58),

        _ => Calcular(TipoDeSolo.SoloArenoso),
    };

    /// <summary>Propriedades de um tipo de solo.</summary>
    public static PropriedadesDoSolo De(TipoDeSolo tipo) => Rapido(tipo);

    /// <summary>
    /// Tabela pré-calculada, indexada pelo valor do enum.
    ///
    /// A simulação consulta as propriedades do solo em 77 mil células por substep, sete
    /// substeps por quadro, e o switch devolvia sempre os mesmos sete valores.
    ///
    /// Medido: 10,87 ms por quadro com o switch, 10,52 ms com a tabela — cerca de 3%.
    /// Menos do que parecia à primeira vista, porque o JIT já resolvia bem o switch
    /// sobre enum contíguo. Fica pela previsibilidade, não pelo ganho: o custo agora é
    /// um acesso a array, independente de quantos tipos de solo existirem no futuro.
    /// </summary>
    private static readonly PropriedadesDoSolo[] Tabela = ConstruirTabela();

    private static PropriedadesDoSolo[] ConstruirTabela()
    {
        var maior = 0;
        foreach (TipoDeSolo t in Enum.GetValues<TipoDeSolo>())
            if ((int)t > maior) maior = (int)t;

        var tabela = new PropriedadesDoSolo[maior + 1];
        foreach (TipoDeSolo t in Enum.GetValues<TipoDeSolo>())
            tabela[(int)t] = Calcular(t);
        return tabela;
    }

    /// <summary>Consulta rápida, no caminho quente da simulação.</summary>
    public static PropriedadesDoSolo Rapido(TipoDeSolo tipo)
    {
        int i = (int)tipo;
        return (uint)i < (uint)Tabela.Length ? Tabela[i] : Tabela[(int)TipoDeSolo.SoloArenoso];
    }

    public static readonly TipoDeSolo[] Todos =
    [
        TipoDeSolo.Mata,
        TipoDeSolo.SoloArenoso,
        TipoDeSolo.SoloArgiloso,
        TipoDeSolo.SoloCompactado,
        TipoDeSolo.Impermeavel,
        TipoDeSolo.Desmatado,
        TipoDeSolo.Queimado,
    ];
}

/// <summary>
/// Que superfície cobre cada pedaço do território.
///
/// Trabalha na mesma grade da simulação de água, para que cada célula consulte a
/// própria infiltração sem reamostrar a cada quadro.
///
/// A areia física não muda de tipo — é o professor quem pinta as regiões, ou aplica um
/// cenário pronto. Essa separação é proposital: o relevo vem das mãos dos estudantes,
/// a cobertura vem da decisão deles sobre o que fazer com aquele território.
/// </summary>
public sealed class SoilMap
{
    private readonly TipoDeSolo[] _celulas;

    public int Width { get; }
    public int Height { get; }
    public TipoDeSolo[] Celulas => _celulas;

    /// <summary>Tipo aplicado quando o mapa é limpo.</summary>
    public TipoDeSolo Padrao { get; set; } = TipoDeSolo.SoloArenoso;

    public SoilMap(int width, int height)
    {
        Width = width;
        Height = height;
        _celulas = new TipoDeSolo[width * height];
    }

    public TipoDeSolo Em(int x, int y) => _celulas[y * Width + x];

    public void Preencher(TipoDeSolo tipo)
    {
        Padrao = tipo;
        Array.Fill(_celulas, tipo);
    }

    /// <summary>Pinta um círculo, em coordenadas normalizadas de 0 a 1.</summary>
    public void Pintar(float u, float v, float raioRelativo, TipoDeSolo tipo)
    {
        int cx = (int)(u * Width);
        int cy = (int)(v * Height);
        int raio = Math.Max(1, (int)(raioRelativo * Width));
        int raio2 = raio * raio;

        int y0 = Math.Max(0, cy - raio), y1 = Math.Min(Height - 1, cy + raio);
        int x0 = Math.Max(0, cx - raio), x1 = Math.Min(Width - 1, cx + raio);

        for (int y = y0; y <= y1; y++)
        {
            int dy = y - cy;
            for (int x = x0; x <= x1; x++)
            {
                int dx = x - cx;
                if (dx * dx + dy * dy <= raio2) _celulas[y * Width + x] = tipo;
            }
        }
    }

    /// <summary>
    /// Aplica um tipo acima ou abaixo de uma altitude. É como se monta rapidamente uma
    /// bacia plausível: mata na encosta, cidade no fundo do vale — que é justamente
    /// onde as pessoas constroem, e por isso onde alaga.
    /// </summary>
    public void PintarPorAltitude(float[] terrenoMm, int larguraTerreno, int alturaTerreno,
                                  float limiteMm, bool acima, TipoDeSolo tipo)
    {
        for (int y = 0; y < Height; y++)
        {
            int sy = y * alturaTerreno / Height;
            for (int x = 0; x < Width; x++)
            {
                int sx = x * larguraTerreno / Width;
                float h = terrenoMm[sy * larguraTerreno + sx];
                bool aplica = acima ? h >= limiteMm : h < limiteMm;
                if (aplica) _celulas[y * Width + x] = tipo;
            }
        }
    }

    /// <summary>Quanto de cada tipo existe, em porcentagem da área.</summary>
    public Dictionary<TipoDeSolo, double> Composicao()
    {
        var contagem = new Dictionary<TipoDeSolo, int>();
        foreach (var c in _celulas)
            contagem[c] = contagem.GetValueOrDefault(c) + 1;

        var total = (double)_celulas.Length;
        return contagem.ToDictionary(p => p.Key, p => 100.0 * p.Value / total);
    }
}
