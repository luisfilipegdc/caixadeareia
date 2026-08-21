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
