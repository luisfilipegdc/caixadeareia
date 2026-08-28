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

using CaixaInterativa.Simulation;
using Xunit;

namespace CaixaInterativa.Tests;

/// <summary>
/// Trava as garantias de honestidade científica: o que a interface afirma, e o que o
/// modelo calcula, precisam continuar separados.
/// </summary>
public class HonestidadeCientificaTests
{
    private const int W = 640, H = 480;

    /// <summary>
    /// O resumo exibido ao professor não pode conter números.
    ///
    /// A versão anterior dizia "Absorve 3,2 mm/s · guarda até 160 mm · resiste 95% à
    /// erosão" — aparência de medição hidrológica sobre valores que o próprio código
    /// declara didáticos. Sem dígito não há falsa precisão.
    /// </summary>
    [Theory]
    [MemberData(nameof(TodasAsCoberturas))]
    public void ResumoNaoExibeNumeros(TipoDeSolo tipo)
    {
        string resumo = PropriedadesDoSolo.De(tipo).Resumo;

        Assert.DoesNotContain(resumo, c => char.IsDigit(c));
        Assert.DoesNotContain("%", resumo);
        Assert.DoesNotContain("mm", resumo);
        Assert.False(string.IsNullOrWhiteSpace(resumo));
    }

    [Fact]
    public void ExisteAvisoDeModeloDidatico()
    {
        Assert.False(string.IsNullOrWhiteSpace(PropriedadesDoSolo.AvisoDidatico));
        Assert.Contains("didática", PropriedadesDoSolo.AvisoDidatico);
    }

    /// <summary>
    /// A mudança foi de apresentação, não de modelo. Se alguém alterar um coeficiente
    /// achando que "é só texto", a física da enchente muda e este teste acusa.
    ///
    /// Os valores conferidos são os que já estavam no código antes desta sessão.
    /// </summary>
    [Fact]
    public void CoeficientesDoModeloNaoMudaram()
    {
        var mata = PropriedadesDoSolo.De(TipoDeSolo.Mata);
        Assert.Equal(3.2f, mata.InfiltracaoMmPorSegundo);
        Assert.Equal(160f, mata.ArmazenamentoMm);
        Assert.Equal(0.95f, mata.ResistenciaAErosao);

        var urbano = PropriedadesDoSolo.De(TipoDeSolo.Impermeavel);
        Assert.Equal(0.02f, urbano.InfiltracaoMmPorSegundo);
        Assert.Equal(3f, urbano.ArmazenamentoMm);

        var varzea = PropriedadesDoSolo.De(TipoDeSolo.Varzea);
        Assert.Equal(2.8f, varzea.InfiltracaoMmPorSegundo);
        Assert.Equal(210f, varzea.ArmazenamentoMm);
    }

    /// <summary>
    /// A escala qualitativa precisa preservar a ordem dos coeficientes: se a mata absorve
    /// mais que o asfalto no modelo, o texto não pode dizer o contrário. É essa relação —
    /// e não o número — que a aula usa.
    /// </summary>
    [Fact]
    public void OrdemQualitativaAcompanhaOsCoeficientes()
    {
        var ordenados = PropriedadesDoSolo.Todos
            .Select(PropriedadesDoSolo.De)
            .OrderBy(p => p.InfiltracaoMmPorSegundo)
            .ToList();

        // O que menos infiltra jamais pode ser descrito como quem mais absorve.
        Assert.Contains("Praticamente não absorve", ordenados[0].Resumo);
        Assert.Contains("Absorve muita água", ordenados[^1].Resumo);
    }

    /// <summary>
    /// O volume em litros depende da largura que o sensor cobre — o número que estava
    /// embutido no construtor e não foi medido em campo. Este teste demonstra a
    /// dependência: dobrar a largura quadruplica o volume, porque a área da célula é o
    /// lado ao quadrado.
    ///
    /// É a evidência de por que a interface marca esses valores com "≈".
    /// </summary>
    [Fact]
    public void VolumeEmLitrosEscalaComALarguraConfigurada()
    {
        static double VolumeCom(float larguraMm)
        {
            var agua = new WaterSimulation(W, H, larguraMm) { BordasEscoam = false };
            agua.Solo.Preencher(TipoDeSolo.Rocha);   // infiltração desprezível
            agua.IniciarChuva(10f, 2f);

            var terrenoPlano = new float[W * H];
            for (int i = 0; i < 90; i++) agua.Atualizar(terrenoPlano, W, H, 0.033f);

            return agua.VolumeLitros;
        }

        double estreita = VolumeCom(1250f);
        double larga = VolumeCom(2500f);

        Assert.True(estreita > 0, "A chuva não colocou água na caixa.");

        // Área quadruplica: mesma lâmina, quatro vezes o volume.
        double razao = larga / estreita;
        Assert.InRange(razao, 3.9, 4.1);
    }

    /// <summary>
    /// As porcentagens são razões entre contagens de células e não passam pelo tamanho
    /// da célula. Elas continuam válidas mesmo sem calibração — por isso não levam a
    /// marca de estimativa na interface.
    /// </summary>
    [Fact]
    public void PorcentagemDeAlagamentoNaoDependeDaLargura()
    {
        static double AlagadoCom(float larguraMm)
        {
            var agua = new WaterSimulation(W, H, larguraMm) { BordasEscoam = false };
            agua.Solo.Preencher(TipoDeSolo.Rocha);
            agua.IniciarChuva(10f, 2f);

            var terrenoPlano = new float[W * H];
            for (int i = 0; i < 90; i++) agua.Atualizar(terrenoPlano, W, H, 0.033f);

            return agua.AreaAlagadaPercent;
        }

        Assert.Equal(AlagadoCom(1250f), AlagadoCom(2500f), precision: 6);
    }

    /// <summary>
    /// A cobertura que o combo mostra ao abrir NÃO é a que o modelo contém.
    ///
    /// O combo começa no primeiro item de <see cref="PropriedadesDoSolo.Todos"/>, que é
    /// Mata; o construtor de <see cref="WaterSimulation"/> preenche o solo com areia.
    /// Enquanto essas duas coisas divergirem, a interface precisa sincronizar
    /// explicitamente ao iniciar uma fonte — foi a origem de um bug reproduzido na
    /// validação visual de 28/08/2026, em que atear fogo respondia "não há vegetação que
    /// possa queimar" com "Mata" escrito na tela.
    ///
    /// Se um dia os dois passarem a coincidir, este teste falha e avisa que a
    /// sincronização virou redundante — não que está errada.
    /// </summary>
    [Fact]
    public void PadraoDoModeloDifereDoPrimeiroItemDaLista()
    {
        var primeiroDaLista = PropriedadesDoSolo.Todos[0];
        var padraoDoModelo = new WaterSimulation(W, H).Solo.Em(0, 0);

        Assert.Equal(TipoDeSolo.Mata, primeiroDaLista);
        Assert.Equal(TipoDeSolo.SoloArenoso, padraoDoModelo);
        Assert.NotEqual(primeiroDaLista, padraoDoModelo);
    }

    /// <summary>
    /// Areia não tem combustível suficiente para pegar fogo; mata tem. É o par que
    /// tornava o sintoma do bug visível.
    /// </summary>
    [Fact]
    public void AreiaNaoQueimaMasMataQueima()
    {
        var comAreia = new WaterSimulation(W, H);
        comAreia.Solo.Preencher(TipoDeSolo.SoloArenoso);
        Assert.False(new FireSimulation(W, H, semente: 5) { Solo = comAreia.Solo }.Atear());

        var comMata = new WaterSimulation(W, H);
        comMata.Solo.Preencher(TipoDeSolo.Mata);
        Assert.True(new FireSimulation(W, H, semente: 5) { Solo = comMata.Solo }.Atear());
    }

    public static TheoryData<TipoDeSolo> TodasAsCoberturas()
    {
        var dados = new TheoryData<TipoDeSolo>();
        foreach (var t in PropriedadesDoSolo.Todos) dados.Add(t);
        return dados;
    }
}
