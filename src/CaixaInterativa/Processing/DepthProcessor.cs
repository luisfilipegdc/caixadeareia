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

namespace CaixaInterativa.Processing;

/// <summary>
/// Converte quadros de profundidade brutos num campo de alturas estavel, em milimetros
/// acima do plano-base calibrado.
///
/// O Kinect v1 tem ruido de ~2-4mm nessa distancia e produz pixels invalidos nas bordas
/// de objetos. Sem tratamento, a projecao "ferve": as curvas de nivel piscam mesmo com a
/// areia parada. As tres etapas abaixo (buracos, tempo, espaco) existem exatamente para
/// isso, nesta ordem.
/// </summary>
public sealed class DepthProcessor
{
    private readonly int _width;
    private readonly int _height;

    private float[] _basePlaneMm;      // distancia sensor->fundo, por pixel
    private bool[] _baseValid;         // este pixel chegou a ser medido na calibracao?
    private float[] _smoothed;         // altura em mm apos suavizacao temporal
    private bool[] _everValid;         // ja tivemos alguma leitura boa neste pixel?
    private readonly float[] _scratch;

    private readonly object _gate = new();

    // Estado da calibracao em andamento
    private double[]? _calibAccum;
    private int[]? _calibCounts;
    private int _calibFramesRemaining;

    /// <summary>Numero minimo de quadros de calibracao em que um pixel precisa ter lido
    /// para ganhar um plano-base. E' uma contagem absoluta, nao uma fracao do total: com
    /// os 60 quadros padrao equivale a 8%, mas uma calibracao mais curta exige os mesmos
    /// 5. Abaixo disso a leitura e' intermitente demais para virar referencia.</summary>
    private const int MinCalibrationSamples = 5;

    public int Width => _width;
    public int Height => _height;
    public bool IsCalibrated { get; private set; }
    public bool IsCalibrating => _calibFramesRemaining > 0;

    /// <summary>Percentual da area do sensor que ganhou plano-base valido. Abaixo de ~80%
    /// numa caixa de areia indica sensor mal posicionado, sol na cena ou areia molhada.</summary>
    public double CoveragePercent { get; private set; }

    /// <summary>Disparado ao terminar a captura do plano-base. Argumento: distancia media em mm.</summary>
    public event Action<double>? CalibrationCompleted;

    public ProcessingSettings Settings { get; set; } = new();

    public DepthProcessor(int width, int height)
    {
        _width = width;
        _height = height;
        int n = width * height;
        _basePlaneMm = new float[n];
        _baseValid = new bool[n];
        _smoothed = new float[n];
        _everValid = new bool[n];
        _scratch = new float[n];
    }

    /// <summary>
    /// Inicia a captura do plano-base. Nivele a areia antes de chamar.
    /// Guardamos a distancia por pixel, nao um unico numero, para que uma caixa levemente
    /// torta ou um sensor nao perfeitamente perpendicular nao vire um gradiente falso no mapa.
    /// </summary>
    public void BeginBaseCalibration(int frames = 60)
    {
        lock (_gate)
        {
            _calibAccum = new double[_width * _height];
            _calibCounts = new int[_width * _height];
            _calibFramesRemaining = Math.Max(1, frames);
        }
    }

    public void ProcessFrame(RawDepthFrame frame, float[] outputHeightsMm)
    {
        if (frame.Width != _width || frame.Height != _height)
            throw new ArgumentException($"Quadro {frame.Width}x{frame.Height} incompativel com {_width}x{_height}.");

        lock (_gate)
        {
            if (_calibFramesRemaining > 0)
            {
                AccumulateCalibration(frame);
                Array.Clear(outputHeightsMm);
                return;
            }
        }

        var s = Settings;
        var depth = frame.Data;
        int n = depth.Length;

        // Etapa 1 - profundidade para altura, com preenchimento de buracos.
        for (int i = 0; i < n; i++)
        {
            ushort d = depth[i];

            // Sem plano-base medido neste pixel nao ha altura possivel. Ele fica
            // permanentemente no nivel zero em vez de virar pico: usar a media global
            // como referencia faria a primeira leitura real render centenas de mm de
            // "relevo" e saturar o mapa em branco. Acontece nos cantos com sombra de
            // infravermelho permanente, que toda caixa real tem.
            if (!_baseValid[i]) { _smoothed[i] = 0f; continue; }

            bool valid = d >= s.MinValidDepthMm && d <= s.MaxValidDepthMm;

            if (!valid)
            {
                // Pixel invalido: mantemos o ultimo valor bom. Zerar aqui criaria
                // crateras piscando nas bordas das maos e dos montes de areia.
                if (!_everValid[i]) _smoothed[i] = 0f;
                continue;
            }

            float raw = IsCalibrated ? _basePlaneMm[i] - d : 0f;
            raw = Math.Clamp(raw, s.MinHeightMm, s.MaxHeightMm);

            if (!_everValid[i])
            {
                _smoothed[i] = raw;
                _everValid[i] = true;
                continue;
            }

            // Etapa 2 - suavizacao temporal com alfa adaptativo.
            // Uma mao entrando na caixa produz um salto grande e legitimo; se usassemos
            // o alfa lento dela, a mao apareceria como um borrao arrastado. Acima do
            // limiar assumimos mudanca real e respondemos rapido.
            float delta = raw - _smoothed[i];
            float alpha = Math.Abs(delta) > s.JumpThresholdMm ? s.FastAlpha : s.SmoothingAlpha;
            _smoothed[i] += delta * alpha;
        }

        // Etapa 3 - suavizacao espacial (box blur separavel).
        if (s.SpatialBlurRadius > 0)
            BoxBlur(_smoothed, outputHeightsMm, _scratch, _width, _height, s.SpatialBlurRadius);
        else
            Array.Copy(_smoothed, outputHeightsMm, n);
    }

    private void AccumulateCalibration(RawDepthFrame frame)
    {
        var accum = _calibAccum!;
        var counts = _calibCounts!;
        var s = Settings;
        var depth = frame.Data;

        for (int i = 0; i < depth.Length; i++)
        {
            ushort d = depth[i];
            if (d < s.MinValidDepthMm || d > s.MaxValidDepthMm) continue;
            accum[i] += d;
            counts[i]++;
        }

        if (--_calibFramesRemaining > 0) return;

        double sum = 0;
        int measured = 0;
        for (int i = 0; i < accum.Length; i++)
        {
            // Exige um minimo de amostras: um pixel que leu 1 vez em 60 quadros esta na
            // borda do alcance e seu "plano-base" seria ruido promovido a referencia.
            if (counts[i] >= MinCalibrationSamples)
            {
                _basePlaneMm[i] = (float)(accum[i] / counts[i]);
                _baseValid[i] = true;
                sum += _basePlaneMm[i];
                measured++;
            }
            else
            {
                _basePlaneMm[i] = 0f;
                _baseValid[i] = false;
            }
        }

        float fallback = measured > 0 ? (float)(sum / measured) : 0f;
        CoveragePercent = 100.0 * measured / _basePlaneMm.Length;
        AverageDistanceMm = fallback;

        Array.Clear(_smoothed);
        Array.Clear(_everValid);
        IsCalibrated = true;
        _calibAccum = null;
        _calibCounts = null;

        CalibrationCompleted?.Invoke(fallback);
    }

    private static void BoxBlur(float[] src, float[] dst, float[] tmp, int w, int h, int radius)
    {
        int window = radius * 2 + 1;
        float inv = 1f / window;

        // Passada horizontal
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            float sum = 0;
            for (int k = -radius; k <= radius; k++)
                sum += src[row + Math.Clamp(k, 0, w - 1)];

            for (int x = 0; x < w; x++)
            {
                tmp[row + x] = sum * inv;
                int add = Math.Clamp(x + radius + 1, 0, w - 1);
                int rem = Math.Clamp(x - radius, 0, w - 1);
                sum += src[row + add] - src[row + rem];
            }
        }

        // Passada vertical
        for (int x = 0; x < w; x++)
        {
            float sum = 0;
            for (int k = -radius; k <= radius; k++)
                sum += tmp[Math.Clamp(k, 0, h - 1) * w + x];

            for (int y = 0; y < h; y++)
            {
                dst[y * w + x] = sum * inv;
                int add = Math.Clamp(y + radius + 1, 0, h - 1);
                int rem = Math.Clamp(y - radius, 0, h - 1);
                sum += tmp[add * w + x] - tmp[rem * w + x];
            }
        }
    }

    public void ResetCalibration()
    {
        lock (_gate)
        {
            IsCalibrated = false;
            CoveragePercent = 0;
            Array.Clear(_basePlaneMm);
            Array.Clear(_baseValid);
            Array.Clear(_smoothed);
            Array.Clear(_everValid);
            _calibAccum = null;
            _calibCounts = null;
            _calibFramesRemaining = 0;
        }
    }

    /// <summary>Distancia media do plano-base medido, para exibir e salvar.</summary>
    public double AverageDistanceMm { get; private set; }

    /// <summary>Empacota a calibracao atual para gravar em disco.</summary>
    public CalibrationData? Export(string sourceName)
    {
        lock (_gate)
        {
            if (!IsCalibrated) return null;
            return new CalibrationData
            {
                Width = _width,
                Height = _height,
                BasePlaneMm = (float[])_basePlaneMm.Clone(),
                BaseValid = (bool[])_baseValid.Clone(),
                CoveragePercent = CoveragePercent,
                AverageDistanceMm = AverageDistanceMm,
                SavedAt = DateTime.Now,
                SourceName = sourceName
            };
        }
    }

    /// <summary>
    /// Restaura uma calibracao salva. Devolve false se as dimensoes nao baterem —
    /// os indices nao corresponderiam e o mapa sairia embaralhado.
    /// </summary>
    public bool Import(CalibrationData dados)
    {
        if (dados.Width != _width || dados.Height != _height) return false;
        if (dados.BasePlaneMm.Length != _basePlaneMm.Length) return false;
        if (dados.BaseValid.Length != _baseValid.Length) return false;

        lock (_gate)
        {
            _basePlaneMm = (float[])dados.BasePlaneMm.Clone();
            _baseValid = (bool[])dados.BaseValid.Clone();
            CoveragePercent = dados.CoveragePercent;
            AverageDistanceMm = dados.AverageDistanceMm;
            Array.Clear(_smoothed);
            Array.Clear(_everValid);
            _calibFramesRemaining = 0;
            IsCalibrated = true;
        }
        return true;
    }
}
