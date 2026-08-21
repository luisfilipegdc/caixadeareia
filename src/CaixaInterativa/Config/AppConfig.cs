// Caixa de Areia Interativa — sistema de projeção topográfica interativa
// Copyright (C) 2026 Luis Filipe Gomes de Carvalho
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

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaixaInterativa.Config;

public sealed class ProcessingSettings
{
    /// <summary>Leituras fora desta faixa sao tratadas como invalidas.
    /// Com near mode o Kinect v1 le a partir de ~400mm; sem ele, ~800mm.</summary>
    public ushort MinValidDepthMm { get; set; } = 400;
    public ushort MaxValidDepthMm { get; set; } = 2000;

    /// <summary>Faixa de relevo mapeada para cor, em mm em relacao ao plano-base.
    /// Negativo = escavado abaixo do nivel da areia plana.</summary>
    public float MinHeightMm { get; set; } = -80f;
    public float MaxHeightMm { get; set; } = 120f;

    /// <summary>Fator do filtro exponencial em regime normal. Menor = mais estavel, mais lento.</summary>
    public float SmoothingAlpha { get; set; } = 0.15f;

    /// <summary>Fator usado quando a mudanca ultrapassa <see cref="JumpThresholdMm"/>.</summary>
    public float FastAlpha { get; set; } = 0.65f;

    /// <summary>Acima disto assumimos movimento real (mao, pa) em vez de ruido.</summary>
    public float JumpThresholdMm { get; set; } = 25f;

    /// <summary>Raio do box blur. 0 desliga. 2-4 costuma ser o ponto certo para areia.</summary>
    public int SpatialBlurRadius { get; set; } = 3;
}

public sealed class RenderSettings
{
    /// <summary>Intervalo entre curvas de nivel, em mm. 0 desliga as curvas.</summary>
    public float ContourIntervalMm { get; set; } = 15f;

    /// <summary>Opacidade das curvas de nivel, 0 a 1.</summary>
    public float ContourOpacity { get; set; } = 0.55f;

    /// <summary>Destaca uma curva mais forte a cada N intervalos (curva mestra).</summary>
    public int MajorContourEvery { get; set; } = 5;

    /// <summary>Sombreamento de relevo. Da volume ao mapa e ajuda o aluno a ler a inclinacao.</summary>
    public bool HillshadeEnabled { get; set; } = true;
    public float HillshadeStrength { get; set; } = 0.35f;
}

/// <summary>
/// Alinhamento da imagem projetada sobre a caixa fisica. Puramente 2D (escala,
/// deslocamento, rotacao, espelhamento) - suficiente quando o projetor esta
/// aproximadamente perpendicular a caixa.
/// </summary>
public sealed class ProjectionSettings
{
    public int ScreenIndex { get; set; } = 1;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double ScaleX { get; set; } = 1.0;
    public double ScaleY { get; set; } = 1.0;
    public double RotationDegrees { get; set; }
    public bool FlipHorizontal { get; set; }
    public bool FlipVertical { get; set; }

    /// <summary>Recorte da area util dentro do quadro de profundidade, em pixels do sensor.</summary>
    public int RoiLeft { get; set; }
    public int RoiTop { get; set; }
    public int RoiRight { get; set; } = 640;
    public int RoiBottom { get; set; } = 480;
}

public sealed class SensorSettings
{
    /// <summary>"kinect" ou "simulador".</summary>
    public string Source { get; set; } = "simulador";
    public bool NearMode { get; set; } = true;

    /// <summary>Angulo do motor de inclinacao em graus. null = nao mexer no motor.</summary>
    public int? TiltAngle { get; set; }
}

public sealed class AppConfig
{
    public SensorSettings Sensor { get; set; } = new();
    public ProcessingSettings Processing { get; set; } = new();
    public RenderSettings Render { get; set; } = new();
    public ProjectionSettings Projection { get; set; } = new();

    [JsonIgnore]
    public static string DefaultPath => Path.Combine(
        AppContext.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppConfig Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path)) return new AppConfig();
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), JsonOptions) ?? new AppConfig();
        }
        catch
        {
            // Config corrompida nunca deve impedir o app de abrir - numa sala de aula
            // isso significaria aula perdida. Voltamos aos padroes silenciosamente.
            return new AppConfig();
        }
    }

    public void Save(string? path = null)
    {
        path ??= DefaultPath;
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
