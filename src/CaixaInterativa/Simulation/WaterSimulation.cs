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

    // Onde a água passou com força sobre solo frágil. Não altera a areia — mostra
    // onde ela seria levada, para o estudante decidir o que fazer a respeito.
    private readonly float[] _erosao;

    // Quanta água cada célula já absorveu. Solo encharcado para de absorver — é por isso
    // que a segunda chuva alaga mais que a primeira, mesmo sendo igual.
    private readonly float[] _saturacao;

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

    /// <summary>Água que chega à borda da caixa escoa para fora, como num terreno aberto.</summary>
    public bool BordasEscoam { get; set; } = true;

    /// <summary>Atrito do leito: segura a água em terreno plano e evita oscilação.</summary>
    public float Amortecimento { get; set; } = 0.14f;

    /// <summary>
    /// Multiplica a infiltração de todos os solos. Serve para acelerar o contraste numa
    /// aula curta sem alterar a proporção entre os tipos, que é o que se quer ensinar.
    /// </summary>
    public float EscalaInfiltracao { get; set; } = 1f;

    /// <summary>Profundidade da água por célula, para quem desenha.</summary>
    public float[] Profundidade => _agua;

    /// <summary>Velocidade do fluxo por célula, em mm/s.</summary>
    public float[] Velocidade => _velocidade;

    private readonly CamadaVisual[] _camadas = new CamadaVisual[1];

    /// <summary>
    /// Uma camada: a lâmina d'água, com a velocidade como campo auxiliar para clarear
    /// a correnteza.
    ///
    /// Remontada a cada acesso, e não guardada no construtor como nos outros módulos,
    /// porque <c>MoverAgua</c> troca <c>_agua</c> por <c>_aguaNova</c> a cada substep:
    /// uma camada montada uma vez só apontaria para o buffer errado. Escrever a struct
    /// no array já existente não aloca.
    /// </summary>
    public IReadOnlyList<CamadaVisual> Camadas
    {
        get
        {
            _camadas[0] = new CamadaVisual(
                _agua, _w, _h,
                CamadaVisual.OrdemAgua, ModoDeCor.Agua,
                Limiar: 0.25f,
                CampoAuxiliar: _velocidade);
            return _camadas;
        }
    }

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
        _erosao = new float[n];
        _saturacao = new float[n];
        Solo = new SoilMap(_w, _h);
        Solo.Preencher(TipoDeSolo.SoloArenoso);
    }

    /// <summary>Cobertura do solo, na mesma grade da simulação.</summary>
    public SoilMap Solo { get; }

    /// <summary>Erosão acumulada por célula, em unidades relativas.</summary>
    public float[] Erosao => _erosao;

    /// <summary>Água armazenada no solo, em mm, por célula.</summary>
    public float[] Saturacao => _saturacao;

    /// <summary>
    /// Quanto o solo já encheu, de 0 a 1, na média da caixa. Perto de 1 o terreno perdeu
    /// a capacidade de absorver e qualquer chuva vira enxurrada.
    /// </summary>
    public double SaturacaoMediaPercent { get; private set; }

    /// <summary>
    /// Velocidade com que o solo devolve água ao subsolo e volta a poder absorver.
    /// Lenta de propósito: um terreno encharcado leva dias para secar, e é isso que faz
    /// a chuva de amanhã ser mais perigosa que a de hoje.
    /// </summary>
    public float DrenagemProfundaPorSegundo { get; set; } = 0.9f;

    /// <summary>Volume total infiltrado no episódio, em litros.</summary>
    public double InfiltradoLitros { get; private set; }

    /// <summary>Volume que escoou pelas bordas, em litros.</summary>
    public double EscoadoLitros { get; private set; }

    /// <summary>Erosão total acumulada, para comparar cenários.</summary>
    public double ErosaoTotal { get; private set; }

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
        DrenarSolo(dt);
        AcumularErosao(dt);
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
        var solo = Solo.Celulas;

        Parallel.For(0, _h, y =>
        {
            int linha = y * _w;
            for (int x = 0; x < _w; x++)
            {
                int i = linha + x;

                // A rugosidade da cobertura entra como atrito: a serapilheira da mata
                // retarda o escoamento, o asfalto deixa correr solto. É o segundo efeito
                // do desmatamento, depois da infiltração — a água não só entra menos,
                // como chega mais rápido lá embaixo.
                //
                // O multiplicador é modesto de propósito. Com 3x, a mata segurava tanta
                // água na superfície que a área alagada ficava MAIOR que a da cidade —
                // o modelo confundia "escoamento retardado", que é bom, com "empoçado",
                // que é o que se quer medir.
                var prop = PropriedadesDoSolo.Rapido(solo[i]);
                float atrito = Amortecimento * (1f + 1.1f * prop.Rugosidade);
                float retencao = 1f - atrito * dt;
                if (retencao < 0f) retencao = 0f;

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

    /// <summary>
    /// Infiltração célula a célula, conforme a cobertura do solo.
    ///
    /// É aqui que o módulo de solo muda o resultado: sob a mesma chuva, a mata absorve
    /// e a área urbana devolve tudo para o escoamento. Toda a diferença entre uma bacia
    /// preservada e uma impermeabilizada nasce destas poucas linhas.
    ///
    /// A infiltração é uma taxa em mm/s, não uma fração: um solo absorve um tanto por
    /// segundo até a água acabar, e não uma porcentagem do que está por cima — que faria
    /// uma poça funda infiltrar mais rápido que uma rasa, o contrário do que ocorre.
    /// </summary>
    private void AplicarInfiltracao(float dt)
    {
        var solo = Solo.Celulas;
        double infiltradoMm = 0;
        object trava = new();

        Parallel.For(0, _h,
            () => 0.0,
            (y, _, parcial) =>
            {
                int linha = y * _w;
                for (int x = 0; x < _w; x++)
                {
                    int i = linha + x;
                    if (_agua[i] <= 0f) continue;

                    var prop = PropriedadesDoSolo.Rapido(solo[i]);

                    // Solo encharcado absorve menos. A capacidade cai conforme o
                    // armazenamento enche, e chega a zero quando o solo satura — o
                    // momento em que a chuva deixa de infiltrar e passa a escorrer.
                    float espaco = MathF.Max(0f, prop.ArmazenamentoMm - _saturacao[i]);
                    float fracaoLivre = prop.ArmazenamentoMm > 0.01f
                        ? espaco / prop.ArmazenamentoMm
                        : 0f;

                    float capacidade = prop.InfiltracaoMmPorSegundo * EscalaInfiltracao
                                       * fracaoLivre * dt;
                    float absorvido = MathF.Min(MathF.Min(_agua[i], capacidade), espaco);

                    _agua[i] -= absorvido;
                    _saturacao[i] += absorvido;
                    parcial += absorvido;

                    // Abaixo de um décimo de milímetro não há o que ver, e deixar
                    // resíduo faz a caixa parecer permanentemente molhada.
                    if (_agua[i] < 0.1f) { parcial += _agua[i]; _agua[i] = 0f; }
                }
                return parcial;
            },
            parcial => { lock (trava) infiltradoMm += parcial; });

        double areaCelula = _tamanhoCelulaMm * _tamanhoCelulaMm;
        InfiltradoLitros += infiltradoMm * areaCelula * 1e-6;
    }

    /// <summary>
    /// O solo devolve água ao subsolo e recupera capacidade de absorver, devagar.
    /// Sem isso a caixa saturaria na primeira chuva e nunca mais absorveria nada.
    /// </summary>
    private void DrenarSolo(float dt)
    {
        if (DrenagemProfundaPorSegundo <= 0f) return;
        float perda = DrenagemProfundaPorSegundo * dt;

        Parallel.For(0, _h, y =>
        {
            int linha = y * _w;
            for (int x = 0; x < _w; x++)
            {
                int i = linha + x;
                if (_saturacao[i] <= 0f) continue;
                _saturacao[i] = MathF.Max(0f, _saturacao[i] - perda);
            }
        });
    }

    /// <summary>
    /// Acumula onde a água passa com força sobre solo frágil.
    ///
    /// Não movemos areia: o relevo vem do sensor, e mexer nele faria o mapa divergir do
    /// que está fisicamente na caixa. O que o sistema entrega é a previsão — "aqui o
    /// solo seria levado" — e o estudante decide se protege a encosta ou se cava para
    /// ver o que acontece.
    /// </summary>
    private void AcumularErosao(float dt)
    {
        var solo = Solo.Celulas;
        double total = 0;
        object trava = new();

        Parallel.For(0, _h,
            () => 0.0,
            (y, _, parcial) =>
            {
                int linha = y * _w;
                for (int x = 0; x < _w; x++)
                {
                    int i = linha + x;
                    if (_agua[i] < 0.5f) continue;

                    var prop = PropriedadesDoSolo.Rapido(solo[i]);
                    float fragilidade = 1f - prop.ResistenciaAErosao;
                    if (fragilidade <= 0f) continue;

                    // Só há arraste acima de uma velocidade mínima: água parada num
                    // lago não erode, por mais funda que seja.
                    float v = _velocidade[i];
                    if (v < 40f) continue;

                    float desgaste = (v - 40f) * fragilidade * dt * 0.0012f;
                    _erosao[i] += desgaste;
                    parcial += desgaste;
                }
                return parcial;
            },
            parcial => { lock (trava) total += parcial; });

        ErosaoTotal += total;
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
        // Saturação média, para a interface avisar quando o terreno encheu.
        var solo = Solo.Celulas;
        double satTotal = 0;
        for (int i = 0; i < _saturacao.Length; i++)
        {
            float cap = PropriedadesDoSolo.Rapido(solo[i]).ArmazenamentoMm;
            if (cap > 0.01f) satTotal += Math.Min(1f, _saturacao[i] / cap);
        }
        SaturacaoMediaPercent = 100.0 * satTotal / _saturacao.Length;

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

    /// <summary>
    /// Enche o solo antecipadamente, como se já tivesse chovido antes.
    ///
    /// É o dado que falta em quase toda explicação de enchente: quando a chuva extrema
    /// chega, o solo em geral já está cheio das chuvas dos dias anteriores. Sem isso, a
    /// simulação mostraria uma bacia seca recebendo a tempestade, que é o caso fácil.
    /// </summary>
    public void PreSaturar(float fracao)
    {
        fracao = Math.Clamp(fracao, 0f, 1f);
        var solo = Solo.Celulas;
        for (int i = 0; i < _saturacao.Length; i++)
            _saturacao[i] = PropriedadesDoSolo.Rapido(solo[i]).ArmazenamentoMm * fracao;
        CalcularEstatisticas();
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
        Array.Clear(_erosao);
        Array.Clear(_saturacao);
        SaturacaoMediaPercent = 0;
        InfiltradoLitros = 0;
        EscoadoLitros = 0;
        ErosaoTotal = 0;
        VolumeLitros = 0;
        AreaAlagadaPercent = 0;
        PicoAlagamentoPercent = 0;
        ChuvaRestanteSegundos = 0f;
    }
}
