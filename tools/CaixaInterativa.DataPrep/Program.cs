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
using System.Text.Json;
using CaixaInterativa.DataPrep;

// ─────────────────────────────────────────────────────────────────────────────
// Prepara o pacote de contexto que o aplicativo de sala consome offline.
//
// Esta ferramenta é a ÚNICA parte do projeto que acessa a rede, e ela não entra no
// executável distribuído. Roda na mesa de quem desenvolve, gera um JSON, e sai de cena.
// O aplicativo lê o arquivo e nunca sabe que a internet existe.
//
// Aceita várias fontes: cada `--fonte` vira um período no pacote. É assim que se compara
// o mesmo território em dois momentos.
// ─────────────────────────────────────────────────────────────────────────────

var fontes = new List<string>();
string? saida = null;

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] is "--fonte" or "--source") fontes.Add(args[i + 1]);
    if (args[i] is "--saida" or "--output") saida = args[i + 1];
}

if (args.Contains("--ajuda") || args.Contains("--help") || fontes.Count == 0)
{
    Console.WriteLine("""
        Prepara o pacote de contexto educacional a partir dos focos de calor do INPE.

          --fonte  <url|caminho>   CSV de focos. Pode repetir para incluir mais períodos.
          --saida  <caminho>       Onde gravar o JSON. Sem isto, escreve na saída padrão.

        Um período, um arquivo:
          dotnet run --project tools/CaixaInterativa.DataPrep -- \
            --fonte .../focos_mensal_br_202607.csv \
            --saida src/CaixaInterativa/Dados/contexto-queimadas.json

        Dois períodos para comparar:
          dotnet run --project tools/CaixaInterativa.DataPrep -- \
            --fonte .../focos_mensal_br_202606.csv \
            --fonte .../focos_mensal_br_202607.csv \
            --saida src/CaixaInterativa/Dados/contexto-queimadas.json

        Os períodos saem do próprio dado (ano-mês de cada foco), não do nome do arquivo.
        """);
    return fontes.Count == 0 && !args.Contains("--ajuda") && !args.Contains("--help") ? 1 : 0;
}

try
{
    var todosOsFocos = new List<FocoDeCalor>();
    var periodos = new List<PeriodoObservado>();
    string acesso = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    foreach (string fonte in fontes)
    {
        Console.Error.WriteLine($"Lendo: {fonte}");

        using TextReader entrada = fonte.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? await BaixarAsync(fonte)
            : new StreamReader(fonte);

        var leitura = LeitorDeFocosCsv.Ler(entrada);
        Console.Error.WriteLine($"  focos lidos: {leitura.Focos.Count:N0}");
        if (leitura.Rejeitadas.Count > 0)
        {
            Console.Error.WriteLine($"  linhas rejeitadas: {leitura.Rejeitadas.Count}");
            foreach (var r in leitura.Rejeitadas.Take(3))
                Console.Error.WriteLine($"    linha {r.Numero}: {r.Motivo}");
        }

        todosOsFocos.AddRange(leitura.Focos);

        // O período sai do dado, não do nome do arquivo. Um arquivo que atravesse a
        // virada do mês produz dois períodos, e isso fica visível.
        foreach (var grupo in leitura.Focos
                     .GroupBy(f => f.DataHoraGmt.ToString("yyyy-MM", CultureInfo.InvariantCulture))
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            int dias = grupo.Select(f => f.DataHoraGmt.Date).Distinct().Count();
            periodos.Add(new PeriodoObservado(
                Periodo: grupo.Key,
                Recurso: fonte[(fonte.LastIndexOf('/') + 1)..],
                Url: fonte,
                DiasObservados: dias,
                FocosLidos: grupo.Count()));

            Console.Error.WriteLine($"  período {grupo.Key}: {grupo.Count():N0} focos em {dias} dia(s)");
        }
    }

    var contextos = Agregador.Agregar(todosOsFocos);
    Console.Error.WriteLine(
        $"recortes (bioma × UF × mês, ≥{Agregador.MinimoDeFocosPorRecorte} focos): {contextos.Count}");

    var pacote = new PacoteDeContexto(
        SchemaVersion: PacoteDeContexto.VersaoAtual,
        Proveniencia: new Proveniencia(
            Fonte: "INPE — Programa Queimadas",
            Organizacao: "Instituto Nacional de Pesquisas Espaciais",
            Conjunto: "Focos de calor detectados por satélite (dados abertos)",
            FormatoOriginal: "CSV, separado por vírgula, cultura invariante",
            DataDeAcesso: acesso,
            ComandoParaRegenerar:
                "dotnet run --project tools/CaixaInterativa.DataPrep -- " +
                string.Join(" ", fontes.Select(f => $"--fonte {f}")) +
                " --saida src/CaixaInterativa/Dados/contexto-queimadas.json",
            Filtros:
            [
                "descartados os valores -999, que o INPE usa para dado inválido (área urbana, corpo d'água)",
                $"descartados os recortes com menos de {Agregador.MinimoDeFocosPorRecorte} focos",
                "descartadas as linhas sem bioma ou com data ilegível",
            ],
            MetodoDeAgregacao:
                "agrupamento por bioma × UF × mês; mediana e quartis por interpolação linear, " +
                "arredondados a três casas. Mediana em vez de média porque a distribuição de FRP " +
                "tem cauda longa: numa amostra real a média foi 43,7 MW contra mediana de 11,5 MW.",
            MetodoDeClassificacao:
                "quartis calculados sobre os próprios recortes deste pacote (classificação relativa). " +
                "Com mais de um período no pacote, os cortes são comuns aos dois — é o que torna as " +
                "classes comparáveis entre eles. Não foram usados os cortes nomeados do INPE porque " +
                "não foi possível confirmá-los em fonte primária legível; o FAQ do Programa Queimadas " +
                "informa apenas que o risco de fogo varia de 0 a 1.",
            Observacoes: Observar(contextos, periodos),
            Periodos: periodos),
        Contextos: contextos);

    var opcoes = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Sem escapar acentos: "Amazônia" tem que ser legível no diff do Git.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    string json = JsonSerializer.Serialize(pacote, opcoes);

    if (saida is null)
    {
        Console.WriteLine(json);
    }
    else
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(saida))!);
        File.WriteAllText(saida, json);
        Console.Error.WriteLine($"gravado: {saida} ({new FileInfo(saida).Length / 1024.0:F1} KB)");
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERRO: {ex.Message}");
    return 1;
}

/// <summary>
/// As ressalvas que acompanham o pacote. As três primeiras valem sempre; as demais são
/// acrescentadas só quando a situação acontece, para o pacote registrar o que houve
/// naquela geração em vez de repetir avisos genéricos.
/// </summary>
static IReadOnlyList<string> Observar(
    IReadOnlyList<ContextoTerritorial> contextos,
    IReadOnlyList<PeriodoObservado> periodos)
{
    var notas = new List<string>
    {
        "Foco de calor não é incêndio: é detecção por satélite, sujeita a nuvem, horário de passagem e resolução do sensor.",
        "Precipitação é o acumulado do dia da detecção, e por isso é quase sempre zero.",
        "Este pacote é CONTEXTO para a aula. Ele não alimenta nenhum parâmetro das simulações.",
    };

    int saturado = contextos.Count(c => c.ClassesDidaticas.Risco == Agregador.SemVariacao);
    if (saturado > 0)
        notas.Add(
            $"O risco de fogo não discriminou os recortes desta geração ({saturado} de {contextos.Count}): " +
            "nos focos detectados ele satura perto de 1, porque o fogo acontece justamente onde o risco é alto. " +
            "A classe foi marcada como \"sem variação suficiente\" em vez de dividida em quatro níveis inventados. " +
            "Para comparar territórios, use a secura.");

    // Um período representado por poucos dias não descreve o mês, e comparar a contagem
    // de focos dele com a de um mês inteiro seria comparar coisas diferentes.
    var curtos = periodos.Where(p => p.DiasObservados < 20).ToList();
    if (curtos.Count > 0)
        notas.Add(
            "Períodos representados por amostra parcial de dias: " +
            string.Join(", ", curtos.Select(p => $"{p.Periodo} ({p.DiasObservados} dia(s))")) +
            ". A contagem de focos desses períodos não é comparável à de um mês completo.");

    if (periodos.Select(p => p.Periodo).Distinct().Count() > 1)
        notas.Add(
            "Este pacote traz mais de um período. Diferenças entre eles são observações, " +
            "não relação de causa: dizer que um período teve mais dias sem chuva e também " +
            "mais focos descreve o que foi medido; dizer que um causou o outro seria " +
            "afirmar o que este dado não mostra.");

    return notas;
}

static async Task<TextReader> BaixarAsync(string url)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    string conteudo = await http.GetStringAsync(url);
    return new StringReader(conteudo);
}
