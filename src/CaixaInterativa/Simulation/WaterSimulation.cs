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

namespace CaixaInterativa.Simulation;

/// <summary>
/// Simulação de água sobre o relevo pelo modelo de tubos virtuais (Mei, Decaudin e Hu).
///
/// Cada célula guarda uma coluna de água e quatro tubos que a ligam aos vizinhos. A
/// diferença de nível entre células acelera o fluxo nos tubos; o fluxo move água. Isso
/// dá o que a aula precisa: a água escorre morro abaixo, acumula nos vales, forma rios
/// que seguem o terreno e para onde deve parar.
///
/// Por que não um solver de Navier-Stokes: seria mais caro e menos estável, e o que o
/// estudante precisa ver é *para onde a água vai*, não turbulência.
///
/// Resolução: simula em metade da resolução do sensor. Pela condição CFL, 640x480 exigiria
/// 14 substeps por quadro (~86 milhões de operações); 320x240 exige 7 (~11 milhões), que
/// cabe no orçamento de 33ms junto com a renderização. A água é um campo suave, então a
/// perda some depois da reamostragem.
/// </summary>
public sealed class WaterSimulation : ISimulationModule
{
    private const float Gravidade = 9810f;   // mm/s²

    private readonly int _w, _h;
    private readonly float _tamanhoCelulaMm;

    private float[] _agua;          // profundidade da água, mm
    private float[] _terreno;       // relevo reamostrado, mm
    private float[] _fluxoE, _fluxoD, _fluxoC, _fluxoB;
    private float[] _aguaNova;

    // Velocidade do fluxo, guardada para colorir correnteza e detectar rios.
    private float[] _velocidade;

    public string Nome => "Água e enchentes";
    public int Width => _w;
    public int Height => _h;
    public bool Ativo { get; set; }

    /// <summary>Chuva sobre toda a área, em mm por segundo. Zero desliga.</summary>
    public float ChuvaMmPorSegundo { get; set; }

    /// <summary>Segundos restantes do episódio de chuva; zero quando não está chovendo.</summary>
    public float ChuvaRestanteSegundos { get; private set; }

    public bool Chovendo => ChuvaRestanteSegundos > 0f;

    /// <summary>
    /// Dispara um episódio de chuva com início e fim.
    ///
    /// A aula funciona como um evento: os estudantes constroem o território, o professor
    /// provoca a chuva e todos observam onde a água chega. Chuva contínua num controle
    /// deslizante não teria esse momento — e sem o fim da chuva não dá para ver o
    /// escoamento, que é metade do fenômeno.
    /// </summary>
    public void IniciarChuva(float intensidadeMmPorSegundo, float duracaoSegundos)
    {
        ChuvaMmPorSegundo = MathF.Max(0f, intensidadeMmPorSegundo);
        ChuvaRestanteSegundos = MathF.Max(0f, duracaoSegundos);
        Ativo = true;
        PicoAlagamentoPercent = 0;
    }

    public void PararChuva() => ChuvaRestanteSegundos = 0f;

    /// <summary>
    /// Maior alagamento atingido no episódio. É o número que responde à pergunta da
    /// aula — a intervenção reduziu a área alagada? — e que se perderia se só
    /// mostrássemos o instante atual, já em escoamento.
    /// </summary>
    public double PicoAlagamentoPercent { get; private set; }

    /// <summary>
    /// Fração da água que some por segundo, representando infiltração no solo.
    /// Sem isso a caixa enche e nunca esvazia, e o aluno só vê o alagamento final.
    /// </summary>
    public float InfiltracaoPorSegundo { get; set; } = 0.06f;

    /// <summary>Água que chega à borda da caixa escoa para fora, como num terreno aberto.</summary>
    public bool BordasEscoam { get; set; } = true;

    /// <summary>Atrito do leito: segura a água em terreno plano e evita oscilação.</summary>
    public float Amortecimento { get; set; } = 0.14f;

    /// <summary>Profundidade da água por célula, para quem desenha.</summary>
    public float[] Profundidade => _agua;

    /// <summary>Velocidade do fluxo por célula, em mm/s.</summary>
    public float[] Velocidade => _velocidade;

    /// <summary>Volume total, em litros. Serve para o aluno comparar cenários.</summary>
    public double VolumeLitros { get; private set; }

    /// <summary>
    /// Lâmina a partir da qual consideramos que a área está alagada.
    ///
    /// Um milímetro seria chão molhado: durante a chuva a superfície inteira passa disso
    /// e a métrica marcaria 96%, sem distinguir um terreno bem drenado de um mal drenado
    /// — que é exatamente a comparação que a aula precisa fazer. Oito milímetros é água
    /// acumulada, do tipo que alaga.
    /// </summary>
    public float LimiarAlagamentoMm { get; set; } = 8f;

    /// <summary>Fração da área com água acumulada acima do limiar.</summary>
    public double AreaAlagadaPercent { get; private set; }

    public WaterSimulation(int larguraSensor, int alturaSensor, float larguraCaixaMm = 1250f)
    {
        _w = Math.Max(2, larguraSensor / 2);
        _h = Math.Max(2, alturaSensor / 2);
        _tamanhoCelulaMm = larguraCaixaMm / _w;

        int n = _w * _h;
        _agua = new float[n];
        _aguaNova = new float[n];
        _terreno = new float[n];
        _fluxoE = new float[n];
        _fluxoD = new float[n];
        _fluxoC = new float[n];
        _fluxoB = new float[n];
        _velocidade = new float[n];
    }

    public void Atualizar(float[] terrenoMm, int larguraTerreno, int alturaTerreno, float dt)
    {
        if (!Ativo) return;

        ReamostrarTerreno(terrenoMm, larguraTerreno, alturaTerreno);

        // A condição CFL diz qual passo mantém o solver estável. Dividimos o quadro em
        // substeps em vez de reduzir a gravidade: mexer na física para caber no orçamento
        // faria a água escorrer devagar demais e o aluno não reconheceria o fenômeno.
        float dtMax = _tamanhoCelulaMm / MathF.Sqrt(Gravidade * Math.Max(1f, PicoDeAgua()));
        int substeps = Math.Clamp((int)MathF.Ceiling(dt / dtMax), 1, 12);
        float passo = dt / substeps;

        for (int i = 0; i < substeps; i++)
        {
            if (Chovendo && ChuvaMmPorSegundo > 0f)
            {
                AplicarChuva(passo);
                ChuvaRestanteSegundos -= passo;
                if (ChuvaRestanteSegundos < 0f) ChuvaRestanteSegundos = 0f;
            }
            AtualizarFluxos(passo);
            MoverAgua(passo);
        }

        AplicarInfiltracao(dt);
        CalcularEstatisticas();
    }

    /// <summary>Maior coluna de água, usada só para dimensionar o passo de tempo.</summary>
    private float PicoDeAgua()
    {
        float pico = 0f;
        // Amostragem esparsa: o pico exato não muda a estabilidade, e varrer 77k
        // células a cada quadro só para isso seria desperdício.
        for (int i = 0; i < _agua.Length; i += 37)
            if (_agua[i] > pico) pico = _agua[i];
        return pico;
    }

    private void ReamostrarTerreno(float[] origem, int ow, int oh)
    {
        Parallel.For(0, _h, y =>
        {
            int sy = y * oh / _h;
            int linhaOrigem = sy * ow;
            int linhaDestino = y * _w;
            for (int x = 0; x < _w; x++)
            {
                int sx = x * ow / _w;
                _terreno[linhaDestino + x] = origem[linhaOrigem + sx];
            }
        });
    }

    private void AplicarChuva(float dt)
    {
        float quantidade = ChuvaMmPorSegundo * dt;
        Parallel.For(0, _h, y =>
        {
            int linha = y * _w;
            for (int x = 0; x < _w; x++) _agua[linha + x] += quantidade;
        });
    }

    /// <summary>
    /// Acelera o fluxo de cada tubo pela diferença de nível com o vizinho, onde nível é
    /// terreno mais água. É por somar os dois que a água sabe contornar um morro em vez
    /// de atravessá-lo, e empoçar num vale já cheio.
    /// </summary>
    private void AtualizarFluxos(float dt)
    {
        float aceleracao = dt * Gravidade / _tamanhoCelulaMm;
        float retencao = 1f - Amortecimento * dt;
        if (retencao < 0f) retencao = 0f;

        Parallel.For(0, _h, y =>
        {
            int linha = y * _w;
            for (int x = 0; x < _w; x++)
            {
                int i = linha + x;
                float nivel = _terreno[i] + _agua[i];

                _fluxoE[i] = x > 0
                    ? MathF.Max(0f, _fluxoE[i] * retencao + aceleracao * (nivel - Nivel(i - 1)))
                    : (BordasEscoam ? MathF.Max(0f, _fluxoE[i] * retencao + aceleracao * _agua[i]) : 0f);

                _fluxoD[i] = x < _w - 1
                    ? MathF.Max(0f, _fluxoD[i] * retencao + aceleracao * (nivel - Nivel(i + 1)))
                    : (BordasEscoam ? MathF.Max(0f, _fluxoD[i] * retencao + aceleracao * _agua[i]) : 0f);

                _fluxoC[i] = y > 0
                    ? MathF.Max(0f, _fluxoC[i] * retencao + aceleracao * (nivel - Nivel(i - _w)))
                    : (BordasEscoam ? MathF.Max(0f, _fluxoC[i] * retencao + aceleracao * _agua[i]) : 0f);

                _fluxoB[i] = y < _h - 1
                    ? MathF.Max(0f, _fluxoB[i] * retencao + aceleracao * (nivel - Nivel(i + _w)))
                    : (BordasEscoam ? MathF.Max(0f, _fluxoB[i] * retencao + aceleracao * _agua[i]) : 0f);

                // Uma célula não pode entregar mais água do que tem. Sem este limite o
                // solver gera água do nada e a caixa "enche" sozinha.
                float saida = _fluxoE[i] + _fluxoD[i] + _fluxoC[i] + _fluxoB[i];
                if (saida * dt > _agua[i] && saida > 1e-6f)
                {
                    float k = _agua[i] / (saida * dt);
                    _fluxoE[i] *= k; _fluxoD[i] *= k; _fluxoC[i] *= k; _fluxoB[i] *= k;
                }
            }
        });
    }

    private float Nivel(int i) => _terreno[i] + _agua[i];

    private void MoverAgua(float dt)
    {
        Parallel.For(0, _h, y =>
        {
            int linha = y * _w;
            for (int x = 0; x < _w; x++)
            {
                int i = linha + x;

                float entra = 0f;
                if (x > 0)       entra += _fluxoD[i - 1];
                if (x < _w - 1)  entra += _fluxoE[i + 1];
                if (y > 0)       entra += _fluxoB[i - _w];
                if (y < _h - 1)  entra += _fluxoC[i + _w];

                float sai = _fluxoE[i] + _fluxoD[i] + _fluxoC[i] + _fluxoB[i];

                float d = _agua[i] + (entra - sai) * dt;
                _aguaNova[i] = d > 0f ? d : 0f;

                // Fluxo líquido nos dois eixos vira a velocidade aparente, que colore
                // a correnteza e distingue um rio de um lago parado.
                float vx = (_fluxoD[i] - _fluxoE[i]);
                float vy = (_fluxoB[i] - _fluxoC[i]);
                _velocidade[i] = MathF.Sqrt(vx * vx + vy * vy);
            }
        });

        (_agua, _aguaNova) = (_aguaNova, _agua);
    }

    private void AplicarInfiltracao(float dt)
    {
        if (InfiltracaoPorSegundo <= 0f) return;
        float fator = MathF.Max(0f, 1f - InfiltracaoPorSegundo * dt);

        Parallel.For(0, _h, y =>
        {
            int linha = y * _w;
            for (int x = 0; x < _w; x++)
            {
                int i = linha + x;
                _agua[i] *= fator;
                // Abaixo de um décimo de milímetro não há o que ver, e deixar resíduo
                // faz a caixa parecer permanentemente molhada.
                if (_agua[i] < 0.1f) _agua[i] = 0f;
            }
        });
    }

    private void CalcularEstatisticas()
    {
        double soma = 0;
        int alagadas = 0;
        for (int i = 0; i < _agua.Length; i++)
        {
            if (_agua[i] <= 0f) continue;
            soma += _agua[i];
            if (_agua[i] >= LimiarAlagamentoMm) alagadas++;
        }

        // volume = profundidade média × área da célula; mm³ para litros é 1e-6.
        double areaCelula = _tamanhoCelulaMm * _tamanhoCelulaMm;
        VolumeLitros = soma * areaCelula * 1e-6;
        AreaAlagadaPercent = 100.0 * alagadas / _agua.Length;
        if (AreaAlagadaPercent > PicoAlagamentoPercent)
            PicoAlagamentoPercent = AreaAlagadaPercent;
    }

    /// <summary>Despeja água num ponto, em coordenadas do sensor.</summary>
    public void DespejarEm(int xSensor, int ySensor, int larguraSensor, int alturaSensor,
                           float quantidadeMm, int raioCelulas = 6)
    {
        int cx = xSensor * _w / Math.Max(1, larguraSensor);
        int cy = ySensor * _h / Math.Max(1, alturaSensor);

        for (int y = cy - raioCelulas; y <= cy + raioCelulas; y++)
        {
            if (y < 0 || y >= _h) continue;
            for (int x = cx - raioCelulas; x <= cx + raioCelulas; x++)
            {
                if (x < 0 || x >= _w) continue;
                float dx = x - cx, dy = y - cy;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > raioCelulas) continue;
                // Queda suave até a borda, para não criar um degrau que vira onda.
                float peso = 1f - dist / raioCelulas;
                _agua[y * _w + x] += quantidadeMm * peso;
            }
        }
    }

    public void Limpar()
    {
        Array.Clear(_agua);
        Array.Clear(_aguaNova);
        Array.Clear(_fluxoE);
        Array.Clear(_fluxoD);
        Array.Clear(_fluxoC);
        Array.Clear(_fluxoB);
        Array.Clear(_velocidade);
        VolumeLitros = 0;
        AreaAlagadaPercent = 0;
        PicoAlagamentoPercent = 0;
        ChuvaRestanteSegundos = 0f;
    }
}
