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

namespace CaixaInterativa.Rendering;

/// <summary>
/// Como uma camada é composta sobre o mapa topográfico.
///
/// São exatamente os quatro tratamentos que já existiam escritos à mão dentro do
/// renderizador — nenhum novo. O renderizador sabe desenhar cada um deles; não sabe
/// qual módulo produziu o campo.
/// </summary>
public enum ModoDeCor
{
    /// <summary>
    /// Lâmina d'água: raso esverdeado, fundo azul-escuro, como num mapa náutico.
    /// Se houver <see cref="CamadaVisual.CampoAuxiliar"/>, ele é lido como velocidade
    /// e clareia a correnteza — o que distingue um rio correndo de um lago parado.
    /// </summary>
    Agua,

    /// <summary>Dano acumulado: amarelo para leve, vermelho para severo — a convenção
    /// dos mapas de risco.</summary>
    Risco,

    /// <summary>Frente de onda: clarão que passa por cima, sem esconder o que está embaixo.</summary>
    Clarao,

    /// <summary>Chama: vermelho na borda da frente de fogo, amarelo no núcleo.</summary>
    Calor,

    /// <summary>
    /// Cicatriz de queimada: escurece o terreno sem apagar o relevo.
    ///
    /// Não é <see cref="Risco"/>. Dano sísmico é amarelo-vermelho porque ainda pede
    /// resposta; área queimada já aconteceu, e a leitura dela é a de uma foto de
    /// satélite depois do fogo — carvão.
    /// </summary>
    Cicatriz,
}

/// <summary>
/// Um campo escalar que um módulo de simulação quer ver desenhado sobre o relevo.
///
/// Este é o contrato que substitui os onze parâmetros por módulo que a assinatura de
/// <see cref="TopographicRenderer.Render"/> carregava. A regra que ele estabelece:
/// o renderizador sabe **como** desenhar um <see cref="ModoDeCor"/>, e não sabe **quem**
/// produziu o campo.
///
/// É um <c>readonly record struct</c> de propósito. A composição roda em ~307 mil pixels
/// por quadro e a lista é percorrida dentro desse laço: uma classe custaria uma
/// indireção por acesso, e um delegate por camada custaria uma chamada virtual por pixel.
/// Aqui não há alocação, nem despacho dinâmico, nem lambda no caminho quente.
///
/// O sentido da dependência é proposital: <c>Simulation</c> conhece <c>Rendering</c>,
/// e <c>Rendering</c> não conhece <c>Simulation</c>. É o que permite acrescentar um
/// fenômeno sem tocar no renderizador.
/// </summary>
/// <param name="Campo">O campo escalar, na resolução da própria camada.</param>
/// <param name="Largura">Largura do campo em células.</param>
/// <param name="Altura">Altura do campo em células.</param>
/// <param name="Ordem">
/// Quem desenha por cima de quem. Menor desenha antes. Ver as constantes
/// <c>Ordem*</c>, que preservam a sequência que estava implícita na ordem dos blocos
/// dentro do renderizador.
/// </param>
/// <param name="Modo">Como compor esta camada sobre o que já está desenhado.</param>
/// <param name="Limiar">Abaixo deste valor a célula não é desenhada.</param>
/// <param name="CampoAuxiliar">
/// Segundo campo, na mesma grade, usado apenas por <see cref="ModoDeCor.Agua"/> para a
/// clareação de correnteza. Nulo quando não se aplica.
/// </param>
public readonly record struct CamadaVisual(
    float[] Campo,
    int Largura,
    int Altura,
    int Ordem,
    ModoDeCor Modo,
    float Limiar,
    float[]? CampoAuxiliar = null)
{
    // A ordem abaixo não foi escolhida: foi extraída da sequência dos blocos no
    // renderizador antes da refatoração — água, depois dano sísmico, depois a frente de
    // onda, e o fogo por cima de tudo, porque é o evento mais urgente na tela.
    // Os intervalos de 100 deixam espaço para um fenômeno novo entrar no meio sem
    // renumerar os existentes.

    /// <summary>Água sobre o terreno.</summary>
    public const int OrdemAgua = 100;

    /// <summary>Dano acumulado, que fica no mapa depois do evento.</summary>
    public const int OrdemRisco = 200;

    /// <summary>Frente de onda, por cima do dano que ela mesma vai deixar.</summary>
    public const int OrdemClarao = 210;

    /// <summary>A cicatriz fica no mapa depois que a chama passa, e por isso vem antes
    /// dela: onde ainda ha' fogo, e' o fogo que se ve.</summary>
    public const int OrdemCicatriz = 250;

    /// <summary>Fogo, acima de tudo.</summary>
    public const int OrdemCalor = 300;

    /// <summary>Uma camada só é desenhável se tiver campo e dimensões coerentes.</summary>
    public bool Desenhavel => Campo is not null && Largura > 0 && Altura > 0;
}
