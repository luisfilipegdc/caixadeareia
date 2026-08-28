# Urbanização e Enchentes

> Primeira atividade pedagógica oficial da Caixa de Areia Interativa.
> **Nunca foi usada com uma turma.** É proposta testada em bancada, não material validado.

## Pergunta investigativa

> **Mantendo o mesmo relevo e a mesma chuva, o que muda no caminho da água quando trocamos
> a cobertura do solo?**

## Duração

10 a 15 minutos, incluindo a discussão. As duas execuções levam cerca de 25 segundos cada.

## Objetivo

Que a turma faça — e veja — um **experimento controlado**: uma variável muda, todas as
outras ficam. Quem sai da aula sabendo que precisou manter o relevo igual para poder
comparar aprendeu controle de variáveis sem ninguém usar a expressão.

## Preparação

1. Ligue a caixa e confirme a calibração.
2. A turma molda o relevo na areia. **Qualquer relevo serve** — vale um vale, uma encosta,
   uma bacia fechada. Não há relevo "certo".
3. Abra a projeção, para a turma acompanhar.
4. Toque em **Urbanização e Enchentes**, na seção ATIVIDADE.

A partir daí o programa cuida das condições. Você não escolhe cobertura, nem intensidade,
nem duração: elas são as mesmas nos dois passos, e é isso que torna a comparação honesta.

## Passo A — Mata

A tela mostra `PASSO A · MATA`. Toque em **Fazer chover**.

Enquanto chove, peça que observem **por onde a água desce** e **onde ela se acumula**. Ao
fim, o programa congela o pico da área alagada. Esse número não muda mais.

> **Não deixe mexerem na areia a partir daqui.** É o pedido central da atividade, e a
> projeção o repete: *MANTENHA O RELEVO*.

## Passo B — Área urbana

Toque em **Passo B · Área urbana**. O programa confere se o relevo continua o mesmo e se o
sensor não foi reiniciado. Se algo mudou, ele **recusa a comparação** em vez de fazê-la.

Toque em **Fazer chover**. Mesma chuva, mesma duração, mesmo relevo — só a cobertura mudou.

## O que observar

| Durante | Pergunte |
|---|---|
| A chuva de A | Para onde a água vai? Onde ela some? |
| A chuva de B | A água some no mesmo lugar? Ela chega mais rápido ao fundo? |
| A comparação | Qual cobertura alagou mais área? Quanto mais? |

## Discussão

A tela fecha com a pergunta que abre a conversa:

> **Que outros fatores também influenciam uma enchente numa cidade real?**

Bueiro entupido, córrego canalizado, ocupação da várzea, chuva que já vinha caindo há dias,
lixo, obra. A caixa isola **uma** variável; a cidade tem todas ao mesmo tempo.

Vale também a pergunta invertida: *e se a chuva fosse o dobro?* A mata tem um limite — e
descobrir que ele existe vale mais que decorar que "mata protege".

## O que permanece igual · o que muda

| Permanece | Muda |
|---|---|
| O relevo moldado pela turma | **A cobertura do solo** |
| A intensidade da chuva | |
| A duração da chuva | |
| O estado inicial do solo (seco nos dois) | |
| O passo de tempo da simulação | |

## O que a caixa mede

**O pico da área alagada, em porcentagem da área da caixa.** É a maior extensão que ficou
com água acumulada durante o episódio.

Foi escolhida por ser a única métrica que não depende da largura física do sensor, que
ainda não foi medida por instalação.

## O que a caixa **não** prova

- **Não prova que urbanização causa enchentes.** Mostra o que este modelo faz quando só a
  cobertura muda. A relação no mundo real é assunto da discussão, não conclusão do software.
- **Não é previsão.** Nenhum número aqui antecipa uma cheia.
- **Não representa uma cidade real.** O relevo é o que a turma moldou.
- **Os coeficientes são didáticos.** Foram escolhidos para o contraste aparecer numa aula,
  não medidos em campo. A ordem de grandeza segue a literatura; o valor não é medição.

## Perguntas prováveis da turma

| Pergunta | Resposta honesta |
|---|---|
| "Isso é uma cidade de verdade?" | Não. É o relevo que vocês fizeram. A cidade entra como *cobertura*, não como forma. |
| "Então a mata resolve a enchente?" | Neste modelo ela alagou menos. Numa cidade real há outros fatores — e mesmo a mata tem limite: aumente a chuva e veja. |
| "Por que não pode mexer na areia?" | Porque aí duas coisas mudariam ao mesmo tempo, e não daria para saber qual causou a diferença. |
| "Por que sempre 20 segundos?" | Para que as duas chuvas sejam a mesma chuva. |
| "Quantos litros?" | O programa mostra litros com `≈` porque a largura da caixa ainda não foi medida aqui. Por isso a comparação usa porcentagem de área. |

## Problemas e recuperação

| Aconteceu | O programa faz | Você faz |
|---|---|---|
| Alguém mexeu na areia entre A e B | Recusa comparar e explica | Recomece a atividade |
| O sensor caiu e reconectou | Invalida a atividade | Recomece — o passo A antigo não vale |
| Quer repetir o passo A | O botão fica desabilitado durante a execução | Encerre e comece de novo |
| Quer outra cobertura | Os controles ficam travados durante a atividade | Encerre; no modo livre eles voltam |
| A comparação cobre o mapa | — | **ESC** ou **M** na projeção volta ao mapa |

---

## Nota técnica

A atividade zera água e saturação antes de cada passo, e roda com passo de tempo fixo.
As duas coisas foram medidas antes de serem escolhidas: sem zerar, duas execuções idênticas
davam 48% e 53%; sem passo fixo, mudar o fps mudava o pico em até 12 pontos.

Com as duas congeladas, três execuções idênticas de Mata deram **52%, 52%, 52%** — com o
fps oscilando entre 15 e 17. É o que permite atribuir à cobertura a diferença que aparece
entre A e B.
