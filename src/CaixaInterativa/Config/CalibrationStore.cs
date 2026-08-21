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

namespace CaixaInterativa.Config;

/// <summary>
/// Uma calibração completa do plano-base, pronta para salvar ou restaurar.
/// </summary>
public sealed class CalibrationData
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required float[] BasePlaneMm { get; init; }
    public required bool[] BaseValid { get; init; }
    public double CoveragePercent { get; init; }
    public double AverageDistanceMm { get; init; }
    public DateTime SavedAt { get; init; }
    public string SourceName { get; init; } = "";
}

/// <summary>
/// Guarda a calibração em disco para que o professor não precise recalibrar a cada aula.
///
/// Sem isto, abrir o programa exige nivelar a areia e capturar o plano-base antes de
/// qualquer uso — o passo mais demorado do fluxo, e o mais fácil de fazer errado com a
/// turma esperando. Com a calibração salva, abrir o programa é abrir e usar.
///
/// Formato binário próprio em vez de JSON: são 307.200 floats mais 307.200 booleanos.
/// Em JSON isso viraria um arquivo de vários megabytes que demora a ler; em binário
/// são ~1,5 MB lidos de uma vez.
/// </summary>
public static class CalibrationStore
{
    private const uint Assinatura = 0x43414958;  // "CAIX"
    private const int Versao = 1;

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "calibracao.dat");

    public static bool Exists(string? path = null) => File.Exists(path ?? DefaultPath);

    public static void Save(CalibrationData dados, string? path = null)
    {
        path ??= DefaultPath;

        // Escreve num temporário e move por cima: se faltar energia no meio da
        // gravação, a calibração anterior continua íntegra em vez de virar um
        // arquivo truncado que não carrega.
        string temporario = path + ".tmp";

        using (var fs = new FileStream(temporario, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(fs))
        {
            w.Write(Assinatura);
            w.Write(Versao);
            w.Write(dados.Width);
            w.Write(dados.Height);
            w.Write(dados.CoveragePercent);
            w.Write(dados.AverageDistanceMm);
            w.Write(dados.SavedAt.ToBinary());
            w.Write(dados.SourceName ?? "");

            foreach (float v in dados.BasePlaneMm) w.Write(v);

            // Empacota os booleanos em bits: 307.200 bytes viram 38.400.
            byte acumulador = 0;
            int bits = 0;
            foreach (bool v in dados.BaseValid)
            {
                if (v) acumulador |= (byte)(1 << bits);
                if (++bits == 8) { w.Write(acumulador); acumulador = 0; bits = 0; }
            }
            if (bits > 0) w.Write(acumulador);
        }

        File.Move(temporario, path, overwrite: true);
    }

    /// <summary>
    /// Carrega a calibração salva. Devolve null se não existir, estiver corrompida ou
    /// tiver sido gravada para uma resolução diferente da atual.
    /// </summary>
    public static CalibrationData? Load(int larguraEsperada, int alturaEsperada, string? path = null)
    {
        path ??= DefaultPath;
        if (!File.Exists(path)) return null;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var r = new BinaryReader(fs);

            if (r.ReadUInt32() != Assinatura) return null;
            if (r.ReadInt32() != Versao) return null;

            int w = r.ReadInt32();
            int h = r.ReadInt32();

            // Calibração de outra resolução não serve: os índices não correspondem.
            if (w != larguraEsperada || h != alturaEsperada) return null;

            double cobertura = r.ReadDouble();
            double distancia = r.ReadDouble();
            var quando = DateTime.FromBinary(r.ReadInt64());
            string fonte = r.ReadString();

            int n = w * h;
            var plano = new float[n];
            for (int i = 0; i < n; i++) plano[i] = r.ReadSingle();

            var valido = new bool[n];
            int bytesDeBits = (n + 7) / 8;
            var bits = r.ReadBytes(bytesDeBits);
            if (bits.Length < bytesDeBits) return null;
            for (int i = 0; i < n; i++)
                valido[i] = (bits[i / 8] & (1 << (i % 8))) != 0;

            return new CalibrationData
            {
                Width = w,
                Height = h,
                BasePlaneMm = plano,
                BaseValid = valido,
                CoveragePercent = cobertura,
                AverageDistanceMm = distancia,
                SavedAt = quando,
                SourceName = fonte
            };
        }
        catch
        {
            // Arquivo corrompido nunca deve impedir o programa de abrir — numa sala de
            // aula isso significaria aula perdida. Segue sem calibração.
            return null;
        }
    }

    public static void Delete(string? path = null)
    {
        path ??= DefaultPath;
        try { if (File.Exists(path)) File.Delete(path); } catch { /* nada a fazer */ }
    }

    /// <summary>Quando a calibração salva foi feita, ou null se não houver.</summary>
    public static DateTime? SavedAt(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path)) return null;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var r = new BinaryReader(fs);
            if (r.ReadUInt32() != Assinatura) return null;
            if (r.ReadInt32() != Versao) return null;
            r.ReadInt32(); r.ReadInt32(); r.ReadDouble(); r.ReadDouble();
            return DateTime.FromBinary(r.ReadInt64());
        }
        catch { return null; }
    }
}
