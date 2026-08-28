# Briefing técnico — Caixa de Areia Interativa

> Documento de contexto para quem (pessoa ou agente) vai ajudar a evoluir o projeto.
> Escrito em agosto de 2026, sobre a versão **1.3**. Traz o estado real do código,
> não a intenção do roadmap.

---

## 1. O que é

Software nativo Windows que lê o relevo de uma caixa de areia física com um sensor Kinect
e projeta de volta sobre a areia um mapa topográfico colorido, atualizado em tempo real
conforme os alunos moldam o terreno com as mãos. Sobre esse mapa rodam **simulações
ambientais** — chuva e enchente, tipos de solo, queimada — usadas como material de aula.

- **Uso:** ensino de geografia e ciências da Terra, ensino fundamental e médio
- **Licença:** GPL-2.0-or-later
- **Repositório:** `github.com/luisfilipegdc/caixadeareia`
- **Local:** Brasília, DF

### Princípio pedagógico que orienta as decisões

O relevo vem das mãos dos estudantes; o software define **o que cobre** esse relevo e
**que evento** acontece sobre ele. A pergunta da aula nunca é "como era o vale do
Taquari", é "o que aconteceria com o vale que **vocês** construíram". Toda simulação
existe para responder a uma pergunta investigativa, não para ser demonstração.

Há um compromisso explícito de **honestidade científica** registrado no roadmap: cada
modo deve declarar na interface o que é medição do relevo real e o que é modelo didático.
Um estudante que sai da aula achando que a caixa "calculou" o El Niño aprendeu algo errado.

---

## 2. Stack e ambiente

| Item | Valor |
|---|---|
| Plataforma | .NET 8 + WPF, `net8.0-windows`, x64 |
| Sensor | Kinect v1 / Kinect for Windows modelo **1517** (`VID_045E`, `PID_02BE/02BF`) |
| API do sensor | NUI nativa do **Kinect SDK 1.8**, via P/Invoke em `Kinect10.dll` — **não** o wrapper gerenciado |
| Resolução | 640×480 de profundidade, ~30 fps |
| Renderização | **CPU**, `Parallel.For`, ~307k pixels/quadro |
| SO de desenvolvimento | Windows 10 Home 19045, .NET SDK 8.0.424 |
| Distribuição | executável único de 68 MB, self-contained, sem instalador |

O Kinect 1517 importa porque é o único modelo que suporta **near mode** (0,4–3,0 m em vez
de 0,8–4,0 m). Com o sensor a ~1 m da areia, isso é a diferença entre leitura limpa e
bordas cortadas.

---

## 3. Arquitetura

```
IDepthSource → DepthProcessor → [ ISimulationModule ] → TopographicRenderer → ProjectionWindow
   sensor       campo de alturas    estado do fenômeno      camadas visuais        projetor
                   (mm, float[])
```

Tudo é orquestrado por `SandboxEngine`, que roda um `DispatcherTimer` a ~60 Hz (o dobro da
taxa do sensor, para tirar até 16 ms de latência percebida) e guarda **apenas o quadro mais
recente** — se a renderização atrasa, descarta quadros velhos em vez de acumular latência.
Numa caixa de areia, atraso é pior que perda.

### Mapa de arquivos

```
src/CaixaInterativa/
├── Depth/
│   ├── IDepthSource.cs           43   contrato da fonte de profundidade
│   ├── NuiNative.cs             206   P/Invoke para Kinect10.dll
│   ├── KinectV1Source.cs        237   captura real
│   └── SimulatedDepthSource.cs  137   relevo sintético, sem hardware
├── Processing/
│   └── DepthProcessor.cs        309   calibração, buracos, suavização
├── Simulation/
│   ├── ISimulationModule.cs      52   contrato de módulo
│   ├── SoilMap.cs               266   12 coberturas + propriedades hidrológicas
│   ├── WaterSimulation.cs       550   tubos virtuais, infiltração, erosão
│   ├── FireSimulation.cs        350   autômato celular de propagação de fogo
│   ├── EarthquakeSimulation.cs  328   ondas sísmicas  ← a remover
│   └── Cenarios.cs              122   6 cenários pedagógicos prontos
├── Rendering/
│   └── TopographicRenderer.cs   309   rampa hipsométrica, curvas, sombreamento, overlays
├── Config/
│   ├── AppConfig.cs             155   persistência em config.json
│   └── CalibrationStore.cs      171   plano-base em binário
├── Views/
│   ├── MainWindow.xaml(.cs)   346+711 painel de controle do professor
│   └── ProjectionWindow.xaml   84+290 tela cheia no projetor
├── SandboxEngine.cs             389   orquestração
├── EngineState.cs                42   estados para o semáforo da UI
└── AppInfo.cs                    71   versão e metadados
```

### Convenções do código

- **Nomes de domínio em português** (`Atualizar`, `Profundidade`, `TipoDeSolo`,
  `AreaAlagadaPercent`); nomes de infraestrutura em inglês (`Render`, `Width`, `Buffer`).
  Isto é deliberado e deve ser mantido.
- **Comentários explicam o *porquê*, com o número medido junto.** O padrão da base é
  registrar a alternativa descartada e o motivo. Exemplo real, em `SoilMap.cs`:
  *"Medido: 10,87 ms por quadro com o switch, 10,52 ms com a tabela — cerca de 3%. Fica
  pela previsibilidade, não pelo ganho."* Código novo deve seguir esse padrão.
- **Cabeçalho GPL de 12 linhas em todo arquivo `.cs`.**
- Mensagens de commit em português, no imperativo, descrevendo a intenção.

---

## 4. Como cada peça funciona

### DepthProcessor — onde mora a diferença entre projeção utilizável e uma que "ferve"

O Kinect v1 tem 2–4 mm de ruído a essa distância e produz pixels inválidos nas bordas.
Três etapas, **nesta ordem**:

1. **Buracos** — pixel inválido mantém o último valor bom. Zerar criaria crateras piscando.
2. **Tempo** — filtro exponencial com **α adaptativo**: areia parada usa α=0,15 (estável);
   salto acima de 25 mm (uma mão entrando) usa α=0,65 (responsivo). Um α único obrigaria a
   escolher entre tremor e arrasto.
3. **Espaço** — box blur separável, custo O(1) por pixel independente do raio.

O **plano-base é armazenado por pixel**, não como um número único — assim uma caixa
levemente torta não vira um gradiente falso atravessando o mapa inteiro.

### WaterSimulation — tubos virtuais (Mei, Decaudin e Hu)

Cada célula guarda uma coluna de água e quatro tubos para os vizinhos; a diferença de
**nível (terreno + água)** acelera o fluxo. É por somar os dois que a água contorna um
morro em vez de atravessá-lo.

- Roda em **metade da resolução do sensor** (320×240). Pela condição CFL, 640×480 exigiria
  14 substeps/quadro (~86 M operações); 320×240 exige 7 (~11 M), que cabe nos 33 ms.
- Substeps adaptativos por CFL, limitados a 12. `dt` é limitado a 100 ms.
- **Infiltração e saturação por célula:** solo encharcado para de absorver — é por isso que
  a segunda chuva alaga mais que a primeira. Drenagem profunda lenta de propósito.
- `LimiarAlagamentoMm = 8` — 1 mm seria chão molhado e a métrica marcaria 96% em tudo.
- `BordasEscoam = true` — água que chega à borda sai, como num terreno aberto.
- `PicoAlagamentoPercent` guarda o máximo do episódio, porque o instante final já está
  em escoamento e perderia a resposta da aula.

### SoilMap — 12 coberturas

`Mata, Várzea, Pastagem, Agricultura, SoloArenoso, SoloArgiloso, UrbanoDrenado,
SoloCompactado, Rocha, Desmatado, Queimado, Impermeavel`

Cada uma com infiltração (mm/s), rugosidade, resistência à erosão, armazenamento (mm) e
cor RGB. **Os números são didáticos, não medições de campo** — escolhidos para que a
diferença entre bacia preservada e desmatada apareça numa aula de meia hora. Consulta por
tabela pré-calculada indexada pelo enum.

O enum é serializado por valor, então **tipos novos entram no fim**.

### FireSimulation — autômato celular

Propagação governada por quatro fatores: **combustível** (por tipo de solo), **vento**
(direção sorteada a cada incêndio + força), **água** (rio e alagado barram) e **encosta**
(fogo sobe morro mais rápido, porque a chama pré-aquece o combustível acima).

Passo fixo de 1/20 s, no máximo 4 por quadro — passo variável faria o fogo andar mais
rápido num computador mais rápido. Ao terminar, `AplicarCicatriz()` grava `Queimado` no
`SoilMap`, e a chuva seguinte encontra um território diferente. **Esse acoplamento é o
ponto do módulo:** o incêndio não termina quando apaga.

### TopographicRenderer — composição em camadas

Rampa hipsométrica de **12 paradas** (`Palette`, um `static readonly Stop[]` privado),
depois sombreamento (luz do noroeste, convenção cartográfica), curvas de nível com curva
mestra a cada 5, e por cima os overlays na ordem: **água → sismo → fogo**. Cada overlay
entra por *alpha blend*, não substituindo a cor, para o aluno continuar vendo o relevo por
baixo. Os overlays vêm em meia resolução e são amostrados **bilinearmente** — com nearest,
a borda de uma poça vira escada visível de blocos 2×2 na projeção.

### Cenarios.cs — 6 cenários pedagógicos

`Livre`, `Enchente no RS`, `A mesma enchente com a várzea preservada`, `Cidade que planejou
a drenagem`, `Depois da queimada`, `Bacia preservada`. Cada um traz contexto real, pergunta
investigativa e um `Action<SoilMap, float[], int, int>` que pinta a cobertura, mais
intensidade/duração de chuva e saturação inicial.

---

## 5. As armadilhas do interop NUI (já resolvidas — não regredir)

Estão documentadas no código e custaram depuração pesada:

1. **`NuiImageStreamGetNextFrame` devolve um ponteiro, não a struct.** A API flat usa
   `CONST NUI_IMAGE_FRAME **ppcImageFrame`. Declarar `out NuiImageFrame` faz o runtime
   escrever só 8 bytes; o resto vira lixo e o processo morre com **0xC0000374 (heap
   corruption)**. O sintoma não aponta para a causa.
2. **A profundidade vem deslocada 3 bits**, mesmo em `NUI_IMAGE_TYPE_DEPTH`. Sem o `>> 3`
   tudo sai 8× maior. O sinal: todos os valores são múltiplos de 8 e o máximo é exatamente
   `0x1FFF << 3 = 65528`.
3. **`ENABLE_NEAR_MODE` é `0x00020000`**, não `0x00040000` (esse é `TOO_FAR_IS_NONZERO`).
   Trocar não gera erro — `SetImageFrameFlags` retorna `S_OK` de qualquer forma. Medido:
   com a flag errada, 6,9% de cobertura e mínimo 801 mm; com a correta, **66,4% e 455 mm**.
   O retorno da API não prova nada; a verificação é empírica.

Técnica que funcionou para diagnosticar: preencher o buffer com `0xCD` antes da chamada
nativa e conferir quantos bytes foram escritos; validar a vtable com `BufferLen()` (614400)
e `Pitch()` (1280), que retornam inteiros conhecidos sem escrever memória.

---

## 6. Estado real, por fase

| Fase | Situação |
|---|---|
| 1. Estabilização | 🟡 Marco atingido: abre, liga a fonte salva, carrega calibração e mostra o relevo sozinho. Faltam **log em arquivo**, modo de diagnóstico, teste de longa duração, backup |
| 2. Montagem física | ✅ **Concluída** (estrutura, areia real, projeção alinhada, simulações testadas) — **mas a documentação ainda não foi atualizada e as medidas finais não estão registradas** |
| 3. Topografia 2.0 | ⬜ Sem homografia, sem legenda de altitude, sem captura de imagem |
| 4. Água e enchentes | ✅ Completa, com saturação do solo |
| 5. Solo e erosão | ✅ Completa, 12 coberturas |
| 6. Clima e temperatura | ⬜ Não iniciada |
| 7. Fenômenos geológicos | 🟡 Terremoto pronto — **a ser removido** |
| 8. Camada pedagógica | 🟡 6 cenários e 2 roteiros no manual; falta alinhamento com a BNCC |
| 9. Plataforma aberta | 🟡 Docs, manual e releases públicos; falta instalador |

### Limitações conhecidas

- **Alinhamento apenas afim** (escala, deslocamento, rotação, espelhamento). Projetor
  oblíquo deixa distorção de perspectiva que só uma homografia de 4 cantos corrige.
- **Sem correção da distorção da lente** do Kinect — erro de alguns mm nas bordas.
- **Renderização em CPU.** Adequada para colorização; um solver iterativo mais pesado
  pediria GPU.
- **`MaxValidDepthMm = 2000`** — sensor montado mais alto exige ajustar `config.json`.

---

## 7. Achados desta revisão de código

### 🔴 A queimada existe, está completa e **não tem como ser ligada**

`FireSimulation` está instanciada em `SandboxEngine` (linha 160), é atualizada no tick
(318) e é desenhada pelo renderer (335) e pelo painel da projeção. Mas o método
`Atear()` — o único jeito de iniciar um incêndio — **não é chamado em lugar nenhum do
projeto**. O `ComboBox` de simulações em `MainWindow.xaml.cs:204` só oferece "Chuva" e
"Terremoto".

Ou seja: 350 linhas de simulação funcional, inalcançáveis pelo usuário.

### 🟠 A barreira de água do fogo é um trinco permanente

Em `FireSimulation.TentarAcender`, quando há água a célula é marcada
`Estado.NaoQueima` — **estado terminal**. Uma célula molhada continua imune depois que a
água seca, e — mais importante para o que se quer construir — a decisão é tomada uma única
vez, na primeira tentativa de acender. Para o fogo **reagir a um canal cavado durante o
incêndio**, esse teste precisa ser reavaliado a cada passo, não gravado no estado.

### 🟡 Não existe conceito de lençol freático

Cavar altera o terreno, e a água que já existe escorre para o buraco — isso funciona hoje.
Mas **cavar não produz água**. Sem chuva ativa, escavar não encontra nada. É exatamente a
peça que falta para as duas interações desejadas.

### 🟡 A rampa de cor é uma constante privada

`TopographicRenderer.Palette` é `static readonly` e hardcoded. Temas por bioma exigem
torná-la selecionável — a mudança é pequena, mas toca a assinatura de `Sample()`.

---

## 8. O que se quer construir agora

1. **Temas por bioma** — as cores do mapa mudam conforme o bioma escolhido (Cerrado é
   diferente de Mata Atlântica). O usuário escolhe o tema.
2. **Remover o terremoto**, mantendo a chuva.
3. **Queimada interativa** — foco de incêndio começa na parte de mata e se alastra; se o
   aluno **cavar e encontrar água**, ele muda o trajeto do fogo.
4. **Degelo das calotas polares** — dois grupos de alunos constroem o polo Norte e o polo
   Sul, com uma ilha no meio; ao acionar o degelo, as calotas derretem e o nível da água
   sobe visivelmente.
5. **Interatividade como princípio geral** — toda simulação precisa ter um jeito de o
   aluno intervir e ver a consequência, inclusive a chuva.

---

## 9. Plano de ação

### A ideia que sustenta tudo: o lençol freático

As três coisas pedidas (fogo interativo, calotas, intervenção na chuva) parecem três
problemas diferentes e são **um só**: hoje a água só existe se estiver chovendo.

Introduzir um **nível de lençol freático** — uma altitude, em mm relativos ao plano-base,
abaixo da qual toda escavação enche de água — resolve as três de uma vez, com um trecho
curto no `Atualizar` da `WaterSimulation`:

```
para cada célula:
    if (terreno[i] < nivelLencolMm)
        agua[i] = max(agua[i], nivelLencolMm - terreno[i])
```

Com isso, cavar **produz** água. E a partir daí:

- O aluno cava um canal na frente do fogo → o canal enche → o fogo para. **Exatamente a
  interação pedida**, sem precisar de detecção de mãos.
- Subir o `nivelLencolMm` ao longo do tempo **é** o degelo: o mar sobe e engole a costa.
- Na chuva, o aluno passa a poder cavar um lago de retenção e ver a diferença.

**Duas armadilhas a tratar junto**, senão o lago vaza:
- Células abaixo do lençol **não podem infiltrar** — já estão saturadas. Sem isso o lago
  drena sozinho.
- `BordasEscoam` precisa desligar quando houver lençol/mar, senão o oceano escoa pela borda
  da caixa.

---

### Etapa 1 — Temas por bioma *(3 a 5 dias)*

Foi o primeiro pedido e é independente do resto — pode começar já.

**O que fazer**

1. Criar `Rendering/TemaVisual.cs`: um `record` com nome, rampa hipsométrica (`Stop[]`),
   cor de água, cor de curva de nível e **cobertura padrão** do bioma.
2. Tirar `Palette` de dentro do renderer; `Render()` passa a receber o tema.
3. Persistir a escolha em `RenderSettings.Tema` e expor um `ComboBox` no painel.

**Temas iniciais, e por que as cores mudam**

| Tema | Leitura |
|---|---|
| **Atlas** (atual) | Convenção escolar, azul→verde→marrom→branco. Continua sendo o padrão |
| **Cerrado** | Verdes acinzentados e ocres; solo vermelho exposto nas encostas; sem neve no topo |
| **Mata Atlântica** | Verdes saturados e escuros, densos até altitude alta; topos em rocha, não em neve |
| **Amazônia** | Verde uniforme com pouca variação vertical — o relevo é baixo, e a leitura vem da água |
| **Caatinga** | Ocres e cinzas; a faixa "verde" quase não existe |
| **Polar** | Brancos e azuis-gelo; a faixa de água domina — é o tema do modo de degelo |

> **Recomendação de projeto:** o tema não deve mudar só a cor. Ele deve trocar também a
> **cobertura padrão** do `SoilMap` (Cerrado → `Pastagem`, Mata Atlântica → `Mata`,
> Caatinga → `Desmatado`) — assim o mesmo relevo, com a mesma chuva, dá resultados
> diferentes por bioma, e o tema vira conteúdo em vez de enfeite. Isso pode exigir 2 ou 3
> tipos de solo novos (`Cerrado`, `Caatinga`, `Gelo`) — entram **no fim do enum**.

**Entregável:** o professor escolhe "Cerrado" e o mapa inteiro muda de identidade visual.

---

### Etapa 2 — Remover o terremoto *(meio dia)*

Escopo fechado, já levantado. Sete pontos:

| Arquivo | O que sai |
|---|---|
| `Simulation/EarthquakeSimulation.cs` | arquivo inteiro (328 linhas) |
| `SandboxEngine.cs` | linhas 57, 157, 315–316, 322, 331–334 |
| `Rendering/TopographicRenderer.cs` | bloco do overlay sísmico (≈194–215) e 4 parâmetros |
| `Views/MainWindow.xaml.cs` | enum `Simulacao`, item do combo, `ExecutarTerremoto`, painel de resultado |
| `Views/MainWindow.xaml` | painel `CfgTremor` e slider de magnitude |
| `Views/ProjectionWindow.xaml.cs` | linhas 203, 224–231 |
| `docs/MANUAL.md`, `docs/PROJETO.md`, `ROADMAP.md` | seções e menções |

O código não se perde — fica no histórico do git, recuperável se um dia voltar como
módulo de vulcão. Fazer isto **antes** da Etapa 3 deixa o renderer mais simples de mexer.

---

### Etapa 3 — Lençol freático e água permanente *(3 a 4 dias)*

O coração do plano.

1. `WaterSimulation.NivelLencolMm` (padrão: desligado, `float.NegativeInfinity`).
2. Aplicar o preenchimento no início de `Atualizar`, antes dos fluxos.
3. Bloquear infiltração em células abaixo do lençol.
4. `BordasEscoam = false` automaticamente quando o lençol estiver ativo.
5. `Ativo = true` mesmo sem chuva — hoje a água só atualiza durante um episódio.
6. Controle no painel: **"Nível da água subterrânea"**, com um preset "seco" e um "várzea".

**Como validar:** com o lençol em −20 mm, cavar até −30 mm deve formar um lago estável
de 10 mm que não drena nem transborda. Conferir que o volume total para de crescer.

---

### Etapa 4 — Queimada interativa *(4 a 6 dias)*

Aqui o módulo que já existe finalmente vira aula.

1. **Ligar na interface.** Item "Queimada" no combo, painel com vento e um botão
   **"🔥 Atear fogo"**.
2. **Foco escolhido por clique.** `Atear(u, v)` já aceita coordenadas — basta converter o
   clique no `Preview` do painel para coordenadas normalizadas. Sem clique, sorteia entre
   as células com combustível (a lógica já está pronta e evita o incêndio começar no
   asfalto e não pegar).
3. **Corrigir o trinco da água** — reavaliar `Agua[i] > limiar` a cada passo em vez de
   gravar `NaoQueima`. **É esta linha que torna o canal cavado eficaz.**
4. **Aviso na tela quando o fogo é barrado por água** — sem isso o aluno cava, funciona, e
   não percebe que foi ele quem causou. A consequência precisa ser legível.
5. **Controle do vento pelo professor** (hoje é sorteado): mudar a direção no meio do
   incêndio é a segunda interação, e é a que ensina por que brigadista teme mudança de vento.

**A aula que isso destrava:** *"O fogo começou na mata e está indo para a cidade. Vocês têm
dois minutos para cavar um aceiro que o segure — sem cortar o rio que abastece a cidade."*

---

### Etapa 5 — Degelo das calotas polares *(1 a 2 semanas)*

O mais ambicioso, e o que exige mais cuidado de honestidade científica.

**O problema:** a areia não derrete. As calotas são **pintadas** sobre o relevo que os
alunos construíram, não simuladas fisicamente.

**Desenho proposto**

1. Tipo de solo novo `Gelo`, com aparência branco-azulada e combustível zero.
2. **Modo "Calotas"**: o mapa é dividido em três zonas — norte, sul e a faixa central.
   Cada grupo constrói a sua; o `SoilMap` pinta de `Gelo` tudo acima de uma altitude nas
   duas zonas polares. A ilha central fica sem gelo.
3. **Botão "Iniciar degelo"** dispara duas coisas ao mesmo tempo, ao longo de ~90 s:
   - a **linha de neve sobe** — o `Gelo` recua das bordas para os topos;
   - o **`NivelLencolMm` sobe** — o mar avança sobre a costa da ilha.
4. **Leitura na projeção:** % da ilha submersa, quanto de gelo restou, quantos "mm de mar"
   subiram. É o número que responde à pergunta da aula.

**Declaração obrigatória na tela:** *"O relevo é medido pelo sensor. O degelo é um modelo
didático — a proporção entre gelo derretido e nível do mar é ilustrativa, não uma previsão
climática."* O roadmap exige isso, e aqui é onde mais importa.

**Interatividade:** enquanto o mar sobe, os alunos podem **construir diques** na ilha com
as próprias mãos e ver o que ainda alaga. O lençol freático faz isso funcionar de graça —
a água encontra qualquer barreira nova no mesmo quadro.

---

### Etapa 6 — Interatividade na chuva *(2 a 3 dias)*

Fecha o pedido de "toda simulação precisa ter interação".

- **Chuva localizada:** clicar no preview faz chover só naquela região, em vez da caixa
  inteira. Permite a pergunta "e se chovesse só na cabeceira da bacia?".
- **Leitura ao vivo do que o aluno mudou:** área alagada agora × pico do episódio, com o
  aviso *"a barragem que vocês fizeram segurou X% da água"*.
- **Chuva contínua sob demanda** além do episódio cronometrado, para experimentar sem
  reiniciar.

---

## 10. Ordem recomendada e por quê

```
1. Temas por bioma        ← independente, pedido primeiro, vitória visível rápida
2. Remover terremoto      ← limpa o renderer antes de mexer nele
3. Lençol freático        ← a peça que destrava 4, 5 e 6
4. Queimada interativa    ← módulo pronto esperando 5 linhas de UI
5. Degelo das calotas     ← o mais caro, e o que mais depende do 3
6. Interatividade na chuva
```

**Fora da fila, mas cobrando juros:** o **log em arquivo** da Fase 1. A montagem física
está pronta e as aulas vão começar; sem log, um problema em sala vira relato sem
evidência. São poucas horas de trabalho e é o que torna todo o resto diagnosticável.

---

## 11. Riscos técnicos a vigiar

| Risco | Onde | Mitigação |
|---|---|---|
| Lago drenando sozinho | Infiltração não bloqueada abaixo do lençol | Testar volume constante por 60 s |
| Oceano escoando pela borda | `BordasEscoam = true` | Desligar automaticamente com lençol ativo |
| Custo do renderer | 4 amostras bilineares por overlay por pixel; temas e gelo somam mais | Medir ms/quadro antes e depois; orçamento é 33 ms |
| Acoplamento de grades | `AplicarCicatriz` assume grade do fogo == grade do solo (ambas sensor/2) | Manter a invariante ou tornar explícita |
| Enum serializado | `TipoDeSolo` é gravado por valor | Tipos novos **sempre no fim** |
| Escala real ≠ sintética | Limiares de alagamento calibrados em terreno sintético | Recalibrar sobre a areia real, agora que a caixa existe |

---

## 12. Pendência de documentação

A montagem física foi concluída, mas `ROADMAP.md`, `docs/MONTAGEM-FISICA.md` e
`docs/DIARIO-DE-BORDO.md` **ainda afirmam que nada rodou sobre areia real**. Falta
registrar: comprimento final da viga, altura do sensor até a areia, cobertura medida em %,
modelo e posição do projetor, e o que aconteceu de fato com os riscos previstos (sombra de
IV das mãos, ruído da areia, escala do relevo).

Enquanto isso não for registrado, a documentação está desatualizada no ponto mais visível
para quem chega ao projeto.
