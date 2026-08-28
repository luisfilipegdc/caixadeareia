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
/// Um cenário pedagógico: a configuração de território e de evento que reproduz uma
/// situação real, para a turma investigar em vez de só assistir.
///
/// O relevo continua vindo das mãos dos estudantes — o cenário define **o que cobre**
/// esse relevo e **que evento** acontece sobre ele. Essa divisão é proposital: a
/// pergunta da aula não é "como era o vale do Taquari", é "o que aconteceria com o vale
/// que vocês construíram, se fosse ocupado assim e chovesse assim".
/// </summary>
/// <remarks>
/// <b>Não conectado ao fluxo oficial.</b> Nada no aplicativo referencia estes cenários:
/// eles não aparecem em nenhuma tela e não são aplicados por nenhum caminho. A primeira
/// atividade oficial — Urbanização e Enchentes — não os usa, de propósito.
///
/// <b>Por que não foram ligados.</b> Três deles pintam a cobertura com
/// <c>PintarPorAltitude</c> em cotas absolutas (45 mm e 30 mm). Sobre areia real essas
/// cotas não significam nada fixo: a faixa útil depende da calibração de cada montagem —
/// numa medição real ela foi de −11 mm a 102 mm, e "acima de 45 mm" caiu em lugar
/// arbitrário. Ligados assim, pintariam cidade no topo do morro.
///
/// <b>Não ative automaticamente sem revisar.</b> O que vale a pena aqui é a forma do
/// registro — contexto, pergunta, chuva, duração e saturação inicial — e não os limiares.
/// </remarks>
public sealed record Cenario(
    string Nome,
    string Contexto,
    string Pergunta,
    Action<SoilMap, float[], int, int> Aplicar,
    float ChuvaMmPorSegundo = 0f,
    float ChuvaSegundos = 0f,
    float SaturacaoInicial = 0f,
    string Observacao = "")
{
    /// <summary>
    /// Cenários disponíveis. Cada um traz o contexto real e a pergunta que a turma
    /// investiga — sem a pergunta vira demonstração, e demonstração não ensina.
    /// </summary>
    public static readonly Cenario[] Todos =
    [
        new Cenario(
            "Livre",
            "Sem cenário: você escolhe a cobertura e o evento.",
            "",
            (solo, _, _, _) => solo.Preencher(TipoDeSolo.Mata)),

        new Cenario(
            "Enchente no Rio Grande do Sul",
            "Em 2024, o Rio Grande do Sul viveu a maior enchente da sua história. " +
            "Choveu por dias sobre um solo já encharcado, em vales onde as cidades " +
            "ocupam a planície do rio e as encostas foram convertidas em lavoura.",
            "Por que a água chegou onde chegou — e o que teria sido diferente se a " +
            "várzea não estivesse ocupada?",
            (solo, terreno, tw, th) =>
            {
                // Encostas em lavoura: a conversão que reduz a infiltração da bacia.
                solo.Preencher(TipoDeSolo.Agricultura);
                // Meia encosta ainda com mata remanescente.
                solo.PintarPorAltitude(terreno, tw, th, 45f, acima: true, TipoDeSolo.Mata);
                // A cidade no fundo do vale, exatamente onde o rio transborda.
                solo.PintarPorAltitude(terreno, tw, th, 30f, acima: false, TipoDeSolo.Impermeavel);
            },
            ChuvaMmPorSegundo: 9f,
            ChuvaSegundos: 14f,
            // O ponto que a notícia não conta: quando a chuva extrema chegou, o solo
            // já estava cheio de chuvas anteriores e não tinha para onde absorver.
            SaturacaoInicial: 0.75f,
            Observacao: "O solo começa 75% encharcado, como estava antes da chuva de maio."),

        new Cenario(
            "A mesma enchente, com a várzea preservada",
            "O mesmo território e a mesma chuva — mas com a planície de inundação " +
            "livre de ocupação, cumprindo a função de guardar a cheia.",
            "A área alagada diminuiu? Para onde foi a água que a várzea absorveu?",
            (solo, terreno, tw, th) =>
            {
                solo.Preencher(TipoDeSolo.Agricultura);
                solo.PintarPorAltitude(terreno, tw, th, 45f, acima: true, TipoDeSolo.Mata);
                // A diferença está aqui: o fundo do vale é várzea, não cidade.
                solo.PintarPorAltitude(terreno, tw, th, 30f, acima: false, TipoDeSolo.Varzea);
            },
            ChuvaMmPorSegundo: 9f,
            ChuvaSegundos: 14f,
            SaturacaoInicial: 0.75f,
            Observacao: "Compare com o cenário anterior: mesma chuva, mesma encosta."),

        new Cenario(
            "Cidade que planejou a drenagem",
            "A mesma cidade, construída com piso permeável, praças que alagam de " +
            "propósito e telhados que retêm a chuva.",
            "Quanto uma decisão de projeto urbano muda o desfecho de uma tempestade?",
            (solo, terreno, tw, th) =>
            {
                solo.Preencher(TipoDeSolo.Agricultura);
                solo.PintarPorAltitude(terreno, tw, th, 45f, acima: true, TipoDeSolo.Mata);
                solo.PintarPorAltitude(terreno, tw, th, 30f, acima: false, TipoDeSolo.UrbanoDrenado);
            },
            ChuvaMmPorSegundo: 9f,
            ChuvaSegundos: 14f,
            SaturacaoInicial: 0.75f),

        new Cenario(
            "Depois da queimada",
            "Uma área de mata que acabou de queimar. O fogo deixou uma crosta que " +
            "repele a água, e a estação chuvosa está chegando.",
            "O que acontece com a primeira chuva forte depois de um incêndio?",
            (solo, _, _, _) => solo.Preencher(TipoDeSolo.Queimado),
            ChuvaMmPorSegundo: 10f,
            ChuvaSegundos: 12f,
            Observacao: "Compare a erosão com a de uma mata intacta."),

        new Cenario(
            "Bacia preservada",
            "Uma bacia com mata em toda a extensão, do topo da encosta à margem do rio.",
            "Qual é o limite? Mesmo preservada, uma chuva forte o suficiente alaga?",
            (solo, _, _, _) => solo.Preencher(TipoDeSolo.Mata),
            ChuvaMmPorSegundo: 9f,
            ChuvaSegundos: 14f,
            SaturacaoInicial: 0.75f,
            Observacao: "É o piso de comparação para todos os outros cenários."),
    ];
}
