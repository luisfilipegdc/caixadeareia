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

namespace CaixaInterativa;

/// <summary>
/// Estado geral do sistema, do ponto de vista de quem está operando — não do código.
///
/// A interface traduz cada um destes num semáforo e numa frase que diz o que fazer.
/// Um professor no meio da aula precisa saber, num relance, se pode usar, se falta um
/// passo, ou se algo quebrou.
/// </summary>
public enum EngineState
{
    /// <summary>Nenhuma fonte rodando.</summary>
    Parado,

    /// <summary>Capturando, mas sem plano-base: o mapa ainda não significa nada.</summary>
    PrecisaCalibrar,

    /// <summary>Capturando o plano-base. Ninguém deve mexer na areia agora.</summary>
    Calibrando,

    /// <summary>Tudo funcionando.</summary>
    Pronto,

    /// <summary>Sensor caiu; tentando religar sozinho.</summary>
    Reconectando,

    /// <summary>Falha que exige intervenção.</summary>
    Erro
}
