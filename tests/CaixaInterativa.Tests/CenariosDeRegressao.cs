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

using System.Security.Cryptography;
using CaixaInterativa.Rendering;

namespace CaixaInterativa.Tests;

/// <summary>
/// Campos sintéticos determinísticos para travar a regressão visual do renderizador.
///
/// Nada aqui usa <c>Random</c>, relógio ou estado global: os mesmos números saem em
/// qualquer execução. É isso que permite comparar o buffer produzido antes e depois de
/// uma refatoração e afirmar que a imagem não mudou.
///
/// Os campos não vêm de rodar as simulações de verdade — vêm de funções fechadas
/// escolhidas para **cruzar todos os limiares** do renderizador (0,25 mm de água;
/// 35 mm de saturação da cor; 0,15 de dano; 0,04 de onda; 0,03 de calor). Rodar as
/// simulações traria a física para dentro de um teste que é sobre composição visual,
/// e o fogo depende de <c>Random</c>.
/// </summary>
internal static class CenariosDeRegressao
{
    /// <summary>Resolução do sensor, igual à do Kinect v1 e do simulador.</summary>
    public const int LarguraSensor = 640;
    public const int AlturaSensor = 480;

    /// <summary>As simulações trabalham em metade da resolução do sensor.</summary>
    public const int LarguraSim = LarguraSensor / 2;
    public const int AlturaSim = AlturaSensor / 2;

    private static double Gauss(double dx, double dy, double sigma)
        => Math.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));

    /// <summary>
    /// Relevo em mm sobre o plano-base. Duas colinas e uma bacia, na mesma forma que o
    /// <c>SimulatedDepthSource</c> gera — mas sem o termo de tempo, para ser fixo.
    /// A amplitude cobre a faixa padrão de −80 mm a +120 mm.
    /// </summary>
    public static float[] Terreno()
    {
        var campo = new float[LarguraSensor * AlturaSensor];
        for (int y = 0; y < AlturaSensor; y++)
        {
            double ny = (y - AlturaSensor / 2.0) / (AlturaSensor / 2.0);
            for (int x = 0; x < LarguraSensor; x++)
            {
                double nx = (x - LarguraSensor / 2.0) / (LarguraSensor / 2.0);

                double h = 0.0;
                h += 0.85 * Gauss(nx - 0.35, ny - 0.20, 0.30);
                h += 0.60 * Gauss(nx + 0.40, ny + 0.30, 0.24);
                h -= 0.45 * Gauss(nx + 0.10, ny - 0.45, 0.28);
                h += 0.10 * Math.Sin(nx * 6.0) * Math.Cos(ny * 5.0);

                campo[y * LarguraSensor + x] = (float)(h * 110.0);
            }
        }
        return campo;
    }

    /// <summary>
    /// Lâmina de água em mm. Vai de 0 a ~45: passa por baixo do limiar de 0,25 mm, pela
    /// faixa em que a opacidade cresce, e por cima dos 35 mm em que ela satura.
    /// </summary>
    public static float[] Agua()
    {
        var campo = new float[LarguraSim * AlturaSim];
        for (int y = 0; y < AlturaSim; y++)
        {
            double ny = y / (double)AlturaSim;
            for (int x = 0; x < LarguraSim; x++)
            {
                double nx = x / (double)LarguraSim;
                double v = Math.Sin(nx * Math.PI * 2.5) * Math.Cos(ny * Math.PI * 1.7);
                campo[y * LarguraSim + x] = (float)(45.0 * Math.Max(0.0, v));
            }
        }
        return campo;
    }

    /// <summary>
    /// Velocidade do fluxo em mm/s, de 0 a ~380 — cruza os 260 mm/s em que a espuma
    /// satura, para exercitar os dois lados da clareação por correnteza.
    /// </summary>
    public static float[] VelocidadeDaAgua()
    {
        var campo = new float[LarguraSim * AlturaSim];
        for (int y = 0; y < AlturaSim; y++)
        {
            double ny = y / (double)AlturaSim;
            for (int x = 0; x < LarguraSim; x++)
            {
                double nx = x / (double)LarguraSim;
                double v = 0.5 + 0.5 * Math.Sin(nx * Math.PI * 3.1 + ny * Math.PI * 2.3);
                campo[y * LarguraSim + x] = (float)(380.0 * v);
            }
        }
        return campo;
    }

    /// <summary>Frente de onda sísmica, 0 a 1 — cruza o limiar de 0,04.</summary>
    public static float[] OndaSismica()
    {
        var campo = new float[LarguraSim * AlturaSim];
        for (int y = 0; y < AlturaSim; y++)
        {
            double ny = (y - AlturaSim / 2.0) / (AlturaSim / 2.0);
            for (int x = 0; x < LarguraSim; x++)
            {
                double nx = (x - LarguraSim / 2.0) / (LarguraSim / 2.0);
                double d = Math.Sqrt(nx * nx + ny * ny);
                // Anel: forte perto de d = 0,55 e desprezível longe dele.
                double v = Math.Exp(-((d - 0.55) * (d - 0.55)) / (2 * 0.09 * 0.09));
                campo[y * LarguraSim + x] = (float)v;
            }
        }
        return campo;
    }

    /// <summary>Dano acumulado, 0 a 1 — cruza o limiar de 0,15 e a saturação em 0,80.</summary>
    public static float[] DanoSismico()
    {
        var campo = new float[LarguraSim * AlturaSim];
        for (int y = 0; y < AlturaSim; y++)
        {
            double ny = (y - AlturaSim / 2.0) / (AlturaSim / 2.0);
            for (int x = 0; x < LarguraSim; x++)
            {
                double nx = (x - LarguraSim / 2.0) / (LarguraSim / 2.0);
                double d = Math.Sqrt(nx * nx + ny * ny);
                campo[y * LarguraSim + x] = (float)Math.Clamp(1.0 - d, 0.0, 1.0);
            }
        }
        return campo;
    }

    /// <summary>Intensidade da chama, 0 a 1 — cruza o limiar de 0,03.</summary>
    public static float[] CalorDoFogo()
    {
        var campo = new float[LarguraSim * AlturaSim];
        for (int y = 0; y < AlturaSim; y++)
        {
            double ny = (y - AlturaSim / 2.0) / (AlturaSim / 2.0);
            for (int x = 0; x < LarguraSim; x++)
            {
                double nx = (x - LarguraSim / 2.0) / (LarguraSim / 2.0);
                double v = Gauss(nx - 0.30, ny + 0.25, 0.35) + 0.4 * Gauss(nx + 0.45, ny - 0.35, 0.20);
                campo[y * LarguraSim + x] = (float)Math.Clamp(v, 0.0, 1.0);
            }
        }
        return campo;
    }

    /// <summary>
    /// Assinatura do buffer renderizado. SHA-256 em vez de igualdade direta porque o
    /// baseline precisa caber no código-fonte e ser lido por uma pessoa no diff.
    /// </summary>
    public static string Hash(byte[] buffer) => Convert.ToHexString(SHA256.HashData(buffer));

    /// <summary>Os oito cenários exigidos, na ordem em que são reportados.</summary>
    public static readonly string[] Nomes =
    [
        "1. topografia (sem simulações)",
        "2. topografia + água",
        "3. topografia + terremoto",
        "4. topografia + fogo",
        "5. água + terremoto",
        "6. água + fogo",
        "7. terremoto + fogo",
        "8. todos ativos",
    ];

    /// <summary>
    /// As camadas de um cenário, na mesma ordem em que o <c>SandboxEngine</c> as monta:
    /// água, depois terremoto (dano e onda), depois fogo.
    ///
    /// Os limiares e as ordens repetem os valores que os módulos declaram. Ficam
    /// escritos aqui de propósito: se alguém mudar um limiar dentro de um módulo, este
    /// teste continua renderizando com o valor antigo e o hash acusa a divergência — que
    /// é exatamente o que se quer saber.
    /// </summary>
    public static IReadOnlyList<CamadaVisual> Camadas(int cenario)
    {
        var (temAgua, temSismo, temFogo) = Combinacoes[cenario];
        var lista = new List<CamadaVisual>(4);

        if (temAgua)
            lista.Add(new CamadaVisual(Agua(), LarguraSim, AlturaSim,
                                       CamadaVisual.OrdemAgua, ModoDeCor.Agua,
                                       Limiar: 0.25f, CampoAuxiliar: VelocidadeDaAgua()));

        if (temSismo)
        {
            lista.Add(new CamadaVisual(DanoSismico(), LarguraSim, AlturaSim,
                                       CamadaVisual.OrdemRisco, ModoDeCor.Risco, Limiar: 0.15f));
            lista.Add(new CamadaVisual(OndaSismica(), LarguraSim, AlturaSim,
                                       CamadaVisual.OrdemClarao, ModoDeCor.Clarao, Limiar: 0.04f));
        }

        if (temFogo)
            lista.Add(new CamadaVisual(CalorDoFogo(), LarguraSim, AlturaSim,
                                       CamadaVisual.OrdemCalor, ModoDeCor.Calor, Limiar: 0.03f));

        return lista;
    }

    /// <summary>Quais camadas cada cenário liga: (água, terremoto, fogo).</summary>
    public static readonly (bool Agua, bool Sismo, bool Fogo)[] Combinacoes =
    [
        (false, false, false),
        (true,  false, false),
        (false, true,  false),
        (false, false, true),
        (true,  true,  false),
        (true,  false, true),
        (false, true,  true),
        (true,  true,  true),
    ];
}
