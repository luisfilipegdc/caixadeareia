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

using CaixaInterativa.Rendering;

namespace CaixaInterativa.Simulation;

/// <summary>
/// Um fenômeno simulado sobre o relevo — água, erosão, temperatura, ondas sísmicas.
///
/// Encaixa entre o processamento de profundidade e a renderização:
///
///   IDepthSource → DepthProcessor → [ ISimulationModule ] → renderização
///      sensor       campo de alturas    estado do fenômeno    camadas visuais
///
/// O módulo recebe o campo de alturas calibrado a cada quadro e mantém estado próprio
/// entre quadros. Quem desenha decide como compor esse estado sobre o mapa topográfico.
///
/// A regra que mantém isto administrável: um módulo completo por vez — simulação,
/// controles e material pedagógico — antes de começar o próximo.
/// </summary>
public interface ISimulationModule
{
    /// <summary>Nome exibido ao professor, não o nome da classe.</summary>
    string Nome { get; }

    /// <summary>Resolução em que o módulo trabalha, que pode ser menor que a do sensor.</summary>
    int Width { get; }
    int Height { get; }

    /// <summary>Se falso, o módulo não é atualizado nem desenhado.</summary>
    bool Ativo { get; set; }

    /// <summary>
    /// Avança a simulação. <paramref name="terrenoMm"/> é o campo de alturas do sensor,
    /// na resolução dele; o módulo reamostra se trabalhar em outra.
    /// <paramref name="dt"/> vem em segundos e já chega limitado — um travamento
    /// momentâneo não deve virar um salto que estoura a simulação.
    /// </summary>
    void Atualizar(float[] terrenoMm, int larguraTerreno, int alturaTerreno, float dt);

    /// <summary>Volta ao estado inicial, sem perder a configuração.</summary>
    void Limpar();

    /// <summary>
    /// O que este módulo quer ver desenhado sobre o relevo, em ordem crescente de
    /// <see cref="CamadaVisual.Ordem"/>.
    ///
    /// É por aqui que um fenômeno novo aparece na projeção sem que o renderizador
    /// precise conhecê-lo: o módulo descreve **o que** desenhar, e o renderizador decide
    /// **como**, a partir do <see cref="ModoDeCor"/>.
    ///
    /// Quem lê deve percorrer por índice e consumir na hora. A lista é reaproveitada
    /// entre quadros para não gerar lixo, e os campos apontados podem ser trocados pelo
    /// módulo no quadro seguinte.
    /// </summary>
    IReadOnlyList<CamadaVisual> Camadas { get; }
}
