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
/// Ondas sísmicas sobre o território.
///
/// O que este módulo ensina não é "terremoto sacode tudo igual" — é o contrário. Duas
/// casas à mesma distância do epicentro sofrem danos muito diferentes conforme o solo
/// embaixo delas e a inclinação do terreno onde estão. Foi assim na Cidade do México em
/// 1985, onde o leito de lago antigo amplificou o tremor, e é a razão de mapas de risco
/// sísmico existirem.
///
/// Três coisas se combinam para o dano final:
///   intensidade da onda   ← distância do epicentro e magnitude
///   × amplificação do solo ← rocha quase não amplifica, solo mole amplifica muito
///   × instabilidade da encosta ← declividade, para o risco de deslizamento
///
/// Honestidade científica: a caixa não tem falhas geológicas nem camadas de subsuperfície.
/// O relevo representa o território e o epicentro é escolhido por quem opera. Isto é um
/// modelo didático das consequências, não uma previsão sísmica.
/// </summary>
public sealed class EarthquakeSimulation : ISimulationModule
{
    private readonly int _w, _h;
    private readonly float _tamanhoCelulaMm;

    private readonly float[] _intensidade;   // tremor agora, 0 a 1
    private readonly float[] _danoAcumulado; // maior intensidade já sentida por célula
    private readonly float[] _declividade;   // inclinação local do terreno
    private float[] _terreno;

    /// <summary>Declive máximo que areia seca sustenta, em mm por célula.</summary>
    private readonly float _declivMaximo;

    /// <summary>Velocidade aparente da onda na caixa, em mm/s.
    /// Escolhida para o anel cruzar a caixa em poucos segundos — tempo de a turma
    /// acompanhar. Um valor realista cruzaria 1,25 m instantaneamente.</summary>
    private const float VelocidadeOndaMmPorSegundo = 260f;

    public string Nome => "Terremoto";
    public int Width => _w;
    public int Height => _h;
    public bool Ativo { get; set; }

    /// <summary>Cobertura do solo, para saber onde o tremor é amplificado.</summary>
    public SoilMap? Solo { get; set; }

    /// <summary>Intensidade do tremor em cada célula, agora.</summary>
    public float[] Intensidade => _intensidade;

    /// <summary>Maior intensidade que cada ponto já sentiu — o mapa de danos.</summary>
    public float[] Dano => _danoAcumulado;

    private readonly CamadaVisual[] _camadas;

    /// <summary>
    /// Duas camadas, nesta ordem: o mapa de dano, que fica depois que tudo passa, e a
    /// frente de onda por cima, que só existe enquanto o abalo acontece. Montadas uma vez
    /// no construtor — os dois arrays são readonly e nunca trocam de instância.
    /// </summary>
    public IReadOnlyList<CamadaVisual> Camadas => _camadas;

    /// <summary>Epicentro em coordenadas normalizadas, 0 a 1.</summary>
    public float EpicentroU { get; private set; } = 0.5f;
    public float EpicentroV { get; private set; } = 0.5f;

    /// <summary>Magnitude escolhida, de 3 a 8.</summary>
    public float Magnitude { get; private set; }

    /// <summary>Segundos desde o início do abalo; negativo quando não há sismo ativo.</summary>
    public float TempoDecorrido { get; private set; } = -1f;

    public bool EmAndamento => TempoDecorrido >= 0f && TempoDecorrido < DuracaoTotal;

    /// <summary>Quanto tempo o episódio dura, incluindo o decaimento depois do pulso.</summary>
    public float DuracaoTotal { get; private set; }

    /// <summary>Área com dano relevante, em porcentagem.</summary>
    public double AreaAfetadaPercent { get; private set; }

    /// <summary>Área sob risco de deslizamento: encosta íngreme, solo frágil, tremor forte.</summary>
    public double AreaDeslizamentoPercent { get; private set; }

    public EarthquakeSimulation(int larguraSensor, int alturaSensor, float larguraCaixaMm = 1250f)
    {
        _w = Math.Max(2, larguraSensor / 2);
        _h = Math.Max(2, alturaSensor / 2);
        _tamanhoCelulaMm = larguraCaixaMm / _w;

        // tan(34°) ≈ 0,675 — o ângulo de repouso da areia seca.
        _declivMaximo = MathF.Max(0.5f, _tamanhoCelulaMm * 0.675f);

        int n = _w * _h;
        _intensidade = new float[n];
        _danoAcumulado = new float[n];
        _declividade = new float[n];
        _terreno = new float[n];

        _camadas =
        [
            new CamadaVisual(_danoAcumulado, _w, _h,
                             CamadaVisual.OrdemRisco, ModoDeCor.Risco, Limiar: 0.15f),
            new CamadaVisual(_intensidade, _w, _h,
                             CamadaVisual.OrdemClarao, ModoDeCor.Clarao, Limiar: 0.04f),
        ];
    }

    /// <summary>
    /// Dispara um abalo. O epicentro vem em coordenadas normalizadas para não depender
    /// da resolução — quem chama pensa em "onde na caixa", não em índice de célula.
    /// </summary>
    public void Disparar(float u, float v, float magnitude)
    {
        EpicentroU = Math.Clamp(u, 0f, 1f);
        EpicentroV = Math.Clamp(v, 0f, 1f);
        Magnitude = Math.Clamp(magnitude, 3f, 8f);
        TempoDecorrido = 0f;

        // A onda precisa atravessar a diagonal da caixa, mais a cauda do tremor.
        float diagonal = MathF.Sqrt(_w * _w + _h * _h) * _tamanhoCelulaMm;
        DuracaoTotal = diagonal / VelocidadeOndaMmPorSegundo + 3.5f;

        Array.Clear(_danoAcumulado);
        Ativo = true;
    }

    public void Atualizar(float[] terrenoMm, int larguraTerreno, int alturaTerreno, float dt)
    {
        if (!Ativo) return;

        ReamostrarTerreno(terrenoMm, larguraTerreno, alturaTerreno);
        CalcularDeclividade();

        if (TempoDecorrido < 0f) return;
        TempoDecorrido += dt;

        if (TempoDecorrido > DuracaoTotal)
        {
            // O tremor acaba, mas o mapa de danos permanece — é sobre ele que a turma
            // discute depois.
            Array.Clear(_intensidade);
            TempoDecorrido = DuracaoTotal;
            CalcularEstatisticas();
            return;
        }

        PropagarOnda();
        CalcularEstatisticas();
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

    /// <summary>
    /// Declividade local, que decide o risco de deslizamento. Uma encosta íngreme com
    /// solo solto desliza com tremor moderado; terreno plano não desliza nem com tremor
    /// forte.
    /// </summary>
    private void CalcularDeclividade()
    {
        Parallel.For(0, _h, y =>
        {
            int linha = y * _w;
            for (int x = 0; x < _w; x++)
            {
                int i = linha + x;
                int xr = Math.Min(x + 1, _w - 1);
                int xl = Math.Max(x - 1, 0);
                int yb = Math.Min(y + 1, _h - 1);
                int yt = Math.Max(y - 1, 0);

                float dzdx = (_terreno[linha + xr] - _terreno[linha + xl]) * 0.5f;
                float dzdy = (_terreno[yb * _w + x] - _terreno[yt * _w + x]) * 0.5f;

                // Normaliza pelo ângulo de repouso: areia seca não sustenta encosta
                // mais íngreme que ~34°, então esse é o declive máximo que a caixa pode
                // ter. Dividir por um valor maior faria toda encosta real parecer suave
                // e o risco de deslizamento nunca passaria do limiar.
                float grad = MathF.Sqrt(dzdx * dzdx + dzdy * dzdy);
                _declividade[i] = Math.Clamp(grad / _declivMaximo, 0f, 1f);
            }
        });
    }

    /// <summary>
    /// Propaga um pulso circular a partir do epicentro.
    ///
    /// A intensidade cai por dois motivos somados: espalhamento geométrico — a mesma
    /// energia se distribui num anel cada vez maior — e absorção pelo meio. É por isso
    /// que um terremoto forte é sentido longe, mas destrói perto.
    /// </summary>
    private void PropagarOnda()
    {
        float epx = EpicentroU * _w;
        float epy = EpicentroV * _h;
        float raioAtual = TempoDecorrido * VelocidadeOndaMmPorSegundo / _tamanhoCelulaMm;

        // Magnitude é logarítmica: cada ponto multiplica a energia. A base é menor que a
        // real (que seria ~31× por ponto) porque com ela magnitude 6 já saturaria a caixa
        // inteira e não sobraria gradação entre 5 e 8 — a faixa que a aula usa.
        float energia = MathF.Pow(1.75f, Magnitude - 3f);

        // Largura do pulso em células: o tremor não é instantâneo, dura alguns segundos
        // em cada ponto por onde passa.
        float larguraPulso = 1.8f * VelocidadeOndaMmPorSegundo / _tamanhoCelulaMm;

        var solo = Solo?.Celulas;

        Parallel.For(0, _h, y =>
        {
            int linha = y * _w;
            float dy = y - epy;
            for (int x = 0; x < _w; x++)
            {
                int i = linha + x;
                float dx = x - epx;
                float r = MathF.Sqrt(dx * dx + dy * dy);

                // Envelope do pulso: máximo na frente de onda, caindo dos dois lados.
                float atraso = (r - raioAtual) / larguraPulso;
                float envelope = MathF.Exp(-atraso * atraso * 2.2f);

                if (envelope < 0.002f) { _intensidade[i] = 0f; continue; }

                // Espalhamento geométrico (1/sqrt(r)) mais absorção exponencial.
                float dist = MathF.Max(1.5f, r);
                float atenuacao = 1f / MathF.Sqrt(dist) * MathF.Exp(-dist / 62f);

                float intensidade = energia * atenuacao * envelope * 1.9f;

                // Amplificação do solo: é aqui que o módulo de solo muda o resultado.
                if (solo is not null)
                    intensidade *= AmplificacaoDoSolo(solo[i]);

                intensidade = Math.Clamp(intensidade, 0f, 1f);
                _intensidade[i] = intensidade;

                if (intensidade > _danoAcumulado[i]) _danoAcumulado[i] = intensidade;
            }
        });
    }

    /// <summary>
    /// Quanto cada cobertura amplia o tremor que chega.
    ///
    /// Solo mole e saturado vibra muito mais que rocha — o mesmo abalo destrói um bairro
    /// e poupa o vizinho. É a lição central deste módulo, e o motivo de mapas de risco
    /// sísmico considerarem o tipo de terreno, não só a distância da falha.
    /// </summary>
    private static float AmplificacaoDoSolo(TipoDeSolo tipo) => tipo switch
    {
        // Areia solta e saturada é o pior caso: chega a liquefazer.
        TipoDeSolo.SoloArenoso => 2.1f,
        // Várzea é sedimento fino e encharcado — o caso da Cidade do México.
        TipoDeSolo.Varzea => 2.2f,
        TipoDeSolo.Agricultura => 1.6f,
        TipoDeSolo.Pastagem => 1.3f,
        // Rocha é a referência: não amplifica nada.
        TipoDeSolo.Rocha => 0.75f,
        TipoDeSolo.UrbanoDrenado => 1.15f,
        TipoDeSolo.SoloArgiloso => 1.9f,
        TipoDeSolo.Desmatado => 1.7f,
        TipoDeSolo.Queimado => 1.6f,
        // Compactado transmite melhor que solto, mas ainda amplifica.
        TipoDeSolo.SoloCompactado => 1.3f,
        // Mata não muda a vibração do substrato, mas as raízes seguram a encosta —
        // o efeito dela aparece no deslizamento, não na amplificação.
        TipoDeSolo.Mata => 1.0f,
        // Área urbana aqui representa construção sobre base preparada.
        TipoDeSolo.Impermeavel => 1.15f,
        _ => 1.0f,
    };

    private void CalcularEstatisticas()
    {
        var solo = Solo?.Celulas;
        int afetadas = 0, deslizamentos = 0;

        for (int i = 0; i < _danoAcumulado.Length; i++)
        {
            float dano = _danoAcumulado[i];
            if (dano < 0.18f) continue;
            afetadas++;

            // Deslizamento exige as três condições juntas: tremor, encosta e solo que
            // não segura. Terreno plano não desliza, e mata segura mesmo em declive.
            float retencao = solo is not null
                ? PropriedadesDoSolo.Rapido(solo[i]).ResistenciaAErosao
                : 0.5f;

            float risco = dano * _declividade[i] * (1f - retencao);
            if (risco > 0.16f) deslizamentos++;
        }

        double total = _danoAcumulado.Length;
        AreaAfetadaPercent = 100.0 * afetadas / total;
        AreaDeslizamentoPercent = 100.0 * deslizamentos / total;
    }

    /// <summary>
    /// Traduz a intensidade em algo que o estudante reconhece. A escala Mercalli descreve
    /// efeitos percebidos, não energia liberada — é a que responde "o que aconteceria
    /// aqui", que é a pergunta da aula.
    /// </summary>
    public static string DescreverIntensidade(float intensidade) => intensidade switch
    {
        < 0.10f => "Não sentido",
        < 0.22f => "Sentido por poucos",
        < 0.38f => "Sentido por todos, objetos caem",
        < 0.58f => "Danos leves em construções",
        < 0.78f => "Danos consideráveis",
        _ => "Destruição severa",
    };

    public void Limpar()
    {
        Array.Clear(_intensidade);
        Array.Clear(_danoAcumulado);
        TempoDecorrido = -1f;
        DuracaoTotal = 0f;
        AreaAfetadaPercent = 0;
        AreaDeslizamentoPercent = 0;
    }
}
