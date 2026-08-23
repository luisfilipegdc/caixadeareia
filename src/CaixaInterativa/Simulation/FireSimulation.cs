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
/// Propagação de queimada sobre o território.
///
/// Esta é a simulação que fecha o ciclo da aula, porque o fogo não termina quando apaga:
/// ele **altera o solo**. A área queimada vira crosta que repele a água, e a próxima
/// chuva encontra um território diferente do que existia antes — alaga mais e leva mais
/// terra. O estudante queima, depois faz chover, e vê a consequência que não aparece na
/// notícia do incêndio.
///
/// O que governa a propagação:
///   combustível  ← mata queima muito, pasto queima rápido e fraco, rocha não queima
///   vento        ← direção e força, que espalham a frente de fogo
///   água         ← rio e área alagada barram o fogo, como no território real
///   encosta      ← fogo sobe morro mais rápido do que desce
/// </summary>
public sealed class FireSimulation : ISimulationModule
{
    /// <summary>Estado de cada célula na queimada.</summary>
    private enum Estado : byte { Intacto = 0, Queimando = 1, Queimado = 2, NaoQueima = 3 }

    private readonly int _w, _h;
    private readonly Estado[] _estado;
    private readonly float[] _combustivel;   // quanto ainda há para queimar, 0 a 1
    private readonly float[] _calor;         // intensidade visível da chama
    private readonly float[] _terreno;
    private readonly Random _sorteio;

    public string Nome => "Queimada";
    public int Width => _w;
    public int Height => _h;
    public bool Ativo { get; set; }

    /// <summary>Cobertura do solo: define o que queima e recebe o resultado.</summary>
    public SoilMap? Solo { get; set; }

    /// <summary>Água na superfície; rio e alagado barram o fogo.</summary>
    public float[]? Agua { get; set; }

    /// <summary>Intensidade da chama por célula, para desenhar.</summary>
    public float[] Calor => _calor;

    /// <summary>Direção do vento em radianos; 0 aponta para a direita.</summary>
    public float VentoDirecao { get; set; }

    /// <summary>Força do vento, de 0 a 1.</summary>
    public float VentoForca { get; set; } = 0.45f;

    public bool EmAndamento { get; private set; }
    public float TempoDecorrido { get; private set; }

    /// <summary>Área já queimada, em porcentagem.</summary>
    public double AreaQueimadaPercent { get; private set; }

    /// <summary>Área com fogo ativo agora.</summary>
    public double AreaEmChamasPercent { get; private set; }

    /// <summary>Onde o fogo começou, em coordenadas normalizadas.</summary>
    public float FocoU { get; private set; }
    public float FocoV { get; private set; }

    private float _acumulador;

    /// <summary>
    /// Contagem absoluta de células em chamas.
    ///
    /// Usar porcentagem para decidir se o fogo acabou não funciona: uma célula acesa em
    /// 76.800 é 0,0013%, abaixo de qualquer limiar razoável — o incêndio morria no
    /// primeiro quadro, antes mesmo de propagar. O fim do fogo é uma pergunta binária,
    /// e merece uma contagem, não uma fração.
    /// </summary>
    private int _celulasEmChamas;

    public FireSimulation(int larguraSensor, int alturaSensor, int semente = 0)
    {
        _w = Math.Max(2, larguraSensor / 2);
        _h = Math.Max(2, alturaSensor / 2);

        int n = _w * _h;
        _estado = new Estado[n];
        _combustivel = new float[n];
        _calor = new float[n];
        _terreno = new float[n];

        // Semente fixa deixa a aula reproduzível quando o professor quiser repetir o
        // mesmo cenário; semente zero sorteia, para o foco cair em lugar diferente.
        _sorteio = semente == 0 ? new Random() : new Random(semente);
    }

    /// <summary>
    /// Quanto cada cobertura alimenta o fogo. Mata tem muita biomassa e queima por
    /// muito tempo; pasto pega fogo fácil mas acaba rápido; rocha e asfalto não queimam.
    /// </summary>
    private static float CombustivelDe(TipoDeSolo tipo) => tipo switch
    {
        TipoDeSolo.Mata => 1.00f,
        TipoDeSolo.Varzea => 0.55f,      // úmida, queima mal
        TipoDeSolo.Pastagem => 0.75f,
        TipoDeSolo.Agricultura => 0.70f,
        TipoDeSolo.Desmatado => 0.35f,   // resto de vegetação seca
        TipoDeSolo.SoloArenoso => 0.05f,
        TipoDeSolo.SoloArgiloso => 0.05f,
        TipoDeSolo.SoloCompactado => 0.03f,
        TipoDeSolo.UrbanoDrenado => 0.15f,  // arborização urbana
        TipoDeSolo.Rocha => 0f,
        TipoDeSolo.Impermeavel => 0f,
        TipoDeSolo.Queimado => 0f,          // já queimou, não há o que queimar
        _ => 0.2f,
    };

    /// <summary>
    /// Ateia fogo num ponto sorteado entre os que têm o que queimar.
    ///
    /// Sortear qualquer ponto faria o incêndio começar no asfalto metade das vezes e não
    /// pegar — o aluno apertaria o botão e nada aconteceria, sem entender por quê.
    /// </summary>
    public bool Atear(float? u = null, float? v = null)
    {
        var solo = Solo;
        if (solo is null) return false;

        Preparar();

        int foco = -1;

        if (u is not null && v is not null)
        {
            int x = Math.Clamp((int)(u.Value * _w), 0, _w - 1);
            int y = Math.Clamp((int)(v.Value * _h), 0, _h - 1);
            int i = y * _w + x;
            if (_combustivel[i] > 0.05f) foco = i;
        }

        if (foco < 0)
        {
            // Junta os candidatos e sorteia entre eles.
            var candidatos = new List<int>();
            for (int i = 0; i < _combustivel.Length; i++)
                if (_combustivel[i] > 0.3f && _estado[i] == Estado.Intacto) candidatos.Add(i);

            if (candidatos.Count == 0) return false;
            foco = candidatos[_sorteio.Next(candidatos.Count)];
        }

        _estado[foco] = Estado.Queimando;
        _calor[foco] = 1f;
        FocoU = (foco % _w) / (float)_w;
        FocoV = (foco / _w) / (float)_h;

        // Vento sorteado a cada incêndio: a mesma mata queima de forma diferente
        // conforme a direção, o que é justamente o que se quer discutir.
        VentoDirecao = (float)(_sorteio.NextDouble() * Math.PI * 2);

        EmAndamento = true;
        Ativo = true;
        TempoDecorrido = 0f;
        _celulasEmChamas = 1;
        _acumulador = 0f;
        return true;
    }

    private void Preparar()
    {
        var solo = Solo!.Celulas;
        for (int i = 0; i < _estado.Length; i++)
        {
            _combustivel[i] = CombustivelDe(solo[i]);
            _estado[i] = _combustivel[i] <= 0.02f ? Estado.NaoQueima : Estado.Intacto;
            _calor[i] = 0f;
        }
    }

    public void Atualizar(float[] terrenoMm, int larguraTerreno, int alturaTerreno, float dt)
    {
        if (!Ativo) return;

        ReamostrarTerreno(terrenoMm, larguraTerreno, alturaTerreno);
        if (!EmAndamento) { CalcularEstatisticas(); return; }

        TempoDecorrido += dt;

        // Passo fixo: a propagação é um autômato celular, e passo variável faria o fogo
        // andar mais rápido num computador mais rápido.
        _acumulador += dt;
        const float Passo = 1f / 20f;
        int passos = 0;
        while (_acumulador >= Passo && passos < 4)
        {
            Propagar(Passo);
            _acumulador -= Passo;
            passos++;
        }

        CalcularEstatisticas();

        if (_celulasEmChamas == 0)
        {
            EmAndamento = false;
            AplicarCicatriz();
        }
    }

    private void ReamostrarTerreno(float[] origem, int ow, int oh)
    {
        Parallel.For(0, _h, y =>
        {
            int sy = y * oh / _h;
            int linhaOrigem = sy * ow;
            int linhaDestino = y * _w;
            for (int x = 0; x < _w; x++)
                _terreno[linhaDestino + x] = origem[linhaOrigem + x * ow / _w];
        });
    }

    private void Propagar(float dt)
    {
        var novos = new List<int>();
        float ventoX = MathF.Cos(VentoDirecao) * VentoForca;
        float ventoY = MathF.Sin(VentoDirecao) * VentoForca;

        for (int y = 0; y < _h; y++)
        {
            int linha = y * _w;
            for (int x = 0; x < _w; x++)
            {
                int i = linha + x;
                if (_estado[i] != Estado.Queimando) continue;

                // Consome o combustível da célula; quando acaba, apaga.
                _combustivel[i] -= dt * 0.55f;
                _calor[i] = Math.Clamp(_combustivel[i] * 1.4f, 0f, 1f);

                if (_combustivel[i] <= 0f)
                {
                    _estado[i] = Estado.Queimado;
                    _calor[i] = 0f;
                    continue;
                }

                // Tenta acender os quatro vizinhos.
                TentarAcender(x - 1, y, -1, 0, ventoX, ventoY, i, novos);
                TentarAcender(x + 1, y, +1, 0, ventoX, ventoY, i, novos);
                TentarAcender(x, y - 1, 0, -1, ventoX, ventoY, i, novos);
                TentarAcender(x, y + 1, 0, +1, ventoX, ventoY, i, novos);
            }
        }

        foreach (int i in novos)
        {
            if (_estado[i] != Estado.Intacto) continue;
            _estado[i] = Estado.Queimando;
            _calor[i] = 0.6f;
        }
    }

    private void TentarAcender(int x, int y, int dx, int dy,
                               float ventoX, float ventoY, int origem, List<int> novos)
    {
        if (x < 0 || x >= _w || y < 0 || y >= _h) return;
        int i = y * _w + x;
        if (_estado[i] != Estado.Intacto) return;

        // Água apaga: rio e área alagada são as barreiras naturais que os brigadistas
        // usam, e o aluno descobre isso deixando um rio no caminho do fogo.
        if (Agua is not null && Agua[i] > 2f) { _estado[i] = Estado.NaoQueima; return; }

        float chance = _combustivel[i] * 0.30f;

        // Vento a favor empurra a frente de fogo.
        chance *= 1f + (dx * ventoX + dy * ventoY) * 1.6f;

        // Fogo sobe encosta mais rápido: a chama pré-aquece o combustível acima dela.
        float subida = _terreno[i] - _terreno[origem];
        if (subida > 0) chance *= 1f + Math.Min(1.2f, subida * 0.06f);

        if (chance <= 0f) return;
        if (_sorteio.NextDouble() < chance) novos.Add(i);
    }

    /// <summary>
    /// Grava a cicatriz do incêndio no mapa de solo.
    ///
    /// É o ponto central deste módulo: depois que o fogo apaga, o território mudou. A
    /// chuva seguinte cai sobre crosta hidrofóbica, infiltra pouco e leva o solo — a
    /// consequência que o incêndio deixa e que ninguém filma.
    /// </summary>
    private void AplicarCicatriz()
    {
        var solo = Solo;
        if (solo is null) return;

        for (int i = 0; i < _estado.Length; i++)
            if (_estado[i] == Estado.Queimado)
                solo.Celulas[i] = TipoDeSolo.Queimado;
    }

    private void CalcularEstatisticas()
    {
        int queimados = 0, chamas = 0, total = 0;
        for (int i = 0; i < _estado.Length; i++)
        {
            if (_estado[i] == Estado.NaoQueima) continue;
            total++;
            if (_estado[i] == Estado.Queimado) queimados++;
            else if (_estado[i] == Estado.Queimando) chamas++;
        }

        _celulasEmChamas = chamas;

        double baseTotal = Math.Max(1, total);
        AreaQueimadaPercent = 100.0 * queimados / baseTotal;
        AreaEmChamasPercent = 100.0 * chamas / baseTotal;
    }

    /// <summary>Direção do vento em palavras, para a interface.</summary>
    public string VentoPorExtenso()
    {
        string[] rosa = ["leste", "nordeste", "norte", "noroeste",
                         "oeste", "sudoeste", "sul", "sudeste"];
        int i = (int)MathF.Round(VentoDirecao / (MathF.PI / 4)) & 7;
        return rosa[i];
    }

    public void Limpar()
    {
        Array.Clear(_estado);
        Array.Clear(_combustivel);
        Array.Clear(_calor);
        EmAndamento = false;
        TempoDecorrido = 0f;
        _acumulador = 0f;
        _celulasEmChamas = 0;
        AreaQueimadaPercent = 0;
        AreaEmChamasPercent = 0;
    }
}
