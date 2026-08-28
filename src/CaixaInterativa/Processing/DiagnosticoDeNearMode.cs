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

namespace CaixaInterativa.Processing;

/// <summary>
/// Observa a menor profundidade válida que o sensor entrega, para dar um indício de que
/// o near mode não pegou.
///
/// **Por que existe.** `NuiImageStreamSetImageFrameFlags` devolve `S_OK` mesmo quando o
/// near mode não é aplicado — num sensor de Xbox 360, ou com a flag errada. O código já
/// documenta que a única verificação confiável é empírica: com near mode ativo aparecem
/// leituras abaixo de 800 mm; sem ele, 800 mm é um piso duro.
///
/// **O que isto NÃO é.** Não é prova. Se a areia estiver toda a mais de 80 cm do sensor,
/// a mínima observada fica acima de 800 mm legitimamente, com o near mode funcionando
/// perfeitamente. Por isso o resultado é um **aviso**, nunca um erro, e nunca interrompe
/// nada. É diagnóstico para quem for investigar, não veredito.
///
/// **Classificação:** medição (a profundidade lida) com uma inferência declarada como
/// tal. Nenhum parâmetro inventado.
/// </summary>
public sealed class DiagnosticoDeNearMode
{
    /// <summary>
    /// Piso de alcance do sensor sem near mode. Vem da folha de dados do Kinect v1 e está
    /// registrado no diário de bordo: com a flag errada o mínimo lido ficou em 801 mm;
    /// com a correta, caiu para 455 mm.
    /// </summary>
    public const ushort PisoSemNearModeMm = 800;

    /// <summary>
    /// Quadros observados antes de opinar. Meio segundo de captura a 30 fps — tempo de o
    /// sensor estabilizar sem fazer o professor esperar.
    /// </summary>
    public const int QuadrosDeObservacao = 15;

    /// <summary>
    /// Fração mínima de leituras válidas para a observação valer. Com a caixa vazia ou o
    /// sensor tampado quase tudo é inválido, e a mínima observada não diria nada.
    /// </summary>
    private const double CoberturaMinima = 0.10;

    private int _quadros;
    private long _validas;
    private long _amostras;

    /// <summary>Menor profundidade válida vista até agora, em mm. Zero antes da primeira.</summary>
    public ushort MinimaObservadaMm { get; private set; }

    /// <summary>Já observou quadros suficientes para opinar?</summary>
    public bool Concluido => _quadros >= QuadrosDeObservacao;

    /// <summary>
    /// Observa um quadro. Amostragem esparsa: um pixel a cada 7 basta para achar a
    /// mínima de uma cena contínua, e isto roda no caminho de quadro.
    /// </summary>
    public void Observar(ushort[] dados, ushort minValidoMm, ushort maxValidoMm)
    {
        ArgumentNullException.ThrowIfNull(dados);
        if (Concluido) return;

        ushort minimo = MinimaObservadaMm == 0 ? ushort.MaxValue : MinimaObservadaMm;

        for (int i = 0; i < dados.Length; i += 7)
        {
            _amostras++;
            ushort d = dados[i];
            if (d < minValidoMm || d > maxValidoMm) continue;

            _validas++;
            if (d < minimo) minimo = d;
        }

        if (minimo != ushort.MaxValue) MinimaObservadaMm = minimo;
        _quadros++;
    }

    /// <summary>
    /// Devolve o aviso quando há indício de near mode inativo, ou <c>null</c> quando não
    /// há o que dizer — inclusive quando ainda não observou o bastante, quando o near
    /// mode nem foi pedido, ou quando houve leituras válidas poucas demais.
    /// </summary>
    public string? Aviso(bool nearModeSolicitado)
    {
        if (!nearModeSolicitado || !Concluido) return null;
        if (_amostras == 0 || _validas / (double)_amostras < CoberturaMinima) return null;
        if (MinimaObservadaMm == 0 || MinimaObservadaMm < PisoSemNearModeMm) return null;

        return $"O near mode foi pedido, mas nenhuma leitura ficou abaixo de " +
               $"{PisoSemNearModeMm} mm (a menor foi {MinimaObservadaMm} mm). " +
               "Pode ser que ele não tenha sido aplicado — ou que a areia esteja mesmo " +
               "toda a mais de 80 cm do sensor. Se o mapa parecer cortado nas bordas, " +
               "vale conferir o modelo do sensor: o near mode só funciona no Kinect for " +
               "Windows 1517.";
    }

    /// <summary>Recomeça a observação. Usado quando uma fonte nova inicia.</summary>
    public void Reiniciar()
    {
        _quadros = 0;
        _validas = 0;
        _amostras = 0;
        MinimaObservadaMm = 0;
    }
}
