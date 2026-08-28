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

using System.IO;
using System.Text.Json;

namespace CaixaInterativa.Contexto;

/// <summary>O que aconteceu ao tentar carregar um pacote.</summary>
public sealed record ResultadoDoCarregamento(PacoteDeContexto? Pacote, string? Erro)
{
    public bool Carregou => Pacote is not null;

    /// <summary>Contextos disponíveis, ou lista vazia se não carregou.</summary>
    public IReadOnlyList<ContextoTerritorial> Contextos => Pacote?.Contextos ?? [];
}

/// <summary>
/// Lê o pacote de contexto de um arquivo local.
///
/// <b>Não acessa a rede. Nunca.</b> Nem para verificar atualização, nem para baixar nada.
/// O arquivo é gerado antes da aula pela ferramenta em <c>tools/CaixaInterativa.DataPrep</c>
/// e versionado junto com o código. Uma escola sem internet abre o programa e vê os mesmos
/// dados que viu ontem — e é essa previsibilidade que também permite comparar duas aulas.
///
/// Falha silenciosa é aceitável aqui, e proposital: contexto externo é enfeite pedagógico,
/// não requisito. Se o arquivo sumir ou vier corrompido, a caixa continua funcionando como
/// sempre funcionou, e a interface simplesmente não oferece a seção. É a mesma política de
/// <c>AppConfig.Load</c> — numa sala de aula, abrir sempre vale mais que avisar.
/// </summary>
public static class LeitorDeContexto
{
    /// <summary>Nome do arquivo ao lado do executável.</summary>
    public const string NomeDoArquivo = "contexto-queimadas.json";

    /// <summary>Caminho padrão: a pasta Dados, ao lado do executável.</summary>
    public static string CaminhoPadrao =>
        Path.Combine(AppContext.BaseDirectory, "Dados", NomeDoArquivo);

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Carrega e valida. Devolve o erro em vez de lançar — quem chama decide se mostra ou
    /// só omite a seção.
    /// </summary>
    public static ResultadoDoCarregamento Carregar(string? caminho = null)
    {
        caminho ??= CaminhoPadrao;

        try
        {
            if (!File.Exists(caminho))
                return new ResultadoDoCarregamento(null, $"Nenhum pacote de contexto em {caminho}.");

            var pacote = JsonSerializer.Deserialize<PacoteDeContexto>(File.ReadAllText(caminho), Opcoes);

            if (pacote is null)
                return new ResultadoDoCarregamento(null, "O pacote de contexto está vazio.");

            // Versão diferente: recusar é mais seguro que adivinhar. Um campo que mudou de
            // significado entre versões viraria um número errado na tela, sem aviso.
            if (pacote.SchemaVersion != PacoteDeContexto.VersaoSuportada)
                return new ResultadoDoCarregamento(null,
                    $"O pacote está na versão {pacote.SchemaVersion} e este programa entende " +
                    $"a versão {PacoteDeContexto.VersaoSuportada}. Regenere o pacote.");

            if (pacote.Proveniencia is null)
                return new ResultadoDoCarregamento(null,
                    "O pacote não declara procedência. Um dado sem origem não vai para a tela.");

            return new ResultadoDoCarregamento(pacote, null);
        }
        catch (JsonException ex)
        {
            return new ResultadoDoCarregamento(null, $"O pacote não pôde ser lido: {ex.Message}");
        }
        catch (IOException ex)
        {
            return new ResultadoDoCarregamento(null, $"Falha ao abrir o pacote: {ex.Message}");
        }
    }
}
