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
/// Decide quando um sensor que parou de entregar quadros deve ser considerado em falha.
///
/// Existe separada do laço de captura de propósito: o laço é código de interop depurado
/// contra hardware real, e a regra de "quando desistir" é lógica pura que merece teste.
/// A fiação no <see cref="KinectV1Source"/> fica em três linhas.
///
/// **O problema que resolve.** O laço espera o evento de quadro com tempo limite e, ao
/// estourar, apenas tenta de novo. Uma desconexão física levanta exceção e aciona a
/// reconexão automática; mas um sensor que simplesmente **emudece** — sem desconectar —
/// produz tela congelada, sem mensagem e sem reconexão. É o modo de falha mais provável
/// numa sala de aula, e era a pendência P6.
/// </summary>
public sealed class PoliticaDeTimeout
{
    /// <summary>
    /// Quanto tempo sem nenhum quadro antes de declarar falha.
    ///
    /// Três segundos. O raciocínio: o sensor entrega ~30 quadros por segundo, então
    /// mesmo um engasgo severo produz algo em bem menos que isso. Três segundos é longo
    /// o bastante para não disparar por uma oscilação de USB, e curto o bastante para o
    /// professor ver "Reconectando…" antes de concluir que o programa travou.
    ///
    /// **Não foi validado com hardware** — não havia sensor disponível quando isto foi
    /// escrito. É um ponto de partida conservador, registrado como tal.
    /// </summary>
    public const int LimiteMs = 3000;

    private readonly int _esperaPorTentativaMs;
    private readonly int _limiteDeTentativas;

    private int _consecutivos;

    /// <param name="esperaPorTentativaMs">
    /// Quanto tempo cada espera pelo evento de quadro dura. É daqui que sai quantas
    /// tentativas cabem no limite.
    /// </param>
    public PoliticaDeTimeout(int esperaPorTentativaMs = 200)
    {
        if (esperaPorTentativaMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(esperaPorTentativaMs));

        _esperaPorTentativaMs = esperaPorTentativaMs;
        _limiteDeTentativas = Math.Max(1, LimiteMs / esperaPorTentativaMs);
    }

    /// <summary>Quantas esperas seguidas sem quadro toleramos antes de desistir.</summary>
    public int LimiteDeTentativas => _limiteDeTentativas;

    /// <summary>Esperas sem quadro acumuladas desde o último quadro bom.</summary>
    public int Consecutivos => _consecutivos;

    /// <summary>Tempo aproximado sem receber quadro, em milissegundos.</summary>
    public int SilencioMs => _consecutivos * _esperaPorTentativaMs;

    /// <summary>
    /// Registra uma espera que estourou sem quadro. Devolve <c>true</c> no momento exato
    /// em que o silêncio passa do limite — uma única vez, para não disparar falha a cada
    /// tentativa seguinte enquanto o sensor continua mudo.
    /// </summary>
    public bool RegistrarTimeout()
    {
        _consecutivos++;
        return _consecutivos == _limiteDeTentativas;
    }

    /// <summary>Um quadro chegou: o sensor está vivo e a contagem recomeça.</summary>
    public void RegistrarQuadro() => _consecutivos = 0;

    /// <summary>Mensagem para o professor, em linguagem de sala de aula.</summary>
    public string Mensagem() =>
        $"O sensor parou de enviar imagens há {SilencioMs / 1000.0:F0} segundos. " +
        "Verifique o cabo USB e a fonte de energia.";
}
