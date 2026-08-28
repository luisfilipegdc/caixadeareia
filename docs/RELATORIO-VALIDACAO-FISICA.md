# Relatório da sessão de validação física

**Branch:** `claude/autonomous-platform-foundation`
**Commit inicial:** `6f90d70` · **Commit final:** `642dca8`
**Data:** 28 de agosto de 2026

---

## O bloqueio que definiu a sessão

**O Kinect não estava conectado.** Verificado por três caminhos independentes:

- `Get-PnpDevice` lista os três nós do sensor (`PID_02BE`, `02BF`, `02AD`) todos com
  `Status: Unknown` — entradas fantasma de uma conexão anterior, não hardware presente.
- `NuiGetSensorCount`, pelo caminho real do código, devolveu **0**.
- A própria aplicação, ao abrir, exibiu *"Nenhum sensor Kinect detectado pelo driver da
  Microsoft"*.

Isso invalidou a premissa das Etapas 2 a 6, que começam com "com hardware real
conectado". Não simulei o que não podia medir. O que fiz foi separar o que ainda era
possível — validação visual com o simulador, correções puras, e transformar a medição
física num procedimento reproduzível — do que ficou realmente bloqueado.

---

## 1. Screenshots da interface validada

Quatro capturas entraram no repositório:

| Arquivo | O que mostra |
|---|---|
| [`17-queimada-em-andamento.png`](img/17-queimada-em-andamento.png) | A queimada rodando — frente de fogo sobre o relevo. **Primeira vez que este módulo foi visto funcionando** |
| [`18-painel-queimada.png`](img/18-painel-queimada.png) | Painel da queimada: combo, texto explicativo, força do vento, botão |
| [`19-bug-cobertura-dessincronizada.png`](img/19-bug-cobertura-dessincronizada.png) | O bug reproduzido: "Mata" na tela, e o programa dizendo que não há vegetação |
| [`20-litros-como-estimativa.png`](img/20-litros-como-estimativa.png) | Litros com "≈" e a explicação — a mudança de honestidade da sessão anterior, confirmada na tela |

### O que a validação visual confirmou funcionando

- Semáforo de estado (Parado → Nivele a areia → Pronto · tudo funcionando)
- Calibração: 100% de cobertura, salva e recarregada
- Painel da queimada completo, sem truncamento: texto explicativo, slider com valor
  (0,45), aviso do vento sorteado
- Os três textos de honestidade científica, empilhados e legíveis: descrição, resumo
  qualitativo, e *"Comparação didática entre coberturas, não medição de campo"*
- Chuva: "Chovendo… 7s / Área alagada: 48%", depois "A água escoou. Pico de alagamento:
  49% · Infiltrado: ≈ 45,8 L"
- Diagnóstico de near mode fica calado no simulador, como deve

### Problemas visuais encontrados e não corrigidos

**A barra lateral não cabe no monitor do professor.** A 1366×768 — a resolução da máquina
onde isto vai rodar — os controles de simulação ficam abaixo da dobra. O botão "Atear
fogo" e toda a seção de comparação exigem rolagem, e a barra de rolagem é estreita.
Mesmo a 1440×900 o botão fica na borda inferior.

Não mexi: redesenhar a barra lateral é decisão de produto, não correção pontual. Vai
como pendência.

---

## 2. Largura física medida

**Não medida.** Sem sensor e sem fita métrica, é impossível daqui.

O que entreguei no lugar: **[CALIBRACAO-FISICA.md](CALIBRACAO-FISICA.md)** — procedimento
completo, reproduzível em qualquer instalação, usando só o que já existe no software.

Dois marcadores a uma distância conhecida sobre a areia; a grade de alinhamento da
projeção (tecla `G`, 10 divisões) para ler que fração da largura do quadro eles ocupam;
e uma divisão:

```
largura coberta = distância entre marcadores ÷ fração da largura
```

O documento inclui como estimar a margem de erro (5% a 10% com marcadores bem afastados,
o que vira 10% a 20% no volume porque a área é o quadrado) e explica por que **não** se
deve derivar a largura do campo de visão teórico.

`Config.Caixa.LarguraCobertaPeloSensorMm` continua em **1250 mm** e `larguraMedida`
continua **false**. Não inventei valor.

---

## 3. ROI encontrada

**Não implementada.** A Etapa 3 pedia um fluxo de calibração da ROI.

Decidi não fazer: sem sensor e sem a caixa, eu construiria um fluxo de marcação de
limites que não poderia testar contra nada — nem para saber se as coordenadas resultantes
fazem sentido, nem para verificar o efeito sobre a água e a projeção. Um editor de ROI
não validado é pior que a ausência dele, porque parece resolvido.

Continua como pendência P4, agora com a observação de que o procedimento de calibração
física (item 2) usa a mesma grade e provavelmente deve ser o mesmo passo de interface.

---

## 4. Configuração final usada

Nenhuma alteração de configuração. `config.json` mantém os padrões:

```json
"caixa": { "larguraCobertaPeloSensorMm": 1250, "larguraMedida": false }
```

---

## 5. Testes realizados com Kinect

**Nenhum.** Sensor ausente.

Todos os testes de aplicação foram feitos com `SimulatedDepthSource`, dirigindo a
interface por UI Automation: ligar, calibrar, trocar cobertura, selecionar simulação,
atear fogo, fazer chover, ler os resultados.

---

## 6. Comportamento de timeout

Implementado, **não validado com hardware**.

`PoliticaDeTimeout` conta esperas consecutivas sem quadro e declara falha ao passar de
**3 segundos** (15 esperas de 200 ms). A justificativa do valor está no código: o sensor
entrega ~30 quadros por segundo, então mesmo um engasgo severo produz algo bem antes
disso; e 3 s é curto o bastante para o professor ver "Reconectando…" antes de concluir
que o programa travou.

A regra ficou numa classe pura com **8 testes** cobrindo operação normal, timeout
ocasional, silêncio prolongado, disparo único, derivação do limite a partir do tempo de
espera, e recomeço após reconexão. O laço de captura ganhou três linhas.

O que **não** foi testado: o comportamento real do sensor emudecendo, e se o caminho de
reconexão existente se comporta bem quando acionado por esta via. Precisa de hardware.

---

## 7. Diagnóstico de near mode

Implementado como **indício, não prova** — e o código diz isso explicitamente.

`DiagnosticoDeNearMode` observa os 15 primeiros quadros e guarda a menor profundidade
válida. Se o near mode foi pedido e nada ficou abaixo de 800 mm, o status ganha um aviso,
uma vez só, sem interromper nada.

O texto do aviso admite as duas explicações possíveis: *"Pode ser que ele não tenha sido
aplicado — ou que a areia esteja mesmo toda a mais de 80 cm do sensor."* Um aviso que
afirmasse defeito de hardware mandaria o professor procurar problema onde talvez não haja.

Fica calado quando: o near mode não foi pedido, a fonte não é o Kinect, ainda não observou
o bastante, ou houve leituras válidas de menos (sensor tampado, caixa vazia). **9 testes**,
a maioria cobrindo os casos de silêncio. Verificado na tela: o simulador não gera aviso.

Nenhum código de captura foi tocado — a observação acontece no engine, sobre o quadro que
já chegou.

---

## 8. Métricas de água revalidadas

Revalidação completa depende da medição (item 2). O que dava para fazer foi verificar a
**hipótese de célula quadrada**, que a Etapa 6 mandava não esconder.

**Ela é falsa, e eu quantifiquei o quanto.**

| | fração da distância |
|---|---|
| largura coberta (57°) | `2·tan(28,5°)` = 1,0859 |
| altura coberta (43°) | `2·tan(21,5°)` = 0,7878 |

Proporção do campo: **1,378**. Proporção da grade da simulação (320×240): **1,333**.
As células são **3,38% mais largas que altas**, e `areaCelula = tamanho × tamanho`
supera a área real nessa proporção.

**Não corrigi**, e a razão é de honestidade e não de preguiça: se a largura assumida
estiver errada em ~11% como a geometria sugere, o erro na área é de **~24%**. Corrigir
3,4% antes de resolver 24% seria falsa precisão. A correção proposta está escrita em P9,
para depois da medição.

O que continua confiável e foi confirmado por teste: **porcentagens não passam pela área
da célula** e valem independentemente da calibração.

---

## 9. Decisão sobre cenários pedagógicos

**Nenhuma.** A Etapa 7 pedia avaliar os limiares (30 mm, 45 mm) "sobre o relevo real da
caixa". Não há relevo real disponível.

Avaliar contra o simulador seria enganoso: a amplitude sintética foi escolhida pelo
próprio código do simulador, então qualquer conclusão seria circular.

Continua P8, com as quatro alternativas registradas (altura absoluta, porcentagem da
amplitude, percentis do relevo, pintura manual). A recomendação depende de ver a
distribuição de alturas que uma turma real produz.

---

## 10. Bugs encontrados

### Corrigidos

**A cobertura exibida não era a aplicada.** Cada `StartSource` cria uma `WaterSimulation`
nova, cujo construtor preenche o solo com areia; o combo continuava exibindo "Mata".
Sintoma reproduzido na tela: atear fogo respondia *"não há vegetação que possa queimar"*
com "Mata" escrito logo acima. A primeira chuva de uma aula também caía sobre areia
enquanto o professor lia Mata, e o histórico de comparação registrava como mata.
**Encontrado ao abrir o programa pela primeira vez.**

**Estados iniciais de controle não eram aplicados.** `OnStateChanged` só dispara quando o
estado *muda*; ao abrir, o estado já era `Parado` desde o construtor, então os botões
ficavam como o XAML os deixou — "Nivelar e calibrar" aparecia habilitado com a caixa
desligada.

**Célula molhada ficava incombustível para sempre** (P2). Corrigido com uma atribuição
removida.

**Sensor que emudece não disparava reconexão** (P6).

### Encontrados e não corrigidos

**Duas pastas de saída divergentes** (P10). `dotnet build` da solução escreve em
`bin/x64/Release/`; do `.csproj`, em `bin/Release/`. Custou tempo real: validei
visualmente um binário de **dois commits atrás** sem perceber, e só o tamanho do arquivo
(138.752 contra 148.480 bytes) denunciou. Documentado; unificar mexe no empacotamento da
release.

**Barra lateral não cabe em 1366×768.** Ver item 1.

---

## 11. Commits produzidos

```
642dca8 docs: document physical width calibration procedure
c3eb810 feat: warn when near mode may not have been applied
b094e94 fix: recover when the Kinect stops delivering frames
200f0ca fix: allow wet fire cells to burn after drying
eec977e fix: apply the selected soil cover when a source starts
```

---

## 12. Build final

```
dotnet build CaixaInterativa.sln -c Release   →  êxito · 0 avisos · 0 erros
dotnet build CaixaInterativa.sln -c Debug     →  êxito · 0 avisos · 0 erros
```

## 13. Testes finais

```
dotnet test CaixaInterativa.sln -c Release    →  105 aprovados · 0 falhas
```

Eram 83 no início da sessão. Os 22 novos cobrem a política de timeout (8), o diagnóstico
de near mode (9), a causa raiz da dessincronização de cobertura (2) e o bug do fogo
molhado (3).

A regressão visual byte a byte continua intacta desde `4d68a8e`.

---

## 14. Pendências restantes

| # | Pendência | Estado |
|---|---|---|
| **P1** | Medir a largura coberta pelo sensor | Procedimento escrito; **falta medir** |
| P2 | Fogo molhado | ✅ corrigido |
| P3 | `EscoadoLitros` nunca calculado | Aberto — a Etapa 9 condicionava à calibração, que não aconteceu |
| **P4** | ROI sem interface | Aberto — ver item 3 |
| P5 | Erosão calculada e descartada | Aberto |
| P6 | Sensor que emudece | ✅ corrigido, **falta validar com hardware** |
| P7 | Near mode não verificado | ✅ diagnóstico implementado, **falta validar com hardware** |
| P8 | Cenários pedagógicos sem UI | Aberto — ver item 9 |
| **P9** | Células não são quadradas (3,38%) | Documentado e quantificado; correção proposta |
| **P10** | Duas pastas de saída | Documentado |
| — | Barra lateral não cabe em 1366×768 | Novo, não registrado como P ainda |

---

## 15. Commit final

`642dca8`

## 16. `git log --oneline` da sessão

Ver item 11.

## 17. `git diff --stat`

```
 17 files changed, 951 insertions(+), 40 deletions(-)
```

Dos quais 366 linhas são de teste e 192 são o procedimento de calibração.

---

## O que eu faria na próxima sessão, em ordem

1. **Conectar o Kinect.** Sem isso, metade desta lista continua bloqueada.
2. **Medir a largura** seguindo o procedimento, e preencher o `config.json`.
3. **Validar o timeout e o near mode com hardware** — os dois estão implementados e
   testados em lógica, mas nunca viram um sensor.
4. **Decidir sobre a barra lateral**, que é o que o professor vai encontrar primeiro.
5. Só então ROI, cenários e erosão.
