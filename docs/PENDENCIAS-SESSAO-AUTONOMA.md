# Pendências da sessão autônoma

Coisas que encontrei e **não** resolvi, porque exigem decisão sua, medição física ou
mudariam resultados científicos. Cada uma traz evidência, opções e recomendação.

Ordenadas por urgência.

---

## P1 — Medir a largura que o sensor cobre sobre a areia

**Risco:** alto · **Tipo:** medição física · **Bloqueia:** todo valor absoluto em litros

### Problema

`WaterSimulation` deriva o tamanho da célula de uma largura em milímetros que, até esta
sessão, era um literal no construtor (`larguraCaixaMm = 1250f`) e nunca era passado por
ninguém. Desse tamanho saem `VolumeLitros` e `InfiltradoLitros`, exibidos ao professor e
projetados para a turma.

### Evidência

O teste `VolumeEmLitrosEscalaComALarguraConfigurada` demonstra a dependência: dobrar a
largura **quadruplica** o volume, porque a área da célula é o lado ao quadrado.

Pelo `docs/MONTAGEM-FISICA.md`, o eixo horizontal do sensor (57°) cobre
`1,0859 × distância`. A 1,28 m isso dá cerca de **139 cm**, não os 125 cm assumidos —
um erro de ~11% no lado, ~24% na área.

### O que já foi feito

- A largura virou `Config.Caixa.LarguraCobertaPeloSensorMm`, com **o mesmo padrão de
  1250 mm** — nada mudou de comportamento.
- Enquanto `Config.Caixa.LarguraMedida` for falso, a interface marca os litros com "≈"
  e explica no painel do professor.

### O que falta — e é você quem precisa fazer

Medir. Uma forma: colocar marcadores nas bordas da caixa, ver onde aparecem no mapa
projetado, e calcular quantos milímetros o quadro de 640 px cobre. Depois preencher
`larguraCobertaPeloSensorMm` no `config.json` e marcar `larguraMedida: true`.

**Não inventei um valor.** Trocar 1250 por 1390 "porque a conta dá" seria substituir uma
suposição por outra, e o roadmap do projeto proíbe exatamente isso.

---

## P2 — ~~A imunidade à água, no fogo, nunca é revogada~~ ✅ CORRIGIDO em 28/08/2026

`TentarAcender` gravava `Estado.NaoQueima` ao encontrar água, e esse estado é terminal: a
célula ficava incombustível pelo resto do incêndio mesmo depois de a água sumir. A recusa
agora vale só para a tentativa em curso — é a água do instante que decide.

**O que custou para provar.** Duas versões do teste passaram *sem* a correção, por motivos
que valem ficar registrados:

1. **Faixa larga de água não expõe o defeito.** A frente de chama só testa os vizinhos
   imediatos, então boa parte da barreira nunca chega a ser marcada e o fogo contorna por
   ali depois que a água seca.
2. **Comparar dois incêndios também não expõe.** `Atear` chama `Preparar`, que reconstrói
   o estado a partir do solo e apaga qualquer `NaoQueima` anterior.

O cenário que isola: corredor de **uma célula** de largura, e secar a água **dentro do
mesmo incêndio**, enquanto o vizinho da porta ainda queima. Aí o defeito aparece limpo —
antes da correção o fogo parava em 50,6% com ou sem secagem.

**Correção da estimativa de impacto.** A pendência original sugeria que uma barreira
inteira ficava permanentemente inutilizada. Não é o caso: o trinco cria buracos numa
barreira larga, não um bloqueio total. O impacto real era menor do que eu havia descrito.

## P3 — `EscoadoLitros` nunca é calculado

**Risco:** baixo · **Tipo:** métrica morta

`WaterSimulation.EscoadoLitros` é declarado e zerado em `Limpar()`, mas **nunca
incrementado**. `MoverAgua` remove água pelas bordas quando `BordasEscoam` é verdadeiro e
não contabiliza nada.

Hoje não aparece na interface, então não mente para ninguém. Mas se alguém exibir sem
olhar a implementação, vai mostrar "0 L escoados" no meio de uma enchente.

**Opções:** implementar a contagem em `MoverAgua`, ou remover a propriedade.
**Recomendação:** implementar — é informação pedagógica real ("para onde foi a água que
não infiltrou nem ficou") e o cálculo é somar o que sai pelas bordas. Não fiz porque toca
o solver de água, que é código de física validado.

---

## P4 — A ROI não tem interface

**Risco:** médio · **Tipo:** decisão de produto + UI

`ProjectionSettings.RoiLeft/Top/Right/Bottom` existe, é lida pelo renderizador, e o padrão
é o quadro inteiro do sensor (640×480). **Não há nenhum controle na interface** — só
editando `config.json` à mão.

Consequência em campo: o mapa projetado cobre tudo o que o sensor enxerga, incluindo chão,
bordas da caixa e quem estiver por perto. E `BordasEscoam` da água passa a valer na borda
do *campo de visão*, não na borda física da caixa.

**Recomendação:** definir a ROI durante a calibração — o professor marca os quatro cantos
da caixa uma vez. Não implementei porque envolve fluxo de UI novo e decisão de como
apresentar isso sem complicar o passo de calibração, que hoje é um botão só.

---

## P5 — Erosão é calculada todo quadro e descartada

**Risco:** baixo · **Tipo:** decisão de produto

`WaterSimulation.AcumularErosao` roda `Parallel.For` sobre 76.800 células a cada quadro,
preenche `_erosao[]` e acumula `ErosaoTotal`. Nem o campo nem o total chegam ao
renderizador ou à interface.

Com a arquitetura de camadas pronta, **exibir a erosão agora é barato**: bastaria a
`WaterSimulation` declarar uma segunda `CamadaVisual` com um `ModoDeCor` novo.

**Por que não fiz:** exige escolher cores e limiares para um fenômeno que nunca foi visto
na tela — é decisão visual e pedagógica, não técnica. E a erosão prevista é um **modelo
didático** que precisaria de rótulo próprio.

---

## P6 — O sensor que emudece não dispara reconexão

**Risco:** médio · **Tipo:** hardware — **não toquei por regra da sessão**

`KinectV1Source.Loop` trata `WAIT_TIMEOUT` com `continue`, indefinidamente. Desconexão
dura levanta exceção e aciona a reconexão automática; mas um sensor que simplesmente para
de entregar quadros produz **tela congelada, sem mensagem e sem reconexão**.

É o modo de falha mais provável numa sala de aula.

**Correção provável:** contar timeouts consecutivos e disparar `Faulted` acima de ~15
(≈3 s).

**Por que não fiz:** é código de captura do Kinect, depurado com hardware real, e a regra
desta sessão é explícita — documentar em vez de alterar. A correção é pequena, mas precisa
ser validada com o sensor na mão, o que eu não tenho.

---

## P7 — Falta verificar empiricamente o near mode

**Risco:** baixo · **Tipo:** hardware — não toquei

O próprio código documenta que `NuiImageStreamSetImageFrameFlags` retorna `S_OK` mesmo
quando o near mode não é aplicado, e que a única verificação confiável é empírica: com
near mode ativo aparecem leituras abaixo de 800 mm.

**Não existe nenhuma verificação dessas no código.** Seria útil o app registrar, ao ligar,
a menor profundidade válida lida — e avisar se ficar presa em ~800 mm.

**Por que não fiz:** mesma regra. Registro aqui como sugestão para quando houver hardware
disponível para validar.

---

## P8 — Cenários pedagógicos continuam sem caminho de UI

**Risco:** baixo · **Tipo:** decisão de produto

`Cenario.Todos` traz seis cenários completos — Enchente no RS, a mesma com várzea
preservada, cidade drenada, depois da queimada, bacia preservada — cada um com contexto,
pergunta investigativa, composição de solo por altitude, chuva e saturação inicial.
**Zero referências fora do próprio arquivo.**

Conectá-los é mais valioso que qualquer fenômeno novo. Mas cada cenário aplica
`PintarPorAltitude` com limiares fixos (45 mm, 30 mm) que foram calibrados em **terreno
sintético** — sobre areia real, com outra amplitude, "cidade abaixo de 30 mm" pode cair no
lugar errado.

**Recomendação:** conectar depois que a Fase 2 do roadmap tiver medido a amplitude real do
relevo, ou tornar os limiares relativos (percentis do relevo atual) em vez de absolutos.
A segunda opção é uma mudança de modelo e precisa da sua decisão.
