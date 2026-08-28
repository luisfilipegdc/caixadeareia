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

using System.Diagnostics;
using System.IO;
using System.Text;

namespace CaixaInterativa.Diagnostico;

public enum Nivel { Info, Aviso, Erro }

/// <summary>
/// Registro de operação em arquivo.
///
/// Existe para um cenário específico: algo dá errado durante uma aula, com a turma
/// esperando, e não há como parar para investigar. Sem registro, isso vira "não
/// funcionou" — um relato sem evidência, impossível de diagnosticar depois.
///
/// Escreve ao lado do executável, em texto simples que o professor pode abrir no
/// Bloco de Notas e anexar num e-mail de suporte.
/// </summary>
public static class Registro
{
    private static readonly object _trava = new();
    private static readonly Stopwatch _relogio = Stopwatch.StartNew();
    private static string? _caminho;
    private static bool _falhouAoEscrever;

    /// <summary>Onde o arquivo é gravado.</summary>
    public static string Caminho => _caminho ??= Path.Combine(AppContext.BaseDirectory, "registro.txt");

    /// <summary>
    /// Tamanho a partir do qual o arquivo é rotacionado.
    ///
    /// Meio megabyte cobre semanas de uso normal. Sem limite, uma sessão longa com o
    /// sensor instável encheria o disco — e um arquivo gigante é inútil para suporte,
    /// porque ninguém encontra a linha que importa.
    /// </summary>
    private const long TamanhoMaximoBytes = 512 * 1024;

    public static void Iniciar(string versao)
    {
        try
        {
            RotacionarSeGrande();

            var cabecalho = new StringBuilder();
            cabecalho.AppendLine();
            cabecalho.AppendLine(new string('=', 72));
            cabecalho.AppendLine($"Caixa de Areia Interativa v{versao}");
            cabecalho.AppendLine($"Sessão iniciada em {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            cabecalho.AppendLine($"Windows {Environment.OSVersion.Version}  ·  " +
                                 $"{Environment.ProcessorCount} núcleos  ·  " +
                                 $"{(Environment.Is64BitProcess ? "64" : "32")} bits");
            cabecalho.AppendLine(new string('=', 72));

            File.AppendAllText(Caminho, cabecalho.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Pasta somente-leitura, disco cheio, antivírus. Nada disso pode impedir o
            // programa de abrir: numa sala de aula, isso significaria aula perdida.
            _falhouAoEscrever = true;
        }
    }

    public static void Info(string mensagem) => Escrever(Nivel.Info, mensagem);
    public static void Aviso(string mensagem) => Escrever(Nivel.Aviso, mensagem);
    public static void Erro(string mensagem) => Escrever(Nivel.Erro, mensagem);

    public static void Erro(string contexto, Exception ex)
        => Escrever(Nivel.Erro, $"{contexto}: {ex.GetType().Name} — {ex.Message}");

    private static void Escrever(Nivel nivel, string mensagem)
    {
        if (_falhouAoEscrever) return;

        string marca = nivel switch
        {
            Nivel.Erro => "ERRO ",
            Nivel.Aviso => "AVISO",
            _ => "     ",
        };

        // O tempo desde a abertura ajuda mais que o relógio a entender uma sequência:
        // "caiu 40 minutos depois de abrir" diz mais que "caiu às 14h37".
        string linha = $"{DateTime.Now:HH:mm:ss}  [{_relogio.Elapsed:hh\\:mm\\:ss}] {marca} {mensagem}";

        lock (_trava)
        {
            try { File.AppendAllText(Caminho, linha + Environment.NewLine, Encoding.UTF8); }
            catch { _falhouAoEscrever = true; }
        }
    }

    /// <summary>
    /// Guarda a sessão anterior como .anterior.txt e recomeça.
    ///
    /// Manter uma geração antiga importa: o problema que se quer diagnosticar costuma
    /// estar na sessão que acabou de falhar, não na que está começando.
    /// </summary>
    private static void RotacionarSeGrande()
    {
        try
        {
            var arquivo = new FileInfo(Caminho);
            if (!arquivo.Exists || arquivo.Length < TamanhoMaximoBytes) return;

            string anterior = Path.ChangeExtension(Caminho, ".anterior.txt");
            if (File.Exists(anterior)) File.Delete(anterior);
            File.Move(Caminho, anterior);
        }
        catch { /* rotação é conveniência, não pode derrubar nada */ }
    }

    /// <summary>Abre o registro no aplicativo padrão, para o professor anexar num e-mail.</summary>
    public static void Abrir()
    {
        try
        {
            if (!File.Exists(Caminho)) File.WriteAllText(Caminho, "Nenhum evento registrado ainda.\n");
            Process.Start(new ProcessStartInfo { FileName = Caminho, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Erro("Não foi possível abrir o registro", ex);
        }
    }
}
