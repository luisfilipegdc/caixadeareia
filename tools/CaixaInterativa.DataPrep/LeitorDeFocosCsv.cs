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

using System.Globalization;

namespace CaixaInterativa.DataPrep;

/// <summary>Uma linha que não pôde ser lida, com o motivo.</summary>
public sealed record LinhaRejeitada(int Numero, string Motivo);

/// <summary>O que saiu de uma leitura: o que deu certo e o que não deu.</summary>
public sealed record ResultadoDaLeitura(
    IReadOnlyList<FocoDeCalor> Focos,
    IReadOnlyList<LinhaRejeitada> Rejeitadas);

/// <summary>
/// Lê o CSV de focos de calor do INPE.
///
/// Escrito à mão em vez de trazer uma biblioteca de CSV: o arquivo é regular, sem campos
/// com aspas nem vírgulas embutidas, e o projeto tem por norma não adquirir dependência
/// que não precisa. São ~40 linhas de laço contra um pacote NuGet a manter.
///
/// <b>Cultura invariante em tudo.</b> O INPE publica número com ponto decimal e data
/// ISO. Numa máquina configurada em pt-BR, <c>double.Parse</c> sem cultura leria
/// <c>0.77</c> como <c>77</c> — silenciosamente, sem exceção. É a classe de bug que só
/// aparece na máquina de outra pessoa.
/// </summary>
public static class LeitorDeFocosCsv
{
    /// <summary>Colunas que precisam existir. O resto do cabeçalho é ignorado.</summary>
    public static readonly string[] ColunasObrigatorias =
    [
        "data_hora_gmt", "municipio", "estado", "bioma",
        "numero_dias_sem_chuva", "precipitacao", "risco_fogo", "frp",
    ];

    /// <summary>
    /// Lê o conteúdo inteiro. Devolve os focos válidos e a lista do que foi rejeitado —
    /// rejeição silenciosa esconderia mudança de formato na origem.
    /// </summary>
    /// <exception cref="FormatException">Se faltar coluna obrigatória no cabeçalho.</exception>
    public static ResultadoDaLeitura Ler(TextReader entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);

        string? cabecalho = entrada.ReadLine()
            ?? throw new FormatException("O arquivo está vazio: não há nem cabeçalho.");

        var colunas = Dividir(cabecalho)
            .Select((nome, i) => (nome: nome.Trim().ToLowerInvariant(), i))
            .ToDictionary(p => p.nome, p => p.i);

        var faltando = ColunasObrigatorias.Where(c => !colunas.ContainsKey(c)).ToList();
        if (faltando.Count > 0)
            throw new FormatException(
                $"O CSV não tem as colunas esperadas: {string.Join(", ", faltando)}. " +
                "O formato da origem pode ter mudado.");

        var focos = new List<FocoDeCalor>();
        var rejeitadas = new List<LinhaRejeitada>();

        int numero = 1;
        string? linha;
        while ((linha = entrada.ReadLine()) is not null)
        {
            numero++;
            if (string.IsNullOrWhiteSpace(linha)) continue;

            var campos = Dividir(linha);
            if (campos.Length <= colunas.Values.Max())
            {
                rejeitadas.Add(new LinhaRejeitada(numero, $"campos de menos ({campos.Length})"));
                continue;
            }

            string Texto(string col) => campos[colunas[col]].Trim();

            if (!DateTime.TryParse(Texto("data_hora_gmt"), CultureInfo.InvariantCulture,
                                   DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                   out var quando))
            {
                rejeitadas.Add(new LinhaRejeitada(numero, "data inválida"));
                continue;
            }

            string bioma = Texto("bioma");
            if (bioma.Length == 0)
            {
                rejeitadas.Add(new LinhaRejeitada(numero, "bioma vazio"));
                continue;
            }

            focos.Add(new FocoDeCalor(
                quando,
                Texto("municipio"),
                Texto("estado"),
                bioma,
                Numero(Texto("numero_dias_sem_chuva")),
                Numero(Texto("precipitacao")),
                Numero(Texto("risco_fogo")),
                Numero(Texto("frp"))));
        }

        return new ResultadoDaLeitura(focos, rejeitadas);
    }

    /// <summary>
    /// Converte um campo numérico. Devolve <c>null</c> para vazio, ilegível ou para a
    /// sentinela -999 do INPE — as três situações significam a mesma coisa para quem vai
    /// calcular uma estatística: não há valor.
    /// </summary>
    private static double? Numero(string texto)
    {
        if (texto.Length == 0) return null;
        if (!double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return null;

        // Comparação por proximidade: o arquivo traz "-999" e "-999.0".
        return Math.Abs(v - FocoDeCalor.Sentinela) < 0.5 ? null : v;
    }

    private static string[] Dividir(string linha) => linha.Split(',');
}
