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

namespace CaixaInterativa.Depth;

/// <summary>
/// Um quadro de profundidade bruto. Distancias em milimetros a partir do plano do sensor.
/// Zero significa "sem leitura" (sombra de IR, superficie muito escura, fora de alcance).
/// </summary>
public sealed class RawDepthFrame
{
    public required ushort[] Data { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public long FrameNumber { get; init; }
}

public interface IDepthSource : IDisposable
{
    string Name { get; }
    int Width { get; }
    int Height { get; }
    bool IsRunning { get; }

    /// <summary>Disparado na thread de captura, nao na UI.</summary>
    event Action<RawDepthFrame>? FrameArrived;

    /// <summary>Disparado quando a captura morre (sensor desconectado, erro nativo).</summary>
    event Action<string>? Faulted;

    void Start();
    void Stop();
}
