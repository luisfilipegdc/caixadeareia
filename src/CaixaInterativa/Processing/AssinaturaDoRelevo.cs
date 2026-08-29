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

namespace CaixaInterativa.Processing;

/// <summary>O que mudou entre dois relevos, em milímetros.</summary>
/// <param name="MesmoRelevo">
/// Falso quando a diferença passa de <see cref="AssinaturaDoRelevo.ToleranciaMm"/> em
/// alguma região — ou seja, quando comparar os dois resultados deixa de isolar a variável
/// que a aula queria investigar.
/// </param>
/// <param name="DiferencaMaximaMm">Maior diferença encontrada em uma região.</param>
/// <param name="DiferencaMediaMm">Diferença média sobre todas as regiões.</param>
public readonly record struct ComparacaoDeRelevo(
    bool MesmoRelevo,
    float DiferencaMaximaMm,
    float DiferencaMediaMm);

/// <summary>
/// Um resumo grosseiro do relevo, suficiente para responder a uma única pergunta:
/// **a areia é a mesma de antes?**
///
/// Existe por causa de um risco pedagógico concreto. A comparação entre cenários do
/// projeto conclui coisas como *"a área urbana teve 2,4× o alagamento da mata"*. Essa
/// frase só é verdadeira se o relevo tiver ficado igual entre as duas execuções. Se um
/// aluno mexeu na areia no meio, o software estaria atribuindo à cobertura uma diferença
/// que veio do terreno — uma conclusão falsa, apresentada com a autoridade de um
/// resultado medido.
///
/// **Como funciona.** O campo de alturas é reduzido a uma grade de 16×12 regiões, cada
/// uma guardando a altura média da sua área. Duas assinaturas são compatíveis quando
/// nenhuma região difere mais que a tolerância.
///
/// **Por que médias por região, e não um hash.** Um hash responde "é idêntico?", e a
/// resposta seria sempre "não": o Kinect tem 2–4 mm de ruído e nenhum quadro é igual ao
/// anterior. A média sobre milhares de pixels dilui o ruído para bem abaixo de um
/// milímetro, enquanto uma escavação de verdade mexe em centímetros. A pergunta certa é
/// "mudou o suficiente para importar?", e ela precisa de tolerância, não de igualdade.
///
/// **Classificação:** derivação matemática sobre uma medição. Não é modelo didático —
/// não há parâmetro inventado aqui, só média aritmética e subtração.
/// </summary>
public sealed class AssinaturaDoRelevo
{
    /// <summary>Regiões no eixo horizontal.</summary>
    public const int Colunas = 16;

    /// <summary>Regiões no eixo vertical.</summary>
    public const int Linhas = 12;

    /// <summary>
    /// Quanto uma região pode variar antes de considerarmos que o relevo mudou.
    ///
    /// Um centímetro. Fica muito acima do ruído do sensor depois da média — a 640×480 em
    /// 16×12 regiões, cada média cobre 1.600 pixels, e 3 mm de ruído independente viram
    /// menos de 0,1 mm no resultado. E fica muito abaixo do que um estudante faz de
    /// propósito: quem cava um vale ou empilha um morro mexe em vários centímetros.
    ///
    /// A escolha erra deliberadamente para o lado de avisar demais. Um aviso a mais é
    /// incômodo; uma comparação falsa apresentada como conclusão da aula é pior.
    /// </summary>
    public const float ToleranciaMm = 10f;

    private readonly float[] _medias;

    private AssinaturaDoRelevo(float[] medias) => _medias = medias;

    /// <summary>Altura média de cada região, em mm. Somente leitura.</summary>
    public IReadOnlyList<float> Medias => _medias;

    /// <summary>
    /// Calcula a assinatura de um campo de alturas. Devolve <c>null</c> quando o campo
    /// não serve — sem dimensão suficiente para as regiões, ou menor do que diz ser.
    /// Nesse caso quem chama simplesmente não registra assinatura, e a comparação avisa
    /// que não pôde verificar.
    /// </summary>
    public static AssinaturaDoRelevo? De(float[]? alturasMm, int largura, int altura)
    {
        if (alturasMm is null) return null;
        if (largura < Colunas || altura < Linhas) return null;
        if (alturasMm.Length < largura * altura) return null;

        var medias = new float[Colunas * Linhas];

        for (int regiaoY = 0; regiaoY < Linhas; regiaoY++)
        {
            int y0 = regiaoY * altura / Linhas;
            int y1 = (regiaoY + 1) * altura / Linhas;

            for (int regiaoX = 0; regiaoX < Colunas; regiaoX++)
            {
                int x0 = regiaoX * largura / Colunas;
                int x1 = (regiaoX + 1) * largura / Colunas;

                // Soma em double: são milhares de parcelas por região, e acumular em
                // float perderia precisão justamente onde queremos comparar milímetros.
                double soma = 0;
                int contagem = 0;

                for (int y = y0; y < y1; y++)
                {
                    int linha = y * largura;
                    for (int x = x0; x < x1; x++)
                    {
                        soma += alturasMm[linha + x];
                        contagem++;
                    }
                }

                medias[regiaoY * Colunas + regiaoX] =
                    contagem > 0 ? (float)(soma / contagem) : 0f;
            }
        }

        return new AssinaturaDoRelevo(medias);
    }

    /// <summary>
    /// Compara com outra assinatura. Assinaturas sempre têm o mesmo tamanho, porque a
    /// grade é fixa — o campo original pode ter qualquer resolução.
    /// </summary>
    /// <summary>
    /// Uma linha que identifica este relevo no registro de operação.
    ///
    /// Não serve para comparar — quem compara é <see cref="Comparar"/>, bloco a bloco.
    /// Serve para, lendo o registro depois da aula, reconhecer se duas execuções
    /// aconteceram sobre o mesmo terreno sem ter as 192 médias na frente.
    /// </summary>
    public string Resumo
    {
        get
        {
            float menor = float.MaxValue, maior = float.MinValue;
            double soma = 0;
            foreach (float m in _medias)
            {
                if (m < menor) menor = m;
                if (m > maior) maior = m;
                soma += m;
            }
            return $"{Colunas}x{Linhas} blocos · {menor:F0} a {maior:F0} mm · " +
                   $"média {soma / _medias.Length:F0} mm";
        }
    }

    public ComparacaoDeRelevo Comparar(AssinaturaDoRelevo outra)
    {
        ArgumentNullException.ThrowIfNull(outra);

        float maior = 0f;
        double soma = 0;

        for (int i = 0; i < _medias.Length; i++)
        {
            float diferenca = Math.Abs(_medias[i] - outra._medias[i]);
            if (diferenca > maior) maior = diferenca;
            soma += diferenca;
        }

        return new ComparacaoDeRelevo(
            MesmoRelevo: maior <= ToleranciaMm,
            DiferencaMaximaMm: maior,
            DiferencaMediaMm: (float)(soma / _medias.Length));
    }
}
