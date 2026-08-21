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

using System.Reflection;

namespace CaixaInterativa;

/// <summary>
/// Identidade do projeto num lugar só.
///
/// Existe para que a tela, a documentação e o instalador nunca divirjam sobre versão,
/// endereço de suporte ou página do projeto. Trocar um e-mail deveria ser uma edição,
/// não uma caçada.
/// </summary>
public static class AppInfo
{
    public const string Nome = "Caixa de Areia Interativa";

    public const string Autor = "Projeto Caixa de Areia";

    public const string Local = "Brasília, DF";

    /// <summary>Onde o professor pede ajuda quando algo dá errado numa aula.</summary>
    public const string EmailSuporte = "contato@luisfilipegdc.com.br";

    /// <summary>Página do projeto, com material de apoio e instruções.</summary>
    public const string PaginaDoProjeto = "https://luisfilipegdc.com.br/caixa-de-areia";

    /// <summary>Código-fonte, histórico e registro de problemas.</summary>
    public const string Repositorio = "https://github.com/luisfilipegdc/caixadeareia";

    public const string Licenca = "GPL-2.0-or-later";

    /// <summary>
    /// Versão vinda do assembly, para não haver um número escrito à mão que envelhece
    /// sem ninguém perceber. Definida em CaixaInterativa.csproj.
    /// </summary>
    public static string Versao
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }
    }

    public static string TituloDaJanela => $"{Nome}  ·  v{Versao}";

    /// <summary>Versão em destaque, o primeiro dado que o suporte pede.</summary>
    public static string VersaoExibida => $"v{Versao}";

    /// <summary>Linha discreta de autoria e licença, abaixo da versão.</summary>
    public static string Assinatura => $"{Autor}  ·  {Licenca}";

    /// <summary>
    /// Assunto pré-preenchido no e-mail de suporte, já com a versão — evita a primeira
    /// pergunta de toda conversa de suporte.
    /// </summary>
    public static string LinkDeSuporte =>
        $"mailto:{EmailSuporte}?subject={Uri.EscapeDataString($"[{Nome} v{Versao}] Suporte")}";
}
