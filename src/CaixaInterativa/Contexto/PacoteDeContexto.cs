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

namespace CaixaInterativa.Contexto;

/// <summary>
/// Contexto externo observado, para a aula — <b>não</b> para a simulação.
///
/// Estes tipos são a cópia do lado leitor do formato que a ferramenta
/// <c>tools/CaixaInterativa.DataPrep</c> produz. <b>A duplicação é proposital:</b> o
/// contrato entre as duas pontas é o arquivo JSON, e é isso que permite ao aplicativo
/// não depender do projeto de preparação — que existe só na mesa de quem desenvolve e
/// não entra no executável distribuído.
///
/// <see cref="SchemaVersion"/> é o que torna a duplicação segura: o leitor recusa um
/// pacote de versão diferente em vez de interpretá-lo torto. E um teste serializa pelo
/// lado da ferramenta e desserializa por este, provando que os dois lados concordam.
/// </summary>
public sealed record PacoteDeContexto
{
    /// <summary>
    /// Versão do formato que este leitor entende.
    ///
    /// <b>v2</b> — a procedência passou a ser por período. A v1 tinha um único recurso no
    /// topo, o que só funcionava com um período só.
    /// </summary>
    public const int VersaoSuportada = 2;

    public int SchemaVersion { get; init; }
    public Proveniencia? Proveniencia { get; init; }
    public IReadOnlyList<ContextoTerritorial> Contextos { get; init; } = [];
}

/// <summary>
/// De onde veio cada número, e o que foi feito com ele.
///
/// Responde à pergunta que o professor pode fazer em sala: "de onde saiu isso?". Sem ela,
/// um dado externo na tela é indistinguível de um número inventado.
/// </summary>
public sealed record Proveniencia
{
    public string Fonte { get; init; } = "";
    public string Organizacao { get; init; } = "";
    public string Conjunto { get; init; } = "";
    public string FormatoOriginal { get; init; } = "";
    public string DataDeAcesso { get; init; } = "";
    public string ComandoParaRegenerar { get; init; } = "";
    public IReadOnlyList<string> Filtros { get; init; } = [];
    public string MetodoDeAgregacao { get; init; } = "";
    public string MetodoDeClassificacao { get; init; } = "";
    public IReadOnlyList<string> Observacoes { get; init; } = [];

    /// <summary>De onde veio cada período presente no pacote.</summary>
    public IReadOnlyList<PeriodoObservado> Periodos { get; init; } = [];

    /// <summary>Uma linha, para caber ao lado do dado sem empurrar o resto da tela.</summary>
    public string Resumo =>
        $"{Fonte} · {string.Join(", ", Periodos.Select(p => p.Periodo))} · acesso em {DataDeAcesso}";

    /// <summary>A origem de um período específico, ou nulo se ele não estiver no pacote.</summary>
    public PeriodoObservado? Origem(string periodo) =>
        Periodos.FirstOrDefault(p => string.Equals(p.Periodo, periodo, StringComparison.Ordinal));
}

/// <summary>
/// De onde veio um período específico.
///
/// <b><see cref="DiasObservados"/> existe por causa de um defeito real.</b> O primeiro
/// pacote deste projeto veio de um único arquivo diário e rotulou o período como
/// "2026-08" — promoveu um dia a mês inteiro. Contar os dias distintos torna isso
/// impossível de esconder.
/// </summary>
public sealed record PeriodoObservado
{
    public string Periodo { get; init; } = "";
    public string Recurso { get; init; } = "";
    public string Url { get; init; } = "";
    public int DiasObservados { get; init; }
    public int FocosLidos { get; init; }

    /// <summary>Um período representado por poucos dias não descreve o mês.</summary>
    public bool AmostraParcial => DiasObservados is > 0 and < 20;
}

/// <summary>Um recorte bioma + UF + período.</summary>
public sealed record ContextoTerritorial
{
    public string Bioma { get; init; } = "";
    public string Uf { get; init; } = "";
    public string Periodo { get; init; } = "";
    public ObservacoesDoRecorte? Observado { get; init; }
    public ClassesDidaticas? ClassesDidaticas { get; init; }

    /// <summary>Rótulo para lista e combo: "Cerrado · GOIÁS · 2026-08".</summary>
    public string Rotulo => $"{Bioma} · {Uf} · {Periodo}";

    /// <summary>Cabeçalho da leitura principal: "Cerrado · GOIÁS · junho de 2026".</summary>
    public string RotuloPorExtenso => $"{Bioma} · {Uf} · {PeriodoPorExtenso(Periodo)}";

    /// <summary>
    /// "2026-06" vira "junho de 2026".
    ///
    /// O formato ISO é ótimo para ordenar e péssimo para ler em voz alta numa sala. A
    /// lista de seleção continua em ISO, onde a ordenação importa; a leitura principal e
    /// a comparação passam a mostrar o mês por extenso.
    ///
    /// Devolve o texto original se ele não estiver no formato esperado — inventar um mês
    /// seria pior do que mostrar "2026-6X".
    /// </summary>
    public static string PeriodoPorExtenso(string periodo)
    {
        string[] meses =
        [
            "janeiro", "fevereiro", "março", "abril", "maio", "junho",
            "julho", "agosto", "setembro", "outubro", "novembro", "dezembro",
        ];

        var partes = periodo.Split('-');
        if (partes.Length != 2
            || !int.TryParse(partes[0], out int ano)
            || !int.TryParse(partes[1], out int mes)
            || mes is < 1 or > 12)
        {
            return periodo;
        }

        return $"{meses[mes - 1]} de {ano}";
    }
}

/// <summary>Os números como saíram do dado, depois de descartada a sentinela do INPE.</summary>
public sealed record ObservacoesDoRecorte
{
    public int Focos { get; init; }
    public double? RiscoFogoMediano { get; init; }
    public double? RiscoFogoP25 { get; init; }
    public double? RiscoFogoP75 { get; init; }
    public double? DiasSemChuvaMediano { get; init; }
    public double? DiasSemChuvaP75 { get; init; }
    public double? PrecipitacaoMedianaMm { get; init; }
    public double? FrpMedianoMw { get; init; }
    public AmostrasValidas? Amostras { get; init; }
}

/// <summary>Quantos focos tinham valor utilizável em cada campo.</summary>
public sealed record AmostrasValidas
{
    public int RiscoFogo { get; init; }
    public int DiasSemChuva { get; init; }
    public int Precipitacao { get; init; }
    public int Frp { get; init; }
}

/// <summary>
/// A tradução para linguagem de aula.
///
/// <b>É com isto que a interface trabalha</b>, não com os números. Os números ficam no
/// pacote para quem quiser auditar. Essa separação é o que impede um valor observado de
/// virar coeficiente de simulação por descuido.
/// </summary>
public sealed record ClassesDidaticas
{
    public string Risco { get; init; } = "";
    public string Secura { get; init; } = "";

    /// <summary>
    /// Como a classe foi produzida. Hoje sempre <c>relativa_ao_recorte</c>: os cortes
    /// vêm dos quartis dos próprios recortes do pacote, não de uma escala oficial.
    /// </summary>
    public string Classificacao { get; init; } = "";
}
