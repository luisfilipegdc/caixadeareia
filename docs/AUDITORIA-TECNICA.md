# Auditoria técnica — Caixa de Areia Interativa

**Repositório:** `github.com/luisfilipegdc/caixadeareia`
**Commit auditado:** `4d68a8e` (`origin/main`, v1.3) — verificado idêntico ao remoto
**Escopo:** 16 arquivos-fonte C#/XAML, `.csproj`, `.sln`, configuração. Leitura integral.
**Data:** agosto de 2026

### Convenção de rótulos

| Rótulo | Significado |
|---|---|
| **ESTADO ATUAL** | Fato comprovado por leitura do código, com arquivo/classe/método citados |
| **RECOMENDAÇÃO** | Proposta minha, não estado do sistema |
| **HIPÓTESE** | Não confirmável só pelo código; exige medição, hardware ou teste em campo |

---

# SUMÁRIO EXECUTIVO

A resposta à pergunta central da auditoria — *"esta base consegue virar uma plataforma modular sem se tornar um monólito de modos hardcoded?"* — é:

> **Sim, mas não pelo caminho em que está. A abstração que deveria permitir isso (`ISimulationModule`) existe, é implementada por três classes e nunca é usada. O acoplamento real está no renderizador, que conhece cada simulação pelo nome.**

Três achados dominam o relatório:

1. **`ISimulationModule` é uma abstração morta.** Três implementações, **zero** usos polimórficos em todo o repositório. O `SandboxEngine` guarda campos concretos tipados (`WaterSimulation? Agua`, `EarthquakeSimulation? Terremoto`, `FireSimulation? Fogo`). A interface é decorativa.

2. **`TopographicRenderer.Render()` recebe 16 parâmetros, 11 deles específicos de módulos.** Adicionar um fenômeno hoje exige tocar em **cinco** arquivos. Este é o gargalo arquitetural real — não a falta de ECS, não a CPU.

3. **Boa parte da capacidade construída é inalcançável pelo usuário.** Os seis cenários pedagógicos, a queimada inteira, o despejo de água, a pré-saturação do solo e a pintura por região existem, compilam, e não têm nenhum caminho de UI. Não é dívida de arquitetura: é funcionalidade escrita e não conectada.

E um achado que atinge diretamente o princípio declarado de honestidade científica:

4. **A interface apresenta números didáticos com aparência de medição.** `PropriedadesDoSolo.Resumo` exibe *"Absorve 3,2 mm/s"* ao professor; o comentário no código diz explicitamente que esses valores **não são medições de campo**. Essa ressalva existe no código e nunca chega à tela. Além disso, `VolumeLitros` é calculado com uma largura de caixa hardcoded que quase certamente não corresponde à caixa real.

**Veredito curto:** a fundação de captura, processamento e calibração é sólida e madura — preservar. A camada de simulação/renderização precisa de **uma refatoração cirúrgica bem delimitada** (não reescrita). Estimo que **~75% do código atual seja preservável**.

---

# 1. MAPA REAL DA ARQUITETURA

## 1.1 Estrutura de projetos

**ESTADO ATUAL.** Um único projeto. Não há projeto de testes.

```
CaixaInterativa.sln
└── src/CaixaInterativa/CaixaInterativa.csproj
    net8.0-windows · x64 · UseWPF · UseWindowsForms · AllowUnsafeBlocks · Nullable enable
```

`UseWindowsForms` existe **apenas** para `System.Windows.Forms.Screen` (enumeração de monitores, que o WPF não expõe). O `.csproj` remove os usings implícitos de `System.Windows.Forms` e `System.Drawing` para evitar colisão com o WPF. **Decisão boa e bem documentada** no próprio `.csproj`.

**ESTADO ATUAL.** Zero dependências NuGet. Todo o interop é P/Invoke próprio. Para software que precisa rodar numa escola sem suporte técnico, isso é uma decisão acertada: nada para atualizar, nada para quebrar.

## 1.2 Namespaces e responsabilidades

| Namespace | Classes | Responsabilidade |
|---|---|---|
| `CaixaInterativa.Depth` | `IDepthSource`, `RawDepthFrame`, `NuiNative`, `KinectV1Source`, `SimulatedDepthSource` | Captura e abstração de hardware |
| `CaixaInterativa.Processing` | `DepthProcessor` | Profundidade bruta → campo de alturas calibrado |
| `CaixaInterativa.Simulation` | `ISimulationModule`, `SoilMap`, `TipoDeSolo`, `PropriedadesDoSolo`, `WaterSimulation`, `FireSimulation`, `EarthquakeSimulation`, `Cenario` | Fenômenos e ambiente |
| `CaixaInterativa.Rendering` | `TopographicRenderer` | Campo de alturas + estados → pixels BGRA |
| `CaixaInterativa.Config` | `AppConfig`, `ProcessingSettings`, `RenderSettings`, `ProjectionSettings`, `SensorSettings`, `InterfaceSettings`, `CalibrationStore`, `CalibrationData` | Persistência |
| `CaixaInterativa.Views` | `MainWindow`, `ProjectionWindow` | Interface e projeção |
| `CaixaInterativa` (raiz) | `SandboxEngine`, `EngineState`, `AppInfo`, `App` | Orquestração e identidade |

## 1.3 Diagrama real (não o do README)

```
┌─ THREAD "KinectDepthCapture" (ThreadPriority.AboveNormal) ──────────────┐
│                                                                          │
│  Kinect10.dll                                                            │
│    │ WaitForSingleObject(_frameEvent, 200)      [KinectV1Source.Loop]     │
│    ▼                                                                     │
│  NuiImageStreamGetNextFrame → IntPtr                                     │
│    │ Marshal.PtrToStructure<NuiImageFrame>       ← cópia 1 (48 bytes)     │
│    ▼                                                                     │
│  TextureLockRect (vtable slot 5)                                         │
│    │ CopyDepth: unsafe, >>3, satura→0            ← cópia 2 (614 KB)       │
│    ▼ ushort[] managed  (buffer reutilizado)                              │
│  (ushort[])managed.Clone()                       ← cópia 3 (614 KB, LOH) │
│    │                                                                     │
│    ▼ FrameArrived?.Invoke(RawDepthFrame)                                 │
└────┬─────────────────────────────────────────────────────────────────────┘
     │  Volatile.Write(ref _latestFrame)     [SandboxEngine.OnFrameArrived]
     │  ↯ ÚNICO ponto de sincronização entre threads. Sem lock, sem fila.
     │    Slot único: quadro novo sobrescreve quadro não consumido.
     ▼
┌─ UI THREAD (DispatcherTimer @16ms, DispatcherPriority.Render) ───────────┐
│                                                                          │
│  SandboxEngine.OnTick                                                    │
│    │ Volatile.Read(_latestFrame)                                         │
│    │ if (FrameNumber == _lastRenderedFrameNumber) return;  ← anti-retrabalho
│    ▼                                                                     │
│  DepthProcessor.ProcessFrame(frame, _heights)                            │
│    │ lock(_gate) só para checar calibração em curso                      │
│    │ Etapa 1: buracos + α adaptativo   → for SERIAL, 307.200 iterações   │
│    │ Etapa 2: BoxBlur separável        → for SERIAL, 2 passadas          │
│    ▼ float[] _heights (640×480, reutilizado)          ← cópia 4          │
│                                                                          │
│  dt = min(0.1s, _simClock.Elapsed);  _simClock.Restart()                 │
│                                                                          │
│  ┌── Agua.Atualizar   (se Ativo)   320×240, Parallel.For, ≤12 substeps   │
│  ├── Terremoto.Atualizar (se Ativo) 320×240                              │
│  └── Fogo.Atualizar    (se Ativo)  320×240, passo fixo 1/20s, ≤4/quadro  │
│                                                                          │
│  TopographicRenderer.Render(_heights, …16 parâmetros…)                   │
│    │ Parallel.For(0, h)  — o ÚNICO Parallel.For do caminho de imagem     │
│    │ recorte por ROI, rampa, hillshade, curvas, água, sismo, fogo        │
│    ▼ byte[] _buffer BGRA (reutilizado; realocado só se ROI mudar)        │
│                                                                          │
│  WriteableBitmap.WritePixels(...)                          ← cópia 5     │
└────┬─────────────────────────────────────────────────────────────────────┘
     │
     ├─► MainWindow.Preview.Source        (mesma WriteableBitmap)
     └─► ProjectionWindow.Projected.Source
             │ RenderTransform: Scale × Rotate × Translate (afim, 2D)
             ▼
          [ PROJETOR ]

┌─ TIMERS AUXILIARES ──────────────────────────────────────────────────────┐
│  MainWindow._uiTimer        500ms → AtualizarIndicadores + AtualizarSimulacao
│  ProjectionWindow._dadosTimer 500ms → AtualizarDados (painel de números)
│  SandboxEngine._reconnectTimer 3s  → só existe durante falha do sensor
└──────────────────────────────────────────────────────────────────────────┘
```

## 1.4 Estado compartilhado — inventário

**ESTADO ATUAL.**

| Estado | Dono | Acesso cruzado de thread? |
|---|---|---|
| `SandboxEngine._latestFrame` | engine | **Sim** — escrito na captura, lido na UI, via `Volatile` |
| `RawDepthFrame.Data` | por-quadro | Não — cada quadro é um clone novo (é o que torna o `Volatile` seguro) |
| `DepthProcessor._basePlaneMm/_baseValid/_smoothed/_everValid/_scratch` | processor | Não — só UI thread |
| `SandboxEngine._heights` | engine | Não — só UI thread |
| `WaterSimulation.Solo` (`SoilMap`) | água | Não — **compartilhado por referência** com `Terremoto` e `Fogo` |
| `TopographicRenderer._buffer` | renderer | Não |
| `AppConfig` | MainWindow | Não — mas mutado por `ProjectionWindow.OnKeyDown` também |

**Observação importante:** `SandboxEngine.StartSource` (linhas 155–163) monta o grafo de compartilhamento explicitamente: `Terremoto` e `Fogo` recebem `Agua.Solo`, e `Fogo` recebe `Agua.Profundidade`. **Este é o único mecanismo de comunicação entre módulos que existe, e é feito por injeção manual de referências no construtor.** Não escala para dezenas de fenômenos.

---

# 2. PIPELINE COMPLETO DE UM FRAME

**ESTADO ATUAL.** Reconstruído linha a linha.

| # | Etapa | Arquivo · Classe · Método | Input | Output | Thread | Buffer | Aloca? |
|---|---|---|---|---|---|---|---|
| 1 | Espera do evento | `Depth/KinectV1Source.cs` · `KinectV1Source.Loop` | `_frameEvent` | — | Captura | — | não |
| 2 | Obtenção do quadro | idem · `NuiImageStreamGetNextFrame` | `_streamHandle` | `IntPtr` para struct do runtime | Captura | nativo | não |
| 3 | Desempacotamento da struct | idem · `Marshal.PtrToStructure<NuiImageFrame>` | `IntPtr` | `NuiImageFrame` (48 B) | Captura | pilha | marshalling |
| 4 | Lock da textura | `Depth/NuiNative.cs` · `NuiNative.TextureLockRect` | ponteiro da textura | `NuiLockedRect` | Captura | nativo | **sim — delegate** |
| 5 | Cópia + shift | `Depth/KinectV1Source.cs` · `CopyDepth` (unsafe) | `IntPtr` | `ushort[640×480]` | Captura | `managed` reutilizado | não |
| 6 | Publicação | idem · `Loop` | `managed` | `RawDepthFrame` | Captura | **novo array** | **sim — 614 KB LOH** |
| 7 | Recebimento | `SandboxEngine.cs` · `OnFrameArrived` | `RawDepthFrame` | — | Captura | slot único | não |
| 8 | Descarte de repetido | idem · `OnTick` | `FrameNumber` | — | UI | — | não |
| 9 | Alturas + suavização | `Processing/DepthProcessor.cs` · `ProcessFrame` | `ushort[]` | `float[]` mm | UI | `_smoothed`, `_scratch` | não |
| 10 | Blur separável | idem · `BoxBlur` (static private) | `_smoothed` | `outputHeightsMm` | UI | `_scratch` | não |
| 11 | Água | `Simulation/WaterSimulation.cs` · `Atualizar` | `float[]` 640×480 | campos 320×240 | UI | pré-alocados | **sim — 2 `object` por quadro** |
| 12 | Fogo | `Simulation/FireSimulation.cs` · `Atualizar`→`Propagar` | idem | `_calor` | UI | pré-alocados | **sim — `List<int>` por passo** |
| 13 | Composição | `Rendering/TopographicRenderer.cs` · `Render` | tudo acima | `byte[]` BGRA | UI (`Parallel.For`) | `_buffer` | não |
| 14 | Envio ao WPF | `SandboxEngine.cs` · `OnTick` · `WriteableBitmap.WritePixels` | `byte[]` | bitmap | UI | back buffer WPF | não |
| 15 | Transformação | `Views/ProjectionWindow.xaml.cs` · `ApplyTransform` | bitmap | tela | UI/compositor | GPU do WPF | não |

## 2.1 Cópias de memória por quadro

**ESTADO ATUAL.** Os dados de profundidade são copiados **três vezes** antes de virarem altura:

1. `CopyDepth` — nativo → `managed` (necessária: desempacota o shift de 3 bits)
2. `managed.Clone()` — `managed` → array novo (é o que garante segurança entre threads)
3. `ProcessFrame` — `ushort[]` → `_smoothed` → `outputHeightsMm`

A cópia 2 é a cara e a discutível. Ver seção de performance.

## 2.2 Onde a UI thread participa

**ESTADO ATUAL.** **Toda a computação pesada roda na UI thread.** Processamento de profundidade, as três simulações e a renderização acontecem dentro de `SandboxEngine.OnTick`, que é um `DispatcherTimer` com `DispatcherPriority.Render`.

Só a captura roda fora. Isso significa que qualquer travamento de simulação vira **congelamento da interface do professor**, não só da projeção.

**HIPÓTESE.** Com as três simulações ativas simultaneamente sobre areia real, o orçamento de 33 ms pode estourar e a UI passa a engasgar. Não confirmável sem profiling na máquina real — ver seção 12.

---

# 3. RESPOSTA À PERGUNTA CENTRAL

> *"Esta base consegue evoluir para uma plataforma modular capaz de suportar dezenas de experiências sem virar um monólito de modos hardcoded?"*

## 3.1 O que o código diz

**ESTADO ATUAL — achado principal.** `ISimulationModule` (`Simulation/ISimulationModule.cs`) declara:

```csharp
string Nome { get; }
int Width { get; }  int Height { get; }
bool Ativo { get; set; }
void Atualizar(float[] terrenoMm, int larguraTerreno, int alturaTerreno, float dt);
void Limpar();
```

Busca em todo o repositório por `ISimulationModule`: **5 ocorrências** — a declaração, e três `: ISimulationModule` nas classes. Busca por uso polimórfico (`List<ISimulationModule>`, `ISimulationModule[]`, `IEnumerable<ISimulationModule>`, parâmetro do tipo): **zero**.

O `SandboxEngine` declara campos concretos:

```csharp
public WaterSimulation?      Agua       { get; private set; }   // linha 51
public EarthquakeSimulation? Terremoto  { get; private set; }   // linha 57
public FireSimulation?       Fogo       { get; private set; }   // linha 60
```

**A interface não é usada. É documentação com sintaxe de C#.**

## 3.2 Por que ela não pode ser usada como está

**ESTADO ATUAL.** Porque falta a metade que importa: **a interface não diz nada sobre saída visual.** Um módulo produz `float[] Profundidade`, `float[] Calor`, `float[] Intensidade` — cada um com nome, semântica e forma de desenho diferentes. Quem sabe compor isso é o renderizador, e ele sabe **por código hardcoded**:

`Rendering/TopographicRenderer.cs` · `TopographicRenderer.Render` — assinatura real:

```csharp
public byte[] Render(
    float[] heightsMm, int fieldWidth, int fieldHeight,
    ProjectionSettings projection, ProcessingSettings processing, RenderSettings render,
    float[]? waterMm = null, int waterWidth = 0, int waterHeight = 0, float[]? waterSpeed = null,
    float[]? quakeNow = null, float[]? quakeDamage = null, int quakeWidth = 0, int quakeHeight = 0,
    float[]? fireHeat = null, int fireWidth = 0, int fireHeight = 0)
```

**16 parâmetros. 11 são de módulos específicos.** E o corpo tem três blocos `if` nomeados: água (linhas ~148–180), terremoto (~184–215), fogo (~227–243).

## 3.3 O custo real de adicionar um fenômeno hoje

**ESTADO ATUAL.** Para acrescentar um único fenômeno novo, é preciso editar:

| # | Arquivo | O que muda |
|---|---|---|
| 1 | `Simulation/NovoFenomeno.cs` | criar |
| 2 | `SandboxEngine.cs` | propriedade concreta + instanciação em `StartSource` + chamada em `OnTick` + N argumentos no `Render` |
| 3 | `Rendering/TopographicRenderer.cs` | +3 ou 4 parâmetros na assinatura + bloco de blending no laço de pixels |
| 4 | `Views/MainWindow.xaml.cs` | `enum Simulacao` + item do `CmbSimulacao` + `switch` em `OnSimulacaoChanged` + método `Executar…` + `switch` em `AtualizarBotaoExecutar` + bloco em `AtualizarSimulacao` + limpeza em `OnSecar` |
| 5 | `Views/MainWindow.xaml` | painel de configuração próprio (`CfgChuva`, `CfgTremor`, …) |
| 6 | `Views/ProjectionWindow.xaml.cs` | mais um `if` em `AtualizarDados` |

**Seis arquivos, com cadeias de `if`/`switch` em quatro deles.** Esse é exatamente o padrão que o usuário quer evitar — e ele já está instalado, com apenas dois modos:

```csharp
// Views/MainWindow.xaml.cs, linha 193
private enum Simulacao { Chuva, Terremoto }
```

## 3.4 Resposta

**Não, não como está** — mas o problema é menor e mais concentrado do que parece.

O acoplamento **não** está espalhado por 6.000 linhas. Está em **três pontos**: a assinatura do `Render`, o corpo do laço de pixels, e o `enum Simulacao` da UI. As simulações em si (`WaterSimulation`, `FireSimulation`) são **classes autocontidas e bem isoladas** — não conhecem a UI, não conhecem o renderizador, não conhecem o Kinect.

**RECOMENDAÇÃO.** O caminho é fazer `ISimulationModule` produzir **camadas de saída descritas por dados**, e o renderizador consumir uma lista dessas camadas em vez de parâmetros nomeados. Detalhado na seção 9.

---

# 4. A CAPACIDADE MORTA

Este é o achado mais surpreendente da auditoria, e não é arquitetural — é de integração.

**ESTADO ATUAL.** Buscas em todo o repositório (`src/**/*.cs`, `src/**/*.xaml`), excluindo a própria declaração:

| Membro | Arquivo | Chamado de fora? |
|---|---|---|
| `FireSimulation.Atear(u, v)` | `Simulation/FireSimulation.cs:121` | **NÃO — 0 chamadas** |
| `Cenario.Todos` (6 cenários) | `Simulation/Cenarios.cs:39` | **NÃO — 0 usos fora do arquivo** |
| `WaterSimulation.DespejarEm(...)` | `Simulation/WaterSimulation.cs:492` | **NÃO — 0 chamadas** |
| `WaterSimulation.PreSaturar(fracao)` | `Simulation/WaterSimulation.cs:521` | **NÃO — 0 chamadas** |
| `SoilMap.Pintar(u, v, raio, tipo)` | `Simulation/SoilMap.cs:214` | **NÃO — 0 chamadas** |
| `SoilMap.Composicao()` | `Simulation/SoilMap.cs:257` | **NÃO — 0 chamadas** |
| `SoilMap.PintarPorAltitude(...)` | `Simulation/SoilMap.cs:240` | Só pelos `Cenario`, que também estão mortos |
| `WaterSimulation.Erosao` (`float[]`) | `Simulation/WaterSimulation.cs:152` | **NÃO — calculado todo quadro, nunca desenhado** |
| `WaterSimulation.ErosaoTotal` | `:177` | **NÃO — nunca exibido** |
| `WaterSimulation.EscoadoLitros` | `:174` | **Nunca calculado.** Só zerado em `Limpar`. É sempre 0. |
| `NuiNative.TextureRelease` | `Depth/NuiNative.cs:194` | **NÃO — código morto** |
| `NuiNative.NuiCameraElevationGetAngle` | `:155` | **NÃO — código morto** |

## 4.1 Consequências

**A queimada é inacessível.** 350 linhas funcionais — com vento, efeito de encosta, barreira de água e gravação de cicatriz no `SoilMap` — instanciadas em `SandboxEngine.StartSource:160`, atualizadas em `OnTick:318`, desenhadas pelo renderizador (`:335`) e com painel próprio em `ProjectionWindow.AtualizarDados:208`. **Só não existe nada que chame `Atear()`.** O `CmbSimulacao` (`MainWindow.xaml.cs:203-204`) oferece apenas "Chuva e enchente" e "Terremoto".

**Os seis cenários pedagógicos são inacessíveis.** `Cenarios.cs` contém "Enchente no Rio Grande do Sul", "A mesma enchente com a várzea preservada", "Cidade que planejou a drenagem", "Depois da queimada", "Bacia preservada" — cada um com contexto real, pergunta investigativa, composição de solo por altitude, intensidade e duração de chuva e saturação inicial. **Nada disso é alcançável.** É precisamente a camada pedagógica que o roadmap descreve como o diferencial do projeto.

**A composição de território é inacessível.** A única forma de definir cobertura pela UI é `MainWindow.OnCoberturaChanged` → `_engine.Agua.Solo.Preencher(tipo)`, que **preenche o mapa inteiro com um único tipo**. Mata na encosta + cidade no vale — a configuração que dá sentido à pergunta "por que alagou justamente ali" — existe em `PintarPorAltitude` e não tem caminho de UI.

**A erosão é computada e jogada fora.** `AcumularErosao` roda em todo quadro com `Parallel.For` sobre 76.800 células (`WaterSimulation.cs:426`), preenche `_erosao[]`, acumula `ErosaoTotal` — e nem o array nem o total chegam ao renderizador ou à interface. É custo de CPU puro.

**`EscoadoLitros` é sempre zero.** Declarado em `:174`, zerado em `Limpar` (`:543`), **nunca incrementado**. `MoverAgua` remove água pelas bordas quando `BordasEscoam` é true, mas não contabiliza. Se um dia for exibido sem correção, mostrará "0 L escoados" numa enchente.

**RECOMENDAÇÃO.** Antes de qualquer refatoração arquitetural: **conectar o que já existe**. É a maior razão custo/benefício do projeto inteiro. Ligar a queimada e os cenários é trabalho de UI, não de arquitetura, e multiplica a capacidade pedagógica disponível hoje.

---

# 5. HONESTIDADE CIENTÍFICA — AUDITORIA DEDICADA

O princípio declarado é que o software deve distinguir medição, derivação, modelo e efeito visual. Auditei cada etapa do pipeline contra esse critério.

## 5.1 Classificação de cada etapa

| Etapa | Local | Classificação | Observação |
|---|---|---|---|
| Distância bruta em mm | `KinectV1Source.CopyDepth` | **MEDIÇÃO** | Direta do sensor, com shift de 3 bits |
| Saturado → 0 | idem | **DERIVAÇÃO** | Convenção "sem leitura" |
| Plano-base por pixel | `DepthProcessor.AccumulateCalibration` | **MEDIÇÃO** (média de 60 quadros) | Exige ≥5 amostras válidas |
| Altura = base − distância | `DepthProcessor.ProcessFrame` | **DERIVAÇÃO** | Subtração exata |
| Clamp em `[MinHeightMm, MaxHeightMm]` | idem | **ESTIMATIVA** | Descarta informação real fora da faixa |
| Preenchimento de buracos | idem | **ESTIMATIVA** | Repete o último valor bom — **não é medição** |
| Suavização temporal α adaptativo | idem | **ESTIMATIVA** | Filtro IIR; introduz atraso |
| Box blur espacial | `DepthProcessor.BoxBlur` | **INTERPOLAÇÃO** | Raio 3 por padrão = média de 7×7 |
| Rampa de cor por altitude | `TopographicRenderer.Sample` | **EFEITO VISUAL** | Escala relativa, não altitude absoluta |
| Hillshade | `TopographicRenderer.Render` | **EFEITO VISUAL** | Luz do noroeste, `* 0.04f` arbitrário |
| Curvas de nível | idem | **DERIVAÇÃO** sobre dado já estimado | Intervalo configurável |
| Alinhamento afim projetor↔caixa | `ProjectionWindow.ApplyTransform` | **ESTIMATIVA** ajustada à mão | Sem correção de perspectiva |
| Escoamento da água | `WaterSimulation.AtualizarFluxos/MoverAgua` | **MODELO DIDÁTICO** | Tubos virtuais; conserva massa |
| Infiltração por solo | `WaterSimulation.AplicarInfiltracao` | **MODELO DIDÁTICO** | Parâmetros não medidos |
| Erosão | `WaterSimulation.AcumularErosao` | **MODELO DIDÁTICO** | Limiar de 40 mm/s arbitrário |
| Propagação de fogo | `FireSimulation.Propagar` | **MODELO DIDÁTICO** estocástico | `chance = combustível × 0.30` |
| Ondas sísmicas | `EarthquakeSimulation` | **MODELO DIDÁTICO** | Velocidade de onda deliberadamente irreal (260 mm/s) |

**Observação positiva.** As classificações estão **corretamente documentadas nos comentários do código**. `EarthquakeSimulation` (linhas 28–31) declara: *"a caixa não tem falhas geológicas nem camadas de subsuperfície… Isto é um modelo didático das consequências, não uma previsão sísmica."* `PropriedadesDoSolo` (linhas 37–43) declara: *"Os números não são medições de campo — são valores didáticos."* `VelocidadeOndaMmPorSegundo` explica que *"um valor realista cruzaria 1,25 m instantaneamente"*.

**A rigor intelectual do código é alta. O problema é que nada disso sai do código.**

## 5.2 Violações concretas do princípio

### 🔴 VIOLAÇÃO 1 — Falsa precisão apresentada ao professor

**ESTADO ATUAL.** `Simulation/SoilMap.cs:55-58`:

```csharp
public string Resumo =>
    $"Absorve {InfiltracaoMmPorSegundo:F1} mm/s · " +
    $"guarda até {ArmazenamentoMm:F0} mm · " +
    $"resiste {(ResistenciaAErosao * 100):F0}% à erosão";
```

Exibido em `MainWindow.OnCoberturaChanged`:
```csharp
TxtCoberturaInfo.Text = prop.Descricao + "\n" + prop.Resumo;
```

O professor lê: **"Mata — Absorve 3,2 mm/s · guarda até 160 mm · resiste 95% à erosão"**.

Isso tem a aparência exata de uma medição hidrológica. Uma casa decimal em mm/s comunica precisão que não existe. O comentário que diz que são valores didáticos está **doze linhas acima, no código-fonte**, onde nenhum professor vai olhar. Este é o exemplo mais direto do risco que o próprio pedido da auditoria descreve — *"Cerrado = infiltração 0.65 seria perigoso"* — e ele já está em produção.

### 🔴 VIOLAÇÃO 2 — Volume em litros calculado com dimensão hardcoded

**ESTADO ATUAL.** `WaterSimulation` (construtor, linha 127):

```csharp
public WaterSimulation(int larguraSensor, int alturaSensor, float larguraCaixaMm = 1250f)
{
    _w = Math.Max(2, larguraSensor / 2);
    _tamanhoCelulaMm = larguraCaixaMm / _w;
```

`SandboxEngine.StartSource:154` chama `new WaterSimulation(source.Width, source.Height)` — **sempre com o padrão de 1250 mm**. Nunca é passado outro valor.

Esse número é usado em:
```csharp
double areaCelula = _tamanhoCelulaMm * _tamanhoCelulaMm;
VolumeLitros = soma * areaCelula * 1e-6;
InfiltradoLitros += infiltradoMm * areaCelula * 1e-6;
```

E `VolumeLitros` / `InfiltradoLitros` são exibidos como números absolutos ao professor e à turma:
- `MainWindow.AtualizarSimulacao`: `$"Escoando · {agua.VolumeLitros:F1} L"`
- `ProjectionWindow.AtualizarDados`: `Linha("Água na superfície", $"{agua.VolumeLitros:F1} L", ...)`

**Três problemas encadeados:**

1. A caixa real, conforme `docs/MONTAGEM-FISICA.md`, tem **101 cm de largura × 125 cm de comprimento**. O valor 1250 mm corresponde ao comprimento, mas é dividido por `_w`, que deriva do eixo de **640 px** do sensor — o eixo horizontal, de 57°.

2. Pior: o eixo de 640 px cobre `1,0859 × distância`. A 1,28 m isso dá **139 cm**, não 125. O tamanho de célula está errado por ~11%, e a **área** (que é o quadrado) por **~24%**.

3. E não existe nenhuma relação estabelecida entre o campo de visão do sensor e as bordas físicas da caixa — ver a questão da ROI, seção 6.3.

Resultado: **"12,4 L" é projetado na parede da sala como se fosse uma medição, e tem erro sistemático de ordem de 25% por uma constante que ninguém configurou.**

### 🟠 VIOLAÇÃO 3 — Nenhuma declaração de natureza na interface

**ESTADO ATUAL.** Nem `MainWindow.xaml` nem `ProjectionWindow.xaml.cs` exibem qualquer texto distinguindo o que é medido do que é modelado. `ProjectionWindow.AtualizarDados` mostra "ÁREA ALAGADA 34%" em fonte 34 bold, com a mesma autoridade visual com que mostraria uma leitura de sensor.

**RECOMENDAÇÃO.** Cada grandeza exibida deveria carregar sua classificação como **dado**, não como texto solto. Ver seção 10.3.

---

# 6. SIMULAÇÕES EXISTENTES — AUDITORIA INDIVIDUAL

## 6.1 `WaterSimulation` (550 linhas)

| Aspecto | Estado |
|---|---|
| **Objetivo** | Escoamento, acúmulo, infiltração, saturação e erosão sobre o relevo |
| **Modelo** | Tubos virtuais (Mei, Decaudin & Hu) — 4 tubos por célula, aceleração pela diferença de nível `terreno + água` |
| **Resolução** | 320×240 (metade do sensor). Justificativa CFL documentada e correta |
| **Estado interno** | `_agua`, `_aguaNova`, `_terreno`, `_fluxoE/D/C/B`, `_velocidade`, `_erosao`, `_saturacao` — 9 arrays de 76.800 floats ≈ 2,8 MB |
| **Inputs** | `float[] terrenoMm`, `dt`, `SoilMap` |
| **Outputs** | `Profundidade`, `Velocidade`, `Erosao`(morto), `Saturacao`, + 8 métricas escalares |
| **Parâmetros** | `ChuvaMmPorSegundo`, `BordasEscoam`, `Amortecimento`, `EscalaInfiltracao`, `LimiarAlagamentoMm`, `DrenagemProfundaPorSegundo` — **nenhum exposto na UI** |
| **Custo** | ≤12 substeps × (AtualizarFluxos + MoverAgua), ambos `Parallel.For`. + 3 passadas seriais em `CalcularEstatisticas` |

**Classificação:** escoamento = MODELO DIDÁTICO com base física real (conserva massa, limitador de saída em `AtualizarFluxos` impede geração de água do nada — decisão correta e comentada). Infiltração/erosão/saturação = MODELO DIDÁTICO com parâmetros não medidos.

**Pontos fortes (com o porquê):**
- O limitador `if (saida * dt > _agua[i])` com reescalonamento proporcional é a diferença entre um solver estável e um que "cria água". Está certo.
- Substeps por CFL em vez de reduzir a gravidade: preserva o fenômeno em vez de deformá-lo para caber no orçamento. Comentado explicitamente. Decisão madura.
- `LimiarAlagamentoMm = 8` com justificativa medida (*"1 mm marcaria 96% em tudo"*).
- O comentário em `AtualizarFluxos` registrando que o multiplicador de rugosidade a 3× fazia a mata alagar mais que a cidade — **isso é rastreabilidade de calibração de modelo**, e é raro de encontrar.

**Problemas:**

| Sev. | Problema | Local |
|---|---|---|
| Alta | `EscoadoLitros` nunca calculado | `:174`, `MoverAgua` |
| Alta | `larguraCaixaMm` hardcoded → volume errado | construtor `:127` |
| Média | `Erosao`/`ErosaoTotal` computados e descartados | `AcumularErosao:426` |
| Média | `object trava = new()` alocado por quadro, 2× | `:355`, `:429` |
| Média | `CalcularEstatisticas` — 3 varreduras seriais de 76.800 células por quadro | `:462` |
| Baixa | Comentário `<summary>` de `AcumularErosao` está órfão acima de `DrenarSolo` | `:404-410` |

## 6.2 `FireSimulation` (350 linhas)

| Aspecto | Estado |
|---|---|
| **Objetivo** | Propagação de incêndio; grava cicatriz no solo ao terminar |
| **Modelo** | Autômato celular estocástico, vizinhança-4, passo fixo 1/20 s |
| **Fatores** | combustível (por `TipoDeSolo`), vento (direção + força), água (barreira), encosta (`chance *= 1 + min(1.2, subida*0.06)`) |
| **Estado** | `_estado`, `_combustivel`, `_calor`, `_terreno` — 4 arrays de 76.800 |
| **Acoplamento** | Lê `Agua.Profundidade`; escreve em `Solo.Celulas` via `AplicarCicatriz` |
| **Custo** | Laço **serial** sobre 76.800 células, até 4 passos por quadro |

**Classificação:** MODELO DIDÁTICO estocástico. Sem base em modelos de comportamento de fogo reais (Rothermel etc.) — e o código não afirma que tenha.

**Ponto forte:** o comentário sobre `_celulasEmChamas` — *"Usar porcentagem para decidir se o fogo acabou não funciona: uma célula acesa em 76.800 é 0,0013%… o incêndio morria no primeiro quadro"* — é diagnóstico de bug real preservado. Exatamente o tipo de conhecimento que se perde sem essa disciplina.

**Problemas:**

| Sev. | Problema | Evidência |
|---|---|---|
| **Crítica (funcional)** | `Atear()` nunca é chamado — módulo inacessível | 0 chamadas no repositório |
| **Alta** | Barreira de água é um trinco permanente | `TentarAcender`: `if (Agua[i] > 2f) { _estado[i] = Estado.NaoQueima; return; }` — `NaoQueima` é terminal. Uma célula testada uma vez nunca é reavaliada. **Um canal cavado durante o incêndio não barra o fogo.** |
| Média | `new List<int>()` alocada a cada passo de `Propagar` (até 4×/quadro = 120/s) | `Propagar:` primeira linha |
| Média | `Atear()` monta `List<int> candidatos` varrendo 76.800 células | `:135-141` |
| Média | Não-determinístico: `_sorteio = semente == 0 ? new Random() : new Random(semente)`, e o construtor é sempre chamado sem semente | `SandboxEngine:160` |
| Baixa | `Propagar` é serial; não paralelizável trivialmente por causa da lista compartilhada | — |

## 6.3 `EarthquakeSimulation` (328 linhas)

| Aspecto | Estado |
|---|---|
| **Objetivo** | Ondas sísmicas, amplificação por solo, risco de deslizamento por declividade |
| **Modelo** | Anel de onda expandindo a 260 mm/s, dano = intensidade × amplificação × instabilidade |
| **Honestidade** | **Exemplar** — o `<summary>` declara explicitamente que é modelo didático e por que a velocidade é irreal |

**ESTADO ATUAL.** É a simulação mais bem documentada quanto a limitações. O usuário decidiu removê-la; do ponto de vista técnico, a remoção é limpa (7 pontos de contato mapeados) e o código fica recuperável no histórico.

**Observação.** Se um dia voltar como vulcão/lava, o `_declivMaximo` baseado no ângulo de repouso da areia seca (`tan(34°) ≈ 0,675`) é reaproveitável — é a única constante do projeto derivada de física real da areia.

---

# 7. MODELO DE TERRENO — O QUE FALTA

**ESTADO ATUAL.** O terreno é **um único `float[]`**: `SandboxEngine._heights`, 640×480, em mm relativos ao plano-base. Não existe classe de terreno. Não existe nenhuma propriedade derivada compartilhada.

Cada consumidor recalcula o que precisa, do zero, todo quadro:

| Consumidor | O que recalcula | Local |
|---|---|---|
| `TopographicRenderer` | Gradiente para hillshade (`dzdx`, `dzdy`) | `Render`, por pixel |
| `WaterSimulation` | Reamostragem para 320×240 | `ReamostrarTerreno` |
| `FireSimulation` | Reamostragem para 320×240 (**código duplicado**) | `ReamostrarTerreno` |
| `EarthquakeSimulation` | Reamostragem **+ declividade** | `ReamostrarTerreno` + cálculo próprio |
| `SoilMap.PintarPorAltitude` | Amostragem por vizinho mais próximo | `:240` |

**Três implementações independentes de `ReamostrarTerreno` com o mesmo corpo.** Verificado: `WaterSimulation.cs:221`, `FireSimulation.cs:` (dentro de `Atualizar`), `EarthquakeSimulation.cs`.

**ESTADO ATUAL.** Não existe: inclinação compartilhada, aspecto/orientação, detecção de depressões, acumulação de fluxo, divisores de água, bacias. Nenhum desses conceitos aparece no código.

**RECOMENDAÇÃO — `TerrainField`.** Uma classe que encapsula o campo de alturas e calcula derivadas **sob demanda, com cache invalidado por mudança significativa**:

| Propriedade | Frequência necessária | Custo | Estratégia |
|---|---|---|---|
| Alturas (mm) | todo quadro | — | fonte |
| Reamostragem para 320×240 | todo quadro | O(n) | **calcular uma vez, compartilhar** — hoje são 3× |
| Gradiente / inclinação | todo quadro (hillshade) | O(n) | calcular uma vez, compartilhar com renderer e sismo |
| Aspecto (orientação da encosta) | sob demanda | O(n) | cache |
| Depressões (sinks) | só quando o relevo muda | O(n log n) | cache com invalidação |
| Acumulação de fluxo (D8) | só quando o relevo muda | O(n log n) | cache com invalidação |
| Bacias / divisores | só quando o relevo muda | O(n log n) | cache com invalidação |

**Como detectar "mudou significativamente":** manter uma soma de diferenças absolutas por quadro; acima de um limiar, invalidar os caches caros. Numa aula, os alunos moldam em rajadas e depois observam — o padrão de uso favorece cache.

**Ganho pedagógico direto:** acumulação de fluxo e bacias hidrográficas permitem projetar **a rede de drenagem antes de chover** — "onde vocês acham que a água vai correr?" seguido da resposta calculada. Hoje isso é impossível porque o conceito não existe.

---

# 8. RELEVO + AMBIENTE + FENÔMENO

## 8.1 Avaliação da separação proposta

**ESTADO ATUAL.** A separação **já existe parcialmente, e funciona**:

| Conceito | Onde está hoje | Qualidade |
|---|---|---|
| **Relevo** | `float[] _heights` | Existe, mas é primitivo solto (ver seção 7) |
| **Ambiente** | `SoilMap` + `TipoDeSolo` + `PropriedadesDoSolo` | **Bem modelado.** Tabela pré-calculada, 12 tipos, propriedades hidrológicas. É a melhor abstração do projeto |
| **Fenômeno** | `WaterSimulation`, `FireSimulation`, `EarthquakeSimulation` | Classes isoladas, mas sem contrato útil |
| **Parâmetros** | Propriedades públicas espalhadas | **Não modelado.** Nenhum é exposto na UI |
| **Experimento** | `MainWindow._historico` | Rudimentar — ver 8.2 |

**RECOMENDAÇÃO.** A separação proposta no pedido está **certa** e o código já caminha nela. Não force nomes novos: `SoilMap` já é o "Ambiente", e é bom. O que falta é:

1. Elevar o relevo de `float[]` a `TerrainField` (seção 7)
2. Dar a `ISimulationModule` um contrato de **saída** e de **parâmetros**
3. Criar a noção de **experimento** como dado, não como estado de UI

**Nomenclatura:** manter o padrão do projeto — domínio em português. `CampoDeRelevo`, `Cobertura` (já é `SoilMap`), `Fenomeno`, `Experimento`, `CamadaVisual`.

## 8.2 Comparação de cenários

**ESTADO ATUAL.** Existe, e é melhor do que eu esperava encontrar — mas é frágil.

`Views/MainWindow.xaml.cs`:
```csharp
private readonly List<(string Simulacao, string Cobertura, string Resultado, double Valor)> _historico = [];

private void Registrar(string simulacao, string cobertura, string resultado, double valor)
{
    if (valor <= 0) return;
    _historico.RemoveAll(h => h.Simulacao == simulacao && h.Cobertura == cobertura);
    _historico.Add((simulacao, cobertura, resultado, valor));
    AtualizarComparacao();
}
```

E `AtualizarComparacao` produz a frase-conclusão da aula:
```csharp
texto += $"\n\n{pior.Cobertura} teve {pior.Valor / melhor.Valor:F1}× " +
         $"o resultado de {melhor.Cobertura}, na mesma simulação.";
```

**Pedagogicamente, essa é a melhor ideia do projeto.** É exatamente a comparação descrita no pedido: mesmo relevo, coberturas diferentes, razão entre resultados.

**Problemas — todos sérios:**

| Sev. | Problema |
|---|---|
| **Alta** | **A chave é `(Simulacao, Cobertura)` — o relevo não entra.** Se os alunos mexerem na areia entre as duas execuções, o sistema compara resultados de terrenos diferentes e apresenta a razão como se fosse causada pela cobertura. **Isso é uma conclusão cientificamente falsa apresentada como resultado da aula.** |
| **Alta** | Intensidade e duração da chuva também não entram na chave. Comparar "garoa sobre mata" com "tempestade sobre cidade" produz a mesma frase confiante. |
| Média | Saturação inicial não entra |
| Média | O histórico vive só na memória da `MainWindow`; fechar o programa perde a aula |
| Média | Uma única métrica (`Valor`) por execução |

**RECOMENDAÇÃO — `Experimento` como registro imutável.** Um `record` que captura tudo o que define uma execução:

```
Experimento
├── AssinaturaDoRelevo   ← hash/estatísticas do campo de alturas no início
├── Cobertura            ← composição do SoilMap (% por tipo), não um nome
├── Fenomeno + Parametros
├── Metricas[]           ← várias, cada uma com sua classificação de honestidade
└── Instante
```

A comparação só é oferecida quando `AssinaturaDoRelevo` bate — e quando não bate, **o software diz isso**: *"o relevo mudou entre as execuções; a comparação não isola o efeito da cobertura."* Essa mensagem é conteúdo pedagógico de primeira ordem: ensina controle de variáveis.

---

# 9. EXTENSIBILIDADE — A DIREÇÃO RECOMENDADA

## 9.1 As alternativas, comparadas

| Abordagem | Vantagem | Desvantagem | Custo | Veredito |
|---|---|---|---|---|
| **Manter como está** | zero trabalho | 6 arquivos por fenômeno; explosão de `switch` | — | ❌ é o problema |
| **ECS (entity-component-system)** | flexível ao extremo | O domínio é **campos de grade**, não entidades. ECS resolve "muitos objetos heterogêneos"; aqui há poucos sistemas sobre os mesmos grids | alto | ❌ ferramenta errada |
| **Plugins com carregamento dinâmico** | terceiros estendem sem recompilar | Segurança, versionamento, depuração em sala de aula | alto | ❌ prematuro |
| **Pipeline de camadas de saída** | resolve o acoplamento real (renderer) com pouca cirurgia | Exige definir bem o contrato de camada | **baixo-médio** | ✅ |
| **Grafo de dependência entre fenômenos** | permite fogo→solo→água automaticamente | Complexidade de ordenação e ciclos | médio-alto | 🟡 depois |

## 9.2 Recomendação: contrato de saída por camadas

**RECOMENDAÇÃO.** Estender `ISimulationModule` com **três** adições, mantendo o resto:

```
ISimulationModule (atual: Nome, Width, Height, Ativo, Atualizar, Limpar)
  + IReadOnlyList<CamadaVisual> Camadas { get; }   ← o que desenhar
  + IReadOnlyList<Parametro>    Parametros { get; } ← o que a UI expõe
  + IReadOnlyList<Metrica>      Metricas { get; }   ← o que exibir, com classificação
```

Onde `CamadaVisual` descreve **como compor**, por dados:

```
CamadaVisual
├── Campo      : float[]        ← o dado
├── Largura, Altura : int
├── Ordem      : int            ← água=100, sismo=200, fogo=300
├── Mapeamento : ModoDeCor      ← Gradiente | Calor | Risco | Máscara
├── Limiar     : float          ← abaixo disso, não desenha
└── Opacidade  : Func<float,float> ou curva declarativa
```

`TopographicRenderer.Render` passa a ter a assinatura:

```csharp
public byte[] Render(float[] heightsMm, int fieldWidth, int fieldHeight,
                     ProjectionSettings, ProcessingSettings, RenderSettings,
                     IReadOnlyList<CamadaVisual> camadas)
```

**De 16 parâmetros para 7.** Os três blocos `if` nomeados viram um laço sobre camadas ordenadas.

**Por que isso e não mais que isso:**
- Resolve o acoplamento real com uma mudança concentrada em dois arquivos
- Não introduz nenhuma tecnologia nova, nenhuma dependência, nenhum conceito que um colaborador precise aprender
- Mantém `Parallel.For` e o laço por pixel — **sem custo de performance** se `CamadaVisual` for `readonly record struct` e o laço interno for sobre um array pequeno (3–5 camadas)
- A UI passa a montar `CmbSimulacao` a partir de uma lista de módulos, e os painéis de configuração a partir de `Parametros` — **o `enum Simulacao` desaparece**

**Custo estimado:** 2 a 3 dias, incluindo a migração dos três módulos existentes.

**HIPÓTESE.** O laço por pixel com camadas dinâmicas pode ser marginalmente mais lento que os `if` fixos por causa da indireção. Espero diferença abaixo de 5%, mas **isso precisa ser medido antes e depois** — é justamente o tipo de coisa que não se decide no papel.

## 9.3 Composição de fenômenos

**ESTADO ATUAL.** A composição já acontece, mas por **injeção manual de referências** em `SandboxEngine.StartSource:155-163`:

```csharp
Terremoto = new EarthquakeSimulation(...) { Solo = Agua.Solo };
Fogo = new FireSimulation(...) { Solo = Agua.Solo, Agua = Agua.Profundidade };
```

Funciona para três módulos. Para vinte, vira um construtor de 60 linhas com dependências implícitas e ordem de atualização não declarada.

**RECOMENDAÇÃO — para depois, não agora.** Um **quadro-negro de campos compartilhados** (`ContextoDaSimulacao`): um dicionário tipado de campos nomeados (`"agua.profundidade"`, `"solo.cobertura"`, `"terreno.inclinacao"`) que os módulos declaram consumir e produzir. A ordem de atualização vem da topologia das declarações.

**Não fazer isso na primeira rodada.** Com 3–5 módulos, a injeção manual é mais legível. Fazer quando chegar ao sétimo — e o sinal de que chegou a hora é `StartSource` passar de ~20 linhas de montagem.

---

# 10. CENÁRIOS AMBIENTAIS, BIOMAS E O PROBLEMA DOS NÚMEROS

## 10.1 O que existe

**ESTADO ATUAL.** `PropriedadesDoSolo` (`SoilMap.cs`) modela 12 coberturas com 5 propriedades numéricas cada, em `readonly record struct`, consultadas por tabela pré-calculada indexada pelo enum. Tecnicamente é uma boa estrutura: imutável, sem alocação, cache-friendly.

O comentário de calibração de performance (`:130-138`) — *"Medido: 10,87 ms com o switch, 10,52 ms com a tabela — cerca de 3%… Fica pela previsibilidade, não pelo ganho"* — merece nota: **é uma decisão de otimização tomada com medição e documentada honestamente, incluindo o fato de que o ganho foi menor que o esperado.** Isso é raro.

## 10.2 O problema

**ESTADO ATUAL.** Os números são inventados-para-a-aula (o código admite) e apresentados com uma casa decimal na interface (violação 1, seção 5.2). Não há metadado de origem, faixa, incerteza ou nível de confiança.

Acrescentar biomas (Cerrado, Mata Atlântica, Caatinga, Amazônia) **multiplica o problema**: seriam mais números com aparência de ciência, sobre um tema em que o professor tem menos condições de julgar plausibilidade do que sobre "asfalto não absorve água".

## 10.3 Recomendação: escala qualitativa como fonte de verdade

**RECOMENDAÇÃO.** Inverter a relação entre o número e a palavra. Hoje o número é a verdade e a palavra é decoração. Deveria ser o contrário.

```
Cobertura
├── Nome            : "Cerrado preservado"
├── Infiltracao     : Nivel.Alta          ← escala qualitativa de 5 níveis
├── Retencao        : Nivel.Alta
├── ResistenciaErosao : Nivel.MuitoAlta
├── Combustivel     : Nivel.Alta          ← Cerrado é adaptado ao fogo
├── Origem          : "Ordem de grandeza conforme literatura de hidrologia
│                      de bacias; valores relativos, calibrados para que o
│                      contraste apareça numa aula de 30 min."
├── Confianca       : Confianca.OrdemDeGrandeza
└── (interno) coeficientes derivados de Nivel, não digitados à mão
```

**Como funciona:**
- O autor da cobertura escolhe **níveis**, não números
- Uma tabela única converte `Nivel → coeficiente` para cada grandeza
- A UI exibe **"absorve muito"**, não "3,2 mm/s"
- Um botão *"como este número foi obtido?"* mostra `Origem` e `Confianca`
- Ajustar o realismo global vira mexer numa tabela de conversão, não em 12×5 constantes

**Vantagens:**
1. Elimina a falsa precisão na origem
2. Torna impossível a alguém acrescentar "Cerrado = 0,65" sem declarar de onde veio
3. Faz o professor conversar sobre **relações** ("o Cerrado absorve mais que a pastagem"), que é o objetivo pedagógico
4. Comparações relativas continuam funcionando — o que importa é a razão, e ela se preserva

**Desvantagem honesta:** perde-se a possibilidade de, um dia, alimentar valores reais medidos para um solo específico. **Mitigação:** permitir que uma cobertura declare valores absolutos *com* `Confianca.Medido` e citação — a exceção documentada, não a regra.

## 10.4 Biomas: cor não deve ser só cor

**ESTADO ATUAL.** A paleta é `private static readonly Stop[] Palette` em `TopographicRenderer` — 12 paradas hardcoded, hipsométrica clássica.

**RECOMENDAÇÃO.** Um tema de bioma deve trocar **três** coisas, não uma:
1. A rampa hipsométrica (identidade visual)
2. A **cobertura padrão** do `SoilMap` (Cerrado→cerrado, Mata Atlântica→mata densa, Caatinga→caatinga)
3. As **faixas de parâmetro plausíveis** do fenômeno (chuva típica do bioma, sazonalidade)

Se o tema só trocar cor, é enfeite. Se trocar os três, o mesmo relevo com a mesma chuva **produz resultados diferentes por bioma** — e aí o tema é conteúdo.

---

# 11. PERFORMANCE — GARGALOS REAIS

Só listo o que está comprovado no código. Não incluí nada especulativo.

### 🔴 P-1 — Clone de 614 KB por quadro direto na LOH

**SEVERIDADE:** Alta
**LOCAL:** `Depth/KinectV1Source.cs` · `KinectV1Source.Loop`; idêntico em `Depth/SimulatedDepthSource.cs` · `Loop`
**PROBLEMA:**
```csharp
FrameArrived?.Invoke(new RawDepthFrame {
    Data = (ushort[])managed.Clone(), ...
});
```
**EVIDÊNCIA:** `640 × 480 × 2 bytes = 614.400 bytes` por quadro. O limiar da Large Object Heap é 85.000 bytes — **todo quadro é uma alocação LOH**. A 30 fps: **18,4 MB/s**. A LOH não é compactada por padrão e sua coleta é Gen2.
**IMPACTO:** GC · jitter. Coletas Gen2 periódicas com pausas que aparecem como engasgo na projeção. **E a maior parte desses arrays é descartada sem uso**: `SandboxEngine.OnFrameArrived` faz `Volatile.Write` num slot único; se a UI thread estiver ocupada, o quadro anterior é sobrescrito e o clone foi puro desperdício.
**CORREÇÃO:** Pool de 2–3 buffers em rodízio (o clone existe hoje para garantir que a UI não leia um buffer sendo reescrito; um rodízio de 3 preserva essa garantia sem alocar). Alternativa mais simples: `ArrayPool<ushort>.Shared` com devolução após o processamento.
**RISCO:** Médio — mexe na única fronteira entre threads do sistema. Exige cuidado: o clone é **load-bearing para a segurança de thread**, não é gordura acidental.

### 🟠 P-2 — Delegate marshalado a cada chamada de vtable

**SEVERIDADE:** Média
**LOCAL:** `Depth/NuiNative.cs` · `NuiNative.VTableCall<T>`
**PROBLEMA:**
```csharp
private static T VTableCall<T>(IntPtr pInterface, int slot) where T : Delegate
{
    IntPtr vtable = Marshal.ReadIntPtr(pInterface);
    IntPtr fn = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
    return Marshal.GetDelegateForFunctionPointer<T>(fn);   // ← aloca
}
```
Chamado por `TextureLockRect` e `TextureUnlockRect` — **2× por quadro, 60×/s**.
**EVIDÊNCIA:** `GetDelegateForFunctionPointer` constrói um objeto delegate e um stub de interop a cada chamada.
**IMPACTO:** GC (Gen0) · CPU. Pequeno em valor absoluto, mas é desperdício puro no caminho mais quente do sistema.
**CORREÇÃO:** Cachear os delegates por ponteiro de vtable, ou — melhor — usar `delegate* unmanaged[Stdcall]<...>` (function pointers do C# 9), que o projeto já pode usar (`AllowUnsafeBlocks` está ligado).
**RISCO:** Baixo. Bem isolado, testável com o simulador.

### 🟠 P-3 — Processamento de profundidade inteiramente serial

**SEVERIDADE:** Média-alta
**LOCAL:** `Processing/DepthProcessor.cs` · `ProcessFrame` (etapa 1) e `BoxBlur`
**PROBLEMA:** O README declara "Renderização: CPU (Parallel.For)" — e o **renderizador** de fato usa. Mas o processamento não: a etapa 1 é um `for` serial sobre 307.200 pixels, e `BoxBlur` faz duas passadas seriais.
**EVIDÊNCIA:** Nenhum `Parallel.For` em `DepthProcessor.cs`.
**IMPACTO:** CPU · latência. Num processador de 8 núcleos, o pipeline usa 1 núcleo para essa fase.
**Agravante — cache locality:** a passada **vertical** do `BoxBlur` acessa `tmp[add * w + x]`, com passo de `w` floats (2.560 bytes) entre acessos consecutivos. Isso invalida a linha de cache a cada acesso. É o pior padrão de acesso possível para um array 2D.
**CORREÇÃO:** (a) `Parallel.For` na etapa 1 — trivial, as iterações são independentes por pixel. (b) `Parallel.For` por linhas na passada horizontal e por colunas na vertical. (c) Para a locality, transpor o buffer entre as passadas, ou processar a vertical em faixas de colunas que caibam em cache.
**RISCO:** Baixo para (a) e (b); médio para (c).

### 🟡 P-4 — Alocações por quadro nas simulações

**SEVERIDADE:** Baixa-média
**LOCAL:**
- `Simulation/WaterSimulation.cs` · `AplicarInfiltracao` e `AcumularErosao`: `object trava = new();` — 2 objetos/quadro
- `Simulation/FireSimulation.cs` · `Propagar`: `var novos = new List<int>();` — até 4/quadro, e a lista cresce
**IMPACTO:** GC Gen0. Pequeno, mas gratuito de eliminar.
**CORREÇÃO:** Travas em campos `readonly`; lista de novos focos como buffer reutilizável com contador.
**RISCO:** Baixo.

### 🟡 P-5 — Estatísticas seriais sobre 76.800 células, 3× por quadro

**SEVERIDADE:** Baixa-média
**LOCAL:** `Simulation/WaterSimulation.cs` · `CalcularEstatisticas`
**PROBLEMA:** Três varreduras seriais completas por quadro — uma para volume/alagamento, uma para saturação, e a consulta a `PropriedadesDoSolo.Rapido` dentro do laço.
**IMPACTO:** CPU. ~230 mil operações/quadro para produzir 5 números que a UI lê a **2 Hz** (`_uiTimer` de 500 ms).
**CORREÇÃO:** Calcular a cada N quadros, não a cada quadro. A UI não consome mais rápido que isso.
**RISCO:** Baixo. Cuidado apenas com `PicoAlagamentoPercent`, que precisa amostrar o suficiente para não perder o pico.

### 🟡 P-6 — Erosão computada e descartada

**SEVERIDADE:** Média (é 100% desperdício)
**LOCAL:** `Simulation/WaterSimulation.cs` · `AcumularErosao`
**PROBLEMA:** `Parallel.For` completo sobre 76.800 células, todo quadro, produzindo `_erosao[]` e `ErosaoTotal` que **ninguém lê**.
**CORREÇÃO:** Ou exibir (recomendado — erosão é conteúdo pedagógico de primeira linha) ou desligar por flag.
**RISCO:** Baixo.

### 🟢 P-7 — `BrushConverter` instanciado por linha do painel

**SEVERIDADE:** Baixa
**LOCAL:** `Views/ProjectionWindow.xaml.cs` · `Linha`
**PROBLEMA:** `(SolidColorBrush)new BrushConverter().ConvertFrom(cor)!` — instancia um conversor e faz parsing de string por linha, e `AtualizarDados` recria todos os `TextBlock` a 2 Hz.
**IMPACTO:** Desprezível a 2 Hz. Listado por completude.
**CORREÇÃO:** Brushes estáticos congelados (`Freeze()`).
**RISCO:** Nenhum.

## 11.1 O que NÃO é gargalo (verificado)

Para não desperdiçar esforço:

- **LINQ em hot path:** não há. O único LINQ é em `MainWindow.AtualizarComparacao`, disparado por interação humana.
- **Boxing:** não encontrei. `PropriedadesDoSolo` é `readonly record struct` acessado por tabela — sem boxing.
- **Locks no caminho de imagem:** `DepthProcessor._gate` só protege o início/fim da calibração. Os `lock(trava)` em `WaterSimulation` são de `localFinally` do `Parallel.For` — executam uma vez por partição, não por célula.
- **`Marshal.Copy`:** não usado; `CopyDepth` é `unsafe` com ponteiro direto, que é a escolha certa.
- **Divisões/sqrt/trig em laço por pixel:** o renderizador não usa nenhuma. `AmostrarBilinear` é aritmética simples. `MathF.Sqrt` aparece em `MoverAgua` (velocidade) e em `DespejarEm` — aceitável.
- **Realocação do buffer de saída:** `TopographicRenderer` só realoca `_buffer` se a ROI mudar. Correto.

---

# 12. CPU, SIMD OU GPU

| Caminho | Benefício provável | Complexidade | Risco em sala de aula | Impacto na distribuição | Veredito |
|---|---|---|---|---|---|
| **A — CPU otimizada** | P-1 a P-6 somados: estimo 30–50% de folga a mais no orçamento de quadro | Baixa | Nenhum | Nenhum | ✅ **Fazer primeiro** |
| **B — SIMD (`System.Numerics.Vector<T>`)** | `BoxBlur`, `AplicarChuva`, `DrenarSolo`, laços de blending: 2–4× nessas etapas | Média | Baixo — `Vector<T>` degrada graciosamente | Nenhum | 🟡 **Depois de A, e só onde o profiling apontar** |
| **C — GPU (shader HLSL / D3DImage)** | Grande para o solver de água | **Alta** | **Alto** | Passa a depender de driver, DirectX, hardware da escola | ❌ **Não agora** |

**RECOMENDAÇÃO.** Fazer A. Medir. Só então considerar B em pontos específicos.

**Contra a GPU, com razões concretas — não por conservadorismo:**

1. **O orçamento atual não está estourado.** O sistema roda a 20–29 fps com três simulações disponíveis. Não há evidência no código de que a CPU seja o limite.
2. **A máquina de destino é um notebook rodando Windows 10 numa escola.** Um driver de vídeo desatualizado numa sala de aula é uma aula perdida, e o professor não tem como diagnosticar.
3. **A distribuição hoje é um `.exe` único de 68 MB, sem instalador.** Essa simplicidade é um ativo do produto. `D3DImage` + shaders acrescenta superfície de falha que não existe hoje.
4. **O gargalo é arquitetural, não computacional.** Otimizar o solver de água não permite adicionar um único fenômeno novo. Refatorar as camadas de saída, sim.

**Quando reconsiderar a GPU:** se o profiling na máquina real mostrar `Render` + simulações consistentemente acima de ~25 ms com os módulos que a aula realmente usa. Aí a decisão tem base. Hoje não tem.

---

# 13. THREADING E LATÊNCIA — RESPOSTAS DIRETAS

### 13.1 Faz sentido rodar a engine a ~60 Hz se o Kinect entrega ~30 fps?

**ESTADO ATUAL — sim, e a decisão está correta.** `SandboxEngine` cria o timer com `Interval = 16ms` e o comentário justifica: *"Deixar o timer mais rapido que a fonte tira ate 16ms de latencia percebida."*

Isso está certo. Com um timer a 33 ms desalinhado da chegada do quadro, a latência média de espera é ~16 ms e o pior caso ~33 ms. A 16 ms, a média cai para ~8 ms. Numa caixa de areia — onde a pessoa mexe a mão e espera a cor mudar — isso é perceptível.

### 13.2 O mesmo quadro é processado mais de uma vez?

**ESTADO ATUAL — não.** `SandboxEngine.OnTick`:
```csharp
if (frame.FrameNumber == _lastRenderedFrameNumber) return;
_lastRenderedFrameNumber = frame.FrameNumber;
```
Metade dos ticks retorna imediatamente. Custo por tick vazio: uma leitura volátil e uma comparação. **Correto e barato.**

### 13.3 Existe trabalho desnecessário entre quadros?

**ESTADO ATUAL — sim, dois:**

1. **Do lado da captura:** os clones LOH de quadros que serão descartados (P-1). Se a UI thread demora, a captura continua alocando 614 KB a 30 Hz para nada.
2. **`ProjectionWindow._dadosTimer` e `MainWindow._uiTimer`** rodam a 2 Hz independentemente de haver simulação ativa. Custo baixo, mas `AtualizarDados` recria toda a árvore visual do painel.

### 13.4 Existe arquitetura melhor para latência?

**RECOMENDAÇÃO — sim, mas o ganho é modesto e o risco não é.**

A latência de ponta a ponta hoje é aproximadamente:
```
exposição do sensor (~33ms) + espera do evento + cópia + espera do tick (~8ms médio)
+ processamento + simulação + render + composição WPF (~1 quadro de tela)
```

O passo com maior ganho potencial seria **mover processamento e simulação para uma thread própria**, deixando a UI thread só com `WritePixels`. Isso:
- tira o risco de a UI do professor congelar quando a simulação pesar
- permite processar enquanto o próximo quadro chega
- **mas** introduz a necessidade de double-buffering do bitmap e sincronização real, onde hoje há um `Volatile.Write` e nada mais

**Não faria isso agora.** A arquitetura atual é simples o suficiente para caber na cabeça de uma pessoa, e a simplicidade tem valor num projeto mantido por poucos. Faria quando (13.5) se concretizar.

### 13.5 Como isso se comporta quando as simulações ficarem mais complexas?

**ESTADO ATUAL — mal, e o modo de falha é ruim.** Como tudo roda na UI thread, uma simulação pesada não degrada só a projeção: **congela a janela de controle do professor**, no meio da aula, sem mensagem.

Além disso: **as simulações só avançam quando chega quadro novo.** Se o sensor cair ou engasgar, `OnTick` retorna cedo e a água para de escoar. O `dt` acumulado é corretamente medido (`_simClock` só reinicia quando há processamento) e limitado a 100 ms — então não há explosão numérica —, mas o fenômeno **congela junto com o sensor**, o que é conceitualmente errado: a chuva não deveria depender de o Kinect estar entregando quadros.

**RECOMENDAÇÃO.** Desacoplar o avanço da simulação da chegada de quadros: a simulação avança pelo relógio; o terreno é atualizado quando houver quadro novo. Mudança pequena em `OnTick`, ganho conceitual grande.

### 13.6 Race conditions, deadlocks, tearing

**ESTADO ATUAL — não encontrei nenhum.** A análise:

- `_latestFrame`: `Volatile.Write` na captura / `Volatile.Read` na UI. Publicação segura de uma referência a objeto imutável cujo array **nunca é reescrito** (é um clone fresco). Correto.
- `Faulted` é disparado da thread de captura e `SandboxEngine.OnFaulted` marshala com `Application.Current?.Dispatcher.Invoke`. Correto.
- `DepthProcessor` é tocado apenas pela UI thread (`ProcessFrame`, `Import`, `Export`, `BeginBaseCalibration` — todos a partir de `SandboxEngine` na UI thread). O `lock(_gate)` é, na prática, redundante hoje.

**HIPÓTESE.** Se o processamento for movido para outra thread (13.4), `_basePlaneMm` é **reatribuído** em `Import` (`_basePlaneMm = (float[])dados.BasePlaneMm.Clone()`) enquanto `ProcessFrame` o lê **fora do lock**. Hoje é seguro por serem a mesma thread; deixaria de ser. Registrar como armadilha antes de qualquer mudança de threading.

---

# 14. KINECT V1 — AUDITORIA DO INTEROP

## 14.1 O que está certo, e por quê

**ESTADO ATUAL.** As três armadilhas conhecidas estão corretamente tratadas e — o que é mais valioso — **documentadas com o sintoma**, não só com a solução:

1. **Ponteiro vs. struct.** `NuiImageStreamGetNextFrame` é declarado `out IntPtr` e depois desreferenciado com `Marshal.PtrToStructure<NuiImageFrame>`. Correto para a API flat. O comentário registra que declarar `out NuiImageFrame` causa **0xC0000374 (heap corruption)** — e que o sintoma não aponta para a causa.
2. **Shift de 3 bits.** `CopyDepth` faz `src[i] >> NUI_IMAGE_PLAYER_INDEX_SHIFT`. O comentário registra a evidência empírica: todos os valores múltiplos de 8, máximo exatamente `0x1FFF<<3`.
3. **Near mode = `0x00020000`.** A constante está certa, `TOO_FAR_IS_NONZERO` está declarada separadamente para não haver confusão, e o comentário registra a medição (6,9% vs 66,4% de cobertura).

O `layout` da `NuiImageFrame` está correto para x64 (o `LayoutKind.Sequential` com `long`+`uint`+`int`+`int` dá o padding de 4 bytes antes do `IntPtr`, batendo com o offset 24 documentado no diário de bordo).

`DescribeHResult` traduz os HRESULTs NUI para mensagens acionáveis em português — *"Sensor sem alimentação. O adaptador de energia externo é obrigatório."* Para um produto usado por professores, isso vale mais que a maioria das otimizações deste relatório.

## 14.2 Assumptions perigosas

| # | Assumption | Local | Risco |
|---|---|---|---|
| A1 | **`NuiInitialize`/`NuiShutdown` são globais do processo, tratados como se fossem por instância** | `KinectV1Source.Start` / `Cleanup` | Se duas `KinectV1Source` existirem simultaneamente, a segunda `NuiShutdown` derruba a primeira. Hoje `SandboxEngine.StopSource` garante uma só — mas nada no tipo impede |
| A2 | **Resolução fixa em 640×480 hardcoded** | `Width => 640; Height => 480` | Mudar a resolução do stream exige mexer em duas propriedades e em toda a cadeia |
| A3 | **`NuiImageStreamSetImageFrameFlags` não é verificado** | `Start`, near mode | Documentado como intencional (o retorno não prova nada), mas **não há verificação empírica no código** — nada mede se apareceram leituras abaixo de 800 mm |
| A4 | **`continue` silencioso em falhas de quadro** | `Loop`: `if (hr != S_OK \|\| framePtr == IntPtr.Zero) continue;` e `if (lockHr != S_OK …) continue;` | Falha persistente de LockRect vira **tela congelada sem nenhuma mensagem**. O sistema aparenta funcionar |
| A5 | **`WaitForSingleObject` com `WAIT_TIMEOUT` → `continue` indefinido** | `Loop` | Sensor que para de entregar quadros nunca dispara `Faulted`. **A reconexão automática não é acionada.** Este é o modo de falha mais provável em sala |
| A6 | **`_frameEvent` é `ManualReset=true` e nunca é resetado** | `Start`: `CreateEvent(IntPtr.Zero, true, false, null)` | Com manual-reset e sem `ResetEvent`, após o primeiro sinal o `WaitForSingleObject` retorna imediatamente sempre, e o laço passa a girar em busy-loop limitado apenas pelo custo de `GetNextFrame`. **HIPÓTESE:** o SDK pode resetar o evento internamente em `NuiImageStreamGetNextFrame`; a documentação da API flat não é explícita. Precisa ser verificado com o sensor ligado — ver seção 18 |
| A7 | **Nenhum `Release()` na textura** | `Loop` | `NuiImageStreamReleaseFrame` é chamado no `finally`, o que é o contrato correto. `TextureRelease` existe e não é usado. Provavelmente certo, mas a assimetria merece um comentário |

**A5 é o mais importante.** É o cenário "o cabo USB encostou" — e o código trata desconexão dura (que dispara exceção → `Faulted` → reconexão) mas **não trata o sensor que simplesmente para de falar**. A correção é pequena: contar timeouts consecutivos e disparar `Faulted` acima de N.

## 14.3 Vazamentos

**ESTADO ATUAL.** `Cleanup()` fecha `_frameEvent` com `CloseHandle` e zera `_streamHandle`. `Stop()` cancela o token e faz `Join(1500)`. `Dispose()` chama `Stop()`. **O caminho feliz não vaza.**

**HIPÓTESE.** No caminho de reconexão, `StartSource` é chamado repetidamente criando novas `KinectV1Source`, cada uma fazendo `NuiInitialize`/`NuiShutdown`. Ciclos repetidos de init/shutdown do `Kinect10.dll` são um caminho pouco exercitado do SDK de 2013. Se houver vazamento de handle nativo, ele só aparece após muitas reconexões. **Não confirmável pelo código** — exige o teste de longa duração descrito na seção 18.

---

# 15. ABSTRAÇÃO DE HARDWARE

**ESTADO ATUAL — a abstração é boa e está quase limpa.**

`IDepthSource` expõe `Name`, `Width`, `Height`, `IsRunning`, `FrameArrived`, `Faulted`, `Start`, `Stop`, `Dispose`. `RawDepthFrame` carrega `ushort[] Data`, dimensões e `FrameNumber`. Nada disso é específico do Kinect — é o contrato mínimo de qualquer sensor de profundidade.

`SimulatedDepthSource` implementa a mesma interface com relevo sintético (duas gaussianas que "respiram" + uma bacia), ruído de ~2 mm e ~0,5% de pixels inválidos — **calibrado para parecer com o sensor real**. `ReliefScale = 0` produz superfície plana, o que permite ensaiar a calibração do plano-base sem hardware. **Essa classe é o ativo de testabilidade mais valioso do projeto.**

## 15.1 Vazamentos de Kinect para camadas superiores

**ESTADO ATUAL.** Encontrei três, todos na UI — nenhum nas simulações:

| # | Vazamento | Local |
|---|---|---|
| 1 | `MainWindow.Ligar` chama `KinectV1Source.TryProbe` diretamente | `MainWindow.xaml.cs:88` |
| 2 | `MainWindow.DetectSensor` idem | `:142` |
| 3 | `MainWindow.OnStartKinect` constrói `new KinectV1Source(near, tilt)` | `:156` |
| 4 | `ChkNearMode` — conceito exclusivo do Kinect v1 — é controle de primeira classe na UI | `MainWindow.xaml` |

**As simulações não têm nenhuma dependência de Kinect.** `WaterSimulation.Atualizar(float[] terrenoMm, int larguraTerreno, int alturaTerreno, float dt)` não sabe de onde vem o terreno. `DepthProcessor` depende de `RawDepthFrame`, não de `KinectV1Source`.

**Conclusão:** trocar o sensor exigiria mexer **só na `MainWindow`**. Adicionar gravação/replay de aula exigiria uma nova `IDepthSource` — e nada mais. **A abstração cumpre o que promete.**

**RECOMENDAÇÃO (baixa prioridade).** Um pequeno registro de fontes (`nome → factory → disponível?`) removeria os quatro vazamentos e faria o `ChkNearMode` virar uma opção declarada pela fonte, não um checkbox fixo. Vale fazer junto com o replay de aula, não antes.

---

# 16. BUGS E RISCOS

| # | Sev. | Local | Cenário | Efeito | Correção | Risco da correção |
|---|---|---|---|---|---|---|
| B-1 | **Crítica (funcional)** | `FireSimulation.Atear` | Sempre | Módulo inteiro inacessível | Ligar na UI | Baixo |
| B-2 | **Alta** | `MainWindow.Registrar` | Alunos mexem na areia entre execuções | **Comparação cientificamente falsa** apresentada como conclusão da aula | Incluir assinatura do relevo na chave; avisar quando divergir | Baixo |
| B-3 | **Alta** | `WaterSimulation` construtor | Sempre | `VolumeLitros`/`InfiltradoLitros` com erro sistemático (~25% na área) | Configurar dimensões reais da caixa; derivar da calibração | Médio — muda números já vistos em aula |
| B-4 | **Alta** | `KinectV1Source.Loop` (A5) | Sensor para de entregar sem desconectar | Tela congelada, sem `Faulted`, **sem reconexão** | Contar timeouts consecutivos → `Faulted` | Baixo |
| B-5 | **Alta** | `FireSimulation.TentarAcender` | Água aparece depois do fogo começar | `Estado.NaoQueima` é terminal; canal cavado não barra o fogo | Reavaliar por passo em vez de gravar estado | Baixo |
| B-6 | Média | `WaterSimulation.EscoadoLitros` | Sempre | Métrica sempre 0; se exibida, mente | Calcular em `MoverAgua` ou remover | Baixo |
| B-7 | Média | `ProjectionSettings.Roi*` | Sempre | **Sem UI.** A ROI padrão é o quadro inteiro do sensor, que inclui chão, bordas da caixa e o operador. Só editável à mão em `config.json` | Definir ROI na calibração ou por interface | Médio |
| B-8 | Média | `MainWindow` `Closed` | Professor fecha sem salvar | Ajustes de sliders e alinhamento da projeção perdidos | Salvar `AppConfig` no fechamento | Baixo |
| B-9 | Média | `SandboxEngine.OnTick` | Sensor engasga | Simulações congelam junto | Desacoplar avanço do relógio (13.5) | Médio |
| B-10 | Baixa | `DepthProcessor.MinCalibrationSamples` | — | Comentário diz *"Fracao minima dos quadros"*, o valor é contagem absoluta (5). Se alguém chamar `BeginBaseCalibration(6)`, quase nada calibra | Corrigir comentário ou tornar fração | Baixo |
| B-11 | Baixa | `ProjectionWindow.OnKeyDown` | Tecla `D` | Alterna o painel de dados; **não está documentada** na tabela de atalhos do README nem do manual | Documentar | Nenhum |
| B-12 | Baixa | `NuiNative.TextureRelease`, `NuiCameraElevationGetAngle` | — | Código morto | Remover | Nenhum |

**Sobre tratamento de exceções:** `KinectV1Source` tem 5 blocos `catch`, `CalibrationStore` 3, `MainWindow` 4, `SandboxEngine` 2. `AppConfig.Load` engole exceção e volta aos padrões — **decisão correta e comentada**: *"Config corrompida nunca deve impedir o app de abrir - numa sala de aula isso significaria aula perdida."*

`CalibrationStore.Save` escreve em `.tmp` e faz `File.Move(overwrite: true)` — gravação atômica, protege contra queda de energia no meio. **Bem feito.**

---

# 17. TESTABILIDADE

**ESTADO ATUAL.** **Não existe projeto de testes.** Nenhum arquivo `*Test*`. O `.sln` tem um único projeto.

**ESTADO ATUAL — mas a testabilidade é alta.** `SimulatedDepthSource` permite exercitar a pipeline inteira sem hardware, e `DepthProcessor`, `WaterSimulation`, `FireSimulation`, `SoilMap` e `TopographicRenderer` são todas classes puras sem dependência de WPF.

**O obstáculo real ao determinismo:**
1. `dt` vem de `Stopwatch` (`SandboxEngine._simClock`) — mas `Atualizar(…, float dt)` **recebe `dt` como parâmetro**, então em teste basta passar valores fixos. ✅
2. `FireSimulation` usa `new Random()` quando `semente == 0`, e o construtor é sempre chamado sem semente. ❌ — mas o parâmetro `semente` já existe; basta passá-lo.
3. `SimulatedDepthSource` usa `new Random(1234)` — semente fixa. ✅

**Ou seja: o determinismo está a duas linhas de distância.**

## 17.1 Testes concretos propostos

### `DepthProcessor`

| Cenário | Input | Resultado esperado |
|---|---|---|
| Calibração em superfície plana | 60 quadros de 900 mm uniformes | `CoveragePercent == 100`, `AverageDistanceMm ≈ 900`, `IsCalibrated` |
| Pixel intermitente rejeitado | 60 quadros; um pixel válido em 4 deles | Esse pixel tem `BaseValid == false`; altura sai 0 |
| Altura é a diferença | base 900, quadro a 850 | altura ≈ +50 mm |
| Clamp da faixa | base 900, quadro a 500 (Δ=400), `MaxHeightMm=120` | saída = 120, não 400 |
| Preenchimento de buraco | pixel válido, depois 0 | mantém o último valor bom, não cai a zero |
| Pixel nunca válido | sempre 0 desde o início | saída 0, sem NaN |
| α adaptativo — repouso | ruído de ±3 mm em torno de 850 | variação de saída < 1 mm após 30 quadros |
| α adaptativo — salto | mudança de 40 mm num quadro | saída atinge ≥60% do novo valor em 1 quadro |
| Box blur preserva média | campo constante de 50 mm | saída constante de 50 mm em todo pixel, inclusive bordas |
| Blur radius 0 | — | saída idêntica a `_smoothed` |
| Dimensão incompatível | quadro 320×240 num processor 640×480 | lança `ArgumentException` |

### `CalibrationStore`

| Cenário | Input | Resultado esperado |
|---|---|---|
| Round-trip | `CalibrationData` com padrão conhecido | `Load` devolve dados bit-idênticos |
| Resolução diferente | salvo 640×480, carregado como 320×240 | devolve `null`, não lança |
| Assinatura inválida | arquivo com bytes aleatórios | devolve `null` |
| Truncado | arquivo cortado ao meio | devolve `null` |
| Atomicidade | — | após `Save`, não resta `.tmp` |
| Empacotamento de bits | 307.200 bools alternados | tamanho de arquivo confere; round-trip exato |

### `WaterSimulation`

| Cenário | Input | Resultado esperado |
|---|---|---|
| **Conservação de massa** | terreno plano, `BordasEscoam=false`, infiltração 0, chuva 10 mm/s por 1 s | `VolumeLitros` final = volume esperado ±1% |
| Água desce | rampa constante, água no topo | após N passos, massa concentrada na parte baixa |
| Bacia retém | depressão central, `BordasEscoam=false` | volume estável após escoamento; não vaza |
| Bordas escoam | plano, `BordasEscoam=true` | volume → 0 |
| Solo satura | chuva contínua sobre `SoloArgiloso` | `SaturacaoMediaPercent` → 100 e a infiltração cessa |
| Mata absorve mais que asfalto | mesmo relevo, mesma chuva | `PicoAlagamentoPercent(Mata) < PicoAlagamentoPercent(Impermeavel)` |
| **Não gera água** | sem chuva, água inicial conhecida | volume nunca aumenta em 1.000 passos |
| Estabilidade CFL | dt=0.1 s, coluna de água de 200 mm | sem `NaN`, sem `Infinity`, sem oscilação divergente |
| Pré-saturação | `PreSaturar(0.75)` | `SaturacaoMediaPercent ≈ 75` |

### `FireSimulation`

| Cenário | Input | Resultado esperado |
|---|---|---|
| Determinismo | semente 42, terreno e solo fixos | duas execuções → `AreaQueimadaPercent` idêntica |
| Não pega em rocha | `SoilMap` todo `Rocha` | `Atear()` devolve `false` |
| Fogo termina | mata, sem água | `EmAndamento` vira false em tempo finito |
| Cicatriz gravada | após terminar | células queimadas viram `TipoDeSolo.Queimado` |
| **Água barra** | faixa de água atravessando o mapa | fogo não passa para o outro lado |
| **Regressão do B-5** | água **aparece depois** do início do fogo, na frente da chama | fogo é barrado — hoje **falha** |
| Vento enviesa | vento forte para leste | centroide da área queimada a leste do foco |
| Encosta acelera | rampa, foco na base | frente sobe mais que desce em tempo igual |

### `TopographicRenderer`

| Cenário | Input | Resultado esperado |
|---|---|---|
| Determinismo | mesmo campo, duas chamadas | buffers byte-idênticos |
| Extremos da rampa | campo em `MinHeightMm` / `MaxHeightMm` | cor = primeira / última parada da paleta |
| Alfa sempre opaco | qualquer entrada | todo byte de alfa = 255 |
| Recorte por ROI | ROI 100×100 | `Width==100`, `Height==100`, stride 400 |
| Curvas desligadas | `ContourIntervalMm = 0` | nenhum pixel escurecido por curva |
| Overlay de água preserva relevo | água rasa uniforme | cor resultante ≠ cor pura de água (blending, não substituição) |
| **Regressão visual** | campo sintético canônico | hash do buffer comparado a um baseline versionado |

### `SoilMap`

| Cenário | Input | Resultado esperado |
|---|---|---|
| `Preencher` | tipo X | 100% de `Composicao()` é X |
| `PintarPorAltitude` acima | terreno em rampa, limiar no meio | ~50% de cada tipo |
| `Pintar` círculo | u=0.5, v=0.5, raio 0.1 | área pintada ≈ π·r²·W² ±5% |
| Enum estável | — | valores numéricos de `TipoDeSolo` não mudaram (protege calibrações salvas) |

### Performance (como teste de regressão)

| Cenário | Métrica | Critério |
|---|---|---|
| `ProcessFrame` 640×480, blur 3 | ms/quadro | registrar baseline; falhar se piorar >20% |
| `Render` 640×480 com 3 camadas | ms/quadro | idem |
| `WaterSimulation.Atualizar` dt=33 ms | ms/quadro | idem |
| Pipeline completo, 1.000 quadros | bytes alocados | após corrigir P-1, deve cair de ~600 MB para <10 MB |

**RECOMENDAÇÃO.** Começar com **conservação de massa da água** e **determinismo do fogo**. São os dois que protegem a credibilidade científica do produto, e ambos são triviais de escrever.

---

# 18. DECISÕES QUE EXIGEM PROFILING OU TESTE FÍSICO

Nada nesta seção pode ser decidido lendo código.

| # | Questão | Como medir | Decisão que depende disso |
|---|---|---|---|
| 1 | **O `_frameEvent` manual-reset causa busy-loop?** (A6) | Contador de iterações do `Loop` por segundo, com o sensor ligado. Se ≫30, há busy-loop | Se sim: `ResetEvent` ou evento auto-reset |
| 2 | **Onde vai o orçamento de 33 ms?** | `Stopwatch` por etapa (`ProcessFrame`, cada `Atualizar`, `Render`), percentil 95 em 1.000 quadros | Prioriza P-1…P-6; decide se SIMD/GPU entram |
| 3 | **Quanto o GC pausa?** | `GC.CollectionCount(2)` e `GCSettings`, sessão de 30 min | Confirma a severidade de P-1 |
| 4 | **Latência mão→projeção percebida** | Filmar a 120 fps a mão tocando a areia e a projeção mudando; contar quadros | Se >150 ms, justifica thread separada (13.4) |
| 5 | **Near mode está realmente ativo?** | Histograma de profundidade: existem leituras <800 mm? | Valida A3 empiricamente, como o próprio código recomenda |
| 6 | **Cobertura sobre areia real** | `CoveragePercent` após calibrar com a areia da caixa | Define se a suavização precisa de reajuste |
| 7 | **Ruído sobre areia real** | Desvio-padrão por pixel em 300 quadros com areia parada | Calibra `SmoothingAlpha`, `JumpThresholdMm`, `SpatialBlurRadius` |
| 8 | **Sombra de IV das mãos** | Gravar a cobertura enquanto uma mão molda | Decide se o preenchimento de buracos precisa de estratégia temporal melhor |
| 9 | **Legibilidade das cores na areia** | Fotografar a projeção sobre areia real; medir contraste entre faixas | Decide as paletas de bioma — areia clara lava as cores claras |
| 10 | **Amplitude real do relevo** | Histograma de altura com a caixa em uso por uma turma | Define `MaxHeightMm` e os limiares de alagamento — hoje calibrados em terreno sintético |
| 11 | **Dimensões reais cobertas pelo sensor** | Colocar marcadores nos cantos da caixa; ler as coordenadas no mapa | **Corrige B-3 e B-7** — é o pré-requisito de qualquer número em litros |
| 12 | **Estabilidade em sessão longa** | Rodar 4 h com simulações ativas; monitorar memória e handles | Confirma ou descarta o vazamento hipotético de reconexão (14.3) |
| 13 | **Reconexão real** | Desconectar o USB 20× durante uma sessão | Valida o caminho de reconexão e o risco A1 |

**O item 11 é pré-requisito de honestidade científica**, não de performance. Enquanto ele não for feito, qualquer valor absoluto (litros, mm) projetado na parede é decorativo.

---

# 19. DÍVIDA TÉCNICA CLASSIFICADA

## 🔴 CRÍTICA — resolver antes de crescer

| Item | Por quê |
|---|---|
| `Render()` com 16 parâmetros e blocos nomeados | É o gargalo de extensibilidade. Cada fenômeno novo piora |
| `ISimulationModule` sem contrato de saída | A abstração existe e não abstrai nada |
| `enum Simulacao` na UI | Explosão de modos já começou |
| Capacidade morta não conectada | Trabalho já pago que não rende |
| `larguraCaixaMm` hardcoded (B-3) | Números falsos projetados como medição |
| `Resumo` com falsa precisão (§5.2) | Viola o princípio central declarado |

## 🟠 IMPORTANTE — resolver logo

| Item | Por quê |
|---|---|
| Clone LOH por quadro (P-1) | 18 MB/s de GC no caminho quente |
| Sem log em arquivo | Problema em sala vira relato sem evidência |
| `DepthProcessor` serial (P-3) | Deixa 7 núcleos ociosos |
| Timeout do sensor não vira `Faulted` (B-4) | O modo de falha mais provável em sala não é tratado |
| Comparação sem assinatura de relevo (B-2) | Produz conclusão errada com aparência de resultado |
| Sem testes | Toda mudança em física é uma aposta |
| ROI sem interface (B-7) | O mapa cobre coisas que não são a caixa |

## 🟡 ACEITÁVEL — conviver por enquanto

| Item | Por quê aceitar |
|---|---|
| Tudo na UI thread | Simples, funciona no volume atual, e mudar traz risco real |
| Três `ReamostrarTerreno` duplicados | ~15 linhas cada; resolvem sozinhos com `TerrainField` |
| `BrushConverter` a 2 Hz (P-7) | Irrelevante |
| Delegate marshalado (P-2) | Pequeno; corrigir de carona |
| Injeção manual de dependências entre módulos | Mais legível que um quadro-negro, até ~6 módulos |

## ⚪ NÃO MEXER AGORA

| Item | Por quê |
|---|---|
| **P/Invoke do NUI** | Funciona, é o resultado de depuração cara, e está documentado com os sintomas. Mexer sem sensor na mão é irresponsável |
| **`CalibrationStore`** (formato binário) | Bem projetado, atômico, com validação de resolução |
| **Modelo de tubos virtuais da água** | Fisicamente correto, estável, bem calibrado |
| **`SimulatedDepthSource`** | O ativo de testabilidade; mexer só para adicionar cenários |
| **`AppConfig` com fallback silencioso** | A decisão certa para sala de aula |
| **Estilo de comentários** | É o que dá a esta base sua maior qualidade. Preservar como norma |
| **Distribuição em `.exe` único** | Simplicidade é feature |

---

# 20. ARQUITETURA-ALVO

## 20.1 O que fazer com o quê

| Ação | Itens |
|---|---|
| **PRESERVAR** | `Depth/*` inteiro · `DepthProcessor` (paralelizando por dentro) · `CalibrationStore` · `AppConfig` · `SoilMap`/`PropriedadesDoSolo` (acrescentando metadados) · física de `WaterSimulation` e `FireSimulation` · `ProjectionWindow` |
| **EXTRAIR** | `TerrainField` de `SandboxEngine._heights` · `CamadaVisual` dos blocos nomeados do renderer · `Parametro`/`Metrica` das propriedades públicas dos módulos · registro de fontes de `MainWindow` |
| **DESACOPLAR** | Renderer ↔ módulos (via camadas) · UI ↔ módulos (via catálogo) · avanço da simulação ↔ chegada de quadro |
| **CRIAR** | `ICatalogoDeModulos` · `Experimento` (record) · `Atividade` (declarativa) · projeto de testes · logging em arquivo |
| **DELETAR** | `EarthquakeSimulation` (decisão do produto) · `NuiNative.TextureRelease` · `NuiCameraElevationGetAngle` · `EscoadoLitros` (ou implementar) |
| **NÃO MEXER** | ver §19 ⚪ |

## 20.2 Diagrama-alvo

```
┌── CAPTURA ─────────────────────────────────────────────────────────────┐
│  IDepthSource ── KinectV1Source · SimulatedDepthSource · GravacaoSource │
│       │           (registro de fontes; near mode declarado pela fonte)  │
└───────┼────────────────────────────────────────────────────────────────┘
        ▼ RawDepthFrame  (buffers em pool — sem alocação LOH por quadro)
┌── PROCESSAMENTO ───────────────────────────────────────────────────────┐
│  DepthProcessor        buracos · α adaptativo · blur   [PARALELIZADO]   │
│  CalibrationStore      plano-base por pixel, persistido                 │
└───────┼────────────────────────────────────────────────────────────────┘
        ▼
┌── TERRENO  ◄── NOVO ───────────────────────────────────────────────────┐
│  TerrainField                                                           │
│   ├── Alturas (mm)                    todo quadro                       │
│   ├── EmMeiaResolucao                 todo quadro — calculado UMA vez    │
│   ├── Inclinacao / Aspecto            cache, invalidado por mudança      │
│   └── Depressoes · Acumulacao · Bacias  cache caro, sob demanda          │
└───────┼────────────────────────────────────────────────────────────────┘
        │
        ├──────────────┬──────────────────────────────┐
        ▼              ▼                              ▼
┌── AMBIENTE ──┐  ┌── FENÔMENOS ──────────────┐  ┌── EXPERIMENTO ◄── NOVO ┐
│  SoilMap     │  │  ISimulationModule         │  │  Experimento (record)   │
│  Cobertura   │◄─┤   Atualizar(terreno, dt)   │  │   ├ AssinaturaRelevo    │
│   ├ Nivel    │  │   Camadas    ──────────┐   │  │   ├ Cobertura (%)       │
│   ├ Origem   │  │   Parametros           │   │  │   ├ Fenômeno+Params     │
│   └ Confianca│  │   Metricas ────────┐   │   │  │   └ Metricas[]          │
│  (qualitativo│  │                    │   │   │  │  Comparador             │
│   → coefs)   │  │  Agua · Fogo · …   │   │   │  │   avisa quando o relevo │
└──────────────┘  └────────────────────┼───┼───┘  │   mudou entre execuções │
                                       │   │      └─────────────────────────┘
        ┌──────────────────────────────┘   │
        ▼                                  ▼
┌── RENDERIZAÇÃO ────────────────────┐  ┌── APRESENTAÇÃO ────────────────┐
│  TopographicRenderer               │  │  Metrica carrega classificação: │
│   Render(terreno, cfg, camadas[])  │  │   MEDIÇÃO · DERIVAÇÃO ·         │
│   ├ TemaDeBioma (rampa + padrão)   │  │   MODELO · EFEITO VISUAL        │
│   └ laço sobre camadas ordenadas   │  │  A UI exibe o rótulo junto      │
│     (7 parâmetros, não 16)         │  │  ao número. Sempre.             │
└───────┼────────────────────────────┘  └────────────────────────────────┘
        ▼
   WriteableBitmap ──► MainWindow.Preview
                  └──► ProjectionWindow ──► [ PROJETOR ]

┌── ATIVIDADES  ◄── NOVO (declarativo, JSON) ────────────────────────────┐
│  atividades/desmatamento-e-escoamento.json                             │
│   pergunta · configuração A · configuração B · o que observar          │
│   Validado por esquema no carregamento. Erro = atividade ignorada      │
│   com aviso, nunca crash.                                              │
└────────────────────────────────────────────────────────────────────────┘
```

## 20.3 Atividades declarativas — avaliação

**RECOMENDAÇÃO — sim, mas depois da fundação, e em JSON.**

| Aspecto | Avaliação |
|---|---|
| **Formato** | JSON. `System.Text.Json` já está no runtime, o projeto já o usa em `AppConfig`, zero dependências novas. YAML exigiria pacote |
| **Vantagem** | Professor cria atividade sem recompilar; atividades viram material compartilhável; a camada pedagógica deixa de ser código |
| **Risco 1** | JSON malformado quebrando a aula → **mitigação: validar no carregamento, ignorar a atividade inválida com aviso, nunca derrubar o app** (mesma política já adotada em `AppConfig.Load`) |
| **Risco 2** | Referência a fenômeno/cobertura inexistente → **mitigação: validar contra o catálogo em tempo de carga, não de execução** |
| **Risco 3** | Professor típico não edita JSON → **mitigação: JSON é o formato de armazenamento, não a interface. Um editor dentro do app vem depois** |
| **Versionamento** | Campo `versao` obrigatório; migração explícita. O `CalibrationStore` já usa esse padrão (`Assinatura` + `Versao`) |
| **i18n** | Textos como objeto por idioma (`{"pt-BR": "...", "en": "..."}`), com fallback para pt-BR. Barato agora, caríssimo depois |

**Quando fazer:** depois do catálogo de módulos e do `Experimento`. Uma atividade declarativa que referencia fenômenos hardcoded não resolve nada.

---

# 21. O QUE NÃO DEVEMOS FAZER

Seção de proteção. Cada item é uma tentação real neste projeto.

| ❌ Não fazer | Por quê |
|---|---|
| **ECS (Entity-Component-System)** | O domínio são poucos sistemas operando sobre os **mesmos grids**, não milhares de entidades heterogêneas. ECS resolveria um problema que este projeto não tem, e custaria legibilidade num código mantido por poucas pessoas |
| **Mover a água para GPU agora** | Não há evidência de que a CPU seja o limite. Acrescenta dependência de driver numa máquina de escola. **Reconsiderar só com o profiling da seção 18.2 na mão** |
| **Plugins com `AssemblyLoadContext`** | Segurança, versionamento e depuração em sala. O ganho real — "adicionar experiências sem recompilar" — é entregue por **atividades declarativas**, que são 10× mais simples |
| **Injeção de dependência com container** | Um `SandboxEngine` que monta o grafo à mão em 15 linhas é mais legível que um container. Não é um ERP |
| **MVVM completo com bindings** | A `MainWindow` tem 711 linhas de code-behind e é direta de ler. Converter para MVVM é refatoração estética — exatamente o que o pedido pede para evitar |
| **Reescrever o P/Invoke com uma biblioteca** | Não existe wrapper mantido do Kinect v1 para .NET 8. O código atual é o resultado de depuração cara e está documentado |
| **Async/await na pipeline de quadros** | O modelo atual (thread dedicada + slot volátil) é mais simples e mais previsível que `Channel`/`IAsyncEnumerable` para um produtor-consumidor de slot único |
| **Abstrair `IRenderer` antes de existir um segundo renderer** | Abstração especulativa. Existe **um** renderizador e não há segundo à vista |
| **Otimizar antes de medir** | Os gargalos da seção 11 estão comprovados por leitura; ainda assim, a **ordem** de ataque deve vir do profiling |
| **Suportar outros sensores agora** | `IDepthSource` já garante que será possível. Fazer agora é resolver um problema que não existe |
| **Banco de dados para experimentos** | Arquivos JSON numa pasta. Uma turma gera dezenas de registros, não milhões |
| **Perseguir 60 fps na projeção** | O sensor entrega 30. Otimizar além disso é otimizar o que não é visível |

---

# 22. ROADMAP PRIORIZADO

## P0 — ESTABILIDADE

### P0.1 — Log em arquivo
- **Problema:** zero logging no repositório. Um problema em sala vira relato sem evidência.
- **Por que importa:** a caixa física acabou de ficar pronta; as aulas começam. Sem log, todo diagnóstico é adivinhação.
- **Arquivos:** novo `Diagnostico/Log.cs`; pontos de chamada em `SandboxEngine`, `KinectV1Source`, `MainWindow`.
- **Alteração:** log rotativo em arquivo texto, com timestamp, versão, fonte, cobertura, fps, exceções. Sem framework.
- **Complexidade:** baixa · **Risco:** baixo · **Dependências:** nenhuma
- **Pronto quando:** uma sessão completa produz um arquivo que permite reconstruir o que aconteceu.

### P0.2 — Timeout do sensor vira `Faulted` (B-4)
- **Problema:** `WaitForSingleObject` retornando `WAIT_TIMEOUT` faz `continue` indefinidamente; a reconexão automática nunca dispara.
- **Por que importa:** é o modo de falha mais provável em sala, e hoje se manifesta como tela congelada sem mensagem.
- **Arquivos:** `Depth/KinectV1Source.cs` · `Loop`
- **Alteração:** contador de timeouts consecutivos; acima de ~15 (≈3 s), disparar `Faulted`.
- **Complexidade:** baixa · **Risco:** baixo
- **Pronto quando:** desconectar o sensor durante uma sessão leva a "Reconectando…" em até 5 s, em 20 tentativas de 20.

### P0.3 — Salvar configuração ao fechar (B-8)
- **Arquivos:** `Views/MainWindow.xaml.cs` · handler `Closed`
- **Complexidade:** baixa · **Risco:** baixo
- **Pronto quando:** ajustar sliders, alinhar a projeção, fechar e reabrir preserva tudo.

### P0.4 — Eliminar a alocação LOH por quadro (P-1)
- **Arquivos:** `Depth/KinectV1Source.cs`, `Depth/SimulatedDepthSource.cs`, `SandboxEngine.OnFrameArrived`
- **Alteração:** rodízio de 3 buffers.
- **Complexidade:** média · **Risco:** médio — **o clone é load-bearing para a segurança entre threads**
- **Dependências:** idealmente depois de P1.1 (testes), para ter rede de proteção
- **Pronto quando:** 1.000 quadros alocam <10 MB (hoje ~600 MB) e a imagem continua correta.

### P0.5 — Paralelizar `DepthProcessor` (P-3)
- **Arquivos:** `Processing/DepthProcessor.cs`
- **Complexidade:** baixa (etapa 1 e blur) · **Risco:** baixo
- **Pronto quando:** `ProcessFrame` cai mensuravelmente e a saída é bit-idêntica à serial.

## P1 — FUNDAÇÃO ARQUITETURAL

### P1.1 — Projeto de testes
- **Problema:** nenhuma rede de proteção para as mudanças de P0 e P1.
- **Arquivos:** novo `tests/CaixaInterativa.Tests/`
- **Alteração:** começar pelos testes da seção 17 marcados como prioritários: conservação de massa da água, determinismo do fogo, round-trip de calibração.
- **Complexidade:** baixa · **Risco:** nenhum · **Dependências:** passar `semente` ao `FireSimulation`
- **Pronto quando:** `dotnet test` roda em CI e cobre os quatro invariantes de física.

### P1.2 — Camadas visuais: destravar o renderer
- **Problema:** `Render()` com 16 parâmetros; adicionar fenômeno toca 6 arquivos.
- **Por que importa:** **é o gargalo arquitetural.** Nada mais escala antes disso.
- **Arquivos:** `Rendering/TopographicRenderer.cs`, `Simulation/ISimulationModule.cs`, as três simulações, `SandboxEngine.OnTick`
- **Alteração:** `CamadaVisual` como `readonly record struct`; `Render` recebe `IReadOnlyList<CamadaVisual>`; laço ordenado substitui os três blocos nomeados.
- **Complexidade:** média · **Risco:** médio (toca o caminho de imagem) · **Dependências:** P1.1
- **Pronto quando:** o renderizador não menciona água, fogo nem sismo; a saída visual é idêntica à baseline; `Render` tem ≤8 parâmetros.

### P1.3 — Catálogo de módulos: eliminar o `enum Simulacao`
- **Arquivos:** `SandboxEngine.cs`, `Views/MainWindow.xaml.cs`, `Views/MainWindow.xaml`
- **Alteração:** `IReadOnlyList<ISimulationModule>` no engine; `CmbSimulacao` populado a partir dele; painéis de configuração gerados a partir de `Parametros`.
- **Complexidade:** média · **Risco:** médio · **Dependências:** P1.2
- **Pronto quando:** acrescentar um fenômeno exige criar **um** arquivo e registrá-lo em **uma** linha.

### P1.4 — `TerrainField`
- **Arquivos:** novo `Processing/TerrainField.cs`; consumidores em `Rendering` e `Simulation`
- **Alteração:** encapsular alturas; reamostragem única compartilhada (elimina 3 duplicatas); inclinação com cache; base para acumulação de fluxo e bacias.
- **Complexidade:** média · **Risco:** baixo · **Dependências:** P1.2
- **Pronto quando:** existe uma só implementação de reamostragem e a inclinação é calculada uma vez por quadro.

### P1.5 — Corrigir a geometria da caixa (B-3, B-7)
- **Problema:** `larguraCaixaMm = 1250f` hardcoded; ROI sem interface.
- **Por que importa:** **é honestidade científica, não performance.** Todo valor em litros hoje é decorativo.
- **Arquivos:** `Config/AppConfig.cs`, `Simulation/WaterSimulation.cs`, `SandboxEngine.cs`, UI de calibração
- **Alteração:** dimensões reais em `AppConfig`; ROI definida durante a calibração; tamanho de célula derivado.
- **Complexidade:** média · **Risco:** médio — muda números já mostrados em aula
- **Dependências:** medição física (seção 18, item 11)
- **Pronto quando:** despejar um volume conhecido de água virtual sobre área conhecida produz o valor esperado ±5%.

## P2 — ENGINE DE CENÁRIOS E SIMULAÇÕES

### P2.1 — Conectar a capacidade morta
- **Alteração:** ligar `Atear()` na UI (com foco por clique no preview — `Atear(u,v)` já aceita coordenadas); expor `Cenario.Todos`; ligar `PreSaturar`; expor `SoilMap.Pintar` para compor território.
- **Complexidade:** baixa-média · **Risco:** baixo · **Dependências:** P1.3
- **Pronto quando:** os seis cenários e a queimada são acionáveis por um professor sem ler código.
- **Nota:** corrigir B-5 (trinco da água no fogo) junto — é o que torna a queimada interativa.

### P2.2 — Parâmetros e métricas com classificação de honestidade
- **Alteração:** `Metrica` carrega `Classificacao` (MEDIÇÃO/DERIVAÇÃO/MODELO/VISUAL) e `Origem`. UI exibe o rótulo junto ao número. `PropriedadesDoSolo.Resumo` passa a qualitativo (§10.3).
- **Complexidade:** média · **Risco:** baixo · **Dependências:** P1.3
- **Pronto quando:** nenhum número na projeção aparece sem sua classificação, e não há mais casas decimais em parâmetros didáticos.

### P2.3 — `Experimento` e comparação honesta (B-2)
- **Alteração:** record imutável com assinatura de relevo, composição de cobertura e parâmetros; comparação só entre experimentos com relevo compatível; aviso explícito quando não forem.
- **Complexidade:** média · **Risco:** baixo · **Dependências:** P2.2
- **Pronto quando:** mexer na areia entre execuções faz o software avisar que a comparação não isola a variável.

### P2.4 — Temas de bioma
- **Alteração:** rampa + cobertura padrão + faixas de parâmetro por bioma (§10.4).
- **Complexidade:** baixa-média · **Risco:** baixo · **Dependências:** P1.2, P2.2
- **Pronto quando:** trocar de bioma muda cor **e** resultado da mesma chuva sobre o mesmo relevo.

### P2.5 — Atividades declarativas
- **Complexidade:** média · **Risco:** médio · **Dependências:** P2.3
- **Pronto quando:** uma atividade nova é um arquivo JSON, e um JSON inválido gera aviso sem derrubar o app.

## P3 — EXPANSÃO PEDAGÓGICA

Depende inteiramente de P1 e P2. Fenômenos novos, biomas adicionais, camadas derivadas do terreno (bacias, divisores), gravação e replay de aula, exportação de resultados, alinhamento com a BNCC.

**Sem P1.2 e P1.3, cada item de P3 custa seis arquivos.** Com eles, custa um.

---

# 23. EXPERIÊNCIAS PROPOSTAS

**RECOMENDAÇÃO — seção de futuro.** Nenhuma delas existe hoje. Dificuldade estimada considerando a arquitetura **depois** de P1.

### Água e hidrologia

| # | Experiência | Conceito | O aluno faz na areia | Cenário | Fenômeno | Observa | Precisa de | Dific. | Rigor |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Para onde a água desce? | Divisor de águas | Constrói uma serra | qualquer | acumulação de fluxo | rede de drenagem projetada **antes** de chover | `TerrainField.Acumulacao` | média | modelo quantitativo |
| 2 | A bacia que vocês criaram | Bacia hidrográfica | Vale com duas encostas | qualquer | delimitação de bacia | área que contribui para cada saída | `TerrainField.Bacias` | média | quantitativo |
| 3 | Onde o rio vai transbordar | Planície de inundação | Vale com leito estreito | várzea | chuva | primeiras áreas a alagar | existe | baixa | modelo didático |
| 4 | A barragem | Intervenção hidráulica | Cava o vale, ergue um dique | livre | chuva | a montante enche, a jusante seca | existe | baixa | didático |
| 5 | Lençol freático | Água subterrânea | Cava até "achar água" | qualquer | nível freático | poço enche sozinho | nível freático (novo) | média | didático |
| 6 | Chuva só na cabeceira | Área de contribuição | Bacia alongada | qualquer | chuva localizada | onda de cheia desce | chuva por região | baixa | didático |
| 7 | Duas chuvas seguidas | Saturação antecedente | Qualquer | argiloso | chuva ×2 | a segunda alaga muito mais | `PreSaturar` (existe, morto) | baixa | didático |

### Relevo e geomorfologia

| # | Experiência | Conceito | O aluno faz | Fenômeno | Observa | Dific. | Rigor |
|---|---|---|---|---|---|---|---|
| 8 | Ler curvas de nível | Cartografia | Monta um morro | topografia | curvas apertam onde é íngreme | baixa | quantitativo |
| 9 | Perfil topográfico | Corte transversal | Serra e vale | perfil ao longo de uma linha | gráfico do corte | média | quantitativo |
| 10 | Ângulo de repouso | Estabilidade de talude | Empilha areia ao máximo | medição de declividade | existe um limite físico (~34°) | média | **quantitativo real** |
| 11 | Mesma altitude, relevos diferentes | Altitude × declividade | Dois montes, mesmo topo | hipsometria | cor igual, inclinação diferente | baixa | visual |

### Solos, vegetação e biomas

| # | Experiência | Conceito | O aluno faz | Cenário | Observa | Dific. | Rigor |
|---|---|---|---|---|---|---|---|
| 12 | Mata × solo exposto | Infiltração | Uma encosta | mata → desmatado | mesma chuva, escoamentos diferentes | baixa | didático |
| 13 | Cerrado × Mata Atlântica | Biomas | Mesmo relevo | temas de bioma | mesma chuva, respostas diferentes | média | didático |
| 14 | A raiz segura o morro | Coesão radicular | Encosta íngreme | mata × desmatado | risco de deslizamento | média | didático |
| 15 | Caatinga e a chuva rara | Semiárido | Relevo baixo | caatinga | chuva forte escorre sem infiltrar | média | didático |
| 16 | Terraceamento | Conservação de solo | Escava degraus na encosta | agricultura | erosão despenca | baixa | didático |

### Queimadas

| # | Experiência | Conceito | O aluno faz | Observa | Dific. | Rigor |
|---|---|---|---|---|---|---|
| 17 | O aceiro | Combate a incêndio | Cava um canal na frente do fogo | fogo é barrado | baixa (após B-5) | didático |
| 18 | Fogo sobe morro | Comportamento do fogo | Serra com foco na base | frente sobe mais rápido | existe | didático |
| 19 | O vento mudou | Imprevisibilidade | — | direção alterada no meio | baixa | didático |
| 20 | Depois do fogo, a chuva | Crosta hidrofóbica | Queima e depois faz chover | alaga e erode muito mais | existe (`Cenario` morto) | didático |
| 21 | Cerrado e fogo | Ecologia do fogo | Bioma cerrado | queima diferente da mata | média | didático |

### Urbanização e riscos

| # | Experiência | Conceito | O aluno faz | Observa | Dific. | Rigor |
|---|---|---|---|---|---|---|
| 22 | A cidade no vale | Ocupação de várzea | Vale + cidade no fundo | alaga exatamente onde se construiu | existe (morto) | didático |
| 23 | Cidade que planejou | Drenagem urbana | Mesma cidade, piso permeável | alagamento cai | existe (morto) | didático |
| 24 | Impermeabilizar de a pouco | Efeito cumulativo | Aumenta a área urbana por etapas | curva de alagamento × % impermeável | média | quantitativo |
| 25 | Transferir o problema | Externalidade | Dique protegendo um lado | o outro lado piora | média | didático |
| 26 | Mapa de risco | Gestão de risco | Território qualquer | áreas de risco antes do evento | média | didático |

### Clima e mudanças

| # | Experiência | Conceito | O aluno faz | Observa | Dific. | Rigor |
|---|---|---|---|---|---|---|
| 27 | Nível do mar sobe | Elevação oceânica | Ilha e costa | linha de costa recua | média | **modelo didático — exige declaração explícita** |
| 28 | Degelo das calotas | Criosfera | Dois polos + ilha | gelo recua, mar sobe | alta | didático — declarar |
| 29 | Chuva extrema × chuva normal | Eventos extremos | Mesmo território | comparação de intensidade | baixa | didático |
| 30 | Seca prolongada | Estiagem | Qualquer | solo perde umidade, rio seca | média | didático |

**Nota sobre 27 e 28:** são as que mais exigem o rótulo de honestidade. A areia não derreteu; o gelo é pintado sobre o relevo que os alunos construíram. Se isso não for declarado na tela, a experiência ensina algo falso.

---

# 24. OS 10 ARQUIVOS PARA ENVIAR A OUTRO ENGENHEIRO

Na ordem de leitura recomendada.

| # | Arquivo | Por que este |
|---|---|---|
| 1 | `SandboxEngine.cs` | É a espinha dorsal: mostra o ciclo de vida, o timer, a ordem do pipeline e — no `StartSource` — exatamente como os módulos são acoplados hoje |
| 2 | `Simulation/ISimulationModule.cs` | 52 linhas que revelam o problema central: a abstração declarada e nunca usada |
| 3 | `Rendering/TopographicRenderer.cs` | O verdadeiro ponto de acoplamento — os 16 parâmetros e os três blocos nomeados são o gargalo de extensibilidade |
| 4 | `Processing/DepthProcessor.cs` | Onde mora a diferença entre projeção utilizável e uma que "ferve"; as três etapas e o plano-base por pixel |
| 5 | `Depth/NuiNative.cs` | O interop e, mais importante, as três armadilhas documentadas com o sintoma — evita repetir dias de depuração |
| 6 | `Simulation/WaterSimulation.cs` | O módulo mais completo: tubos virtuais, CFL, infiltração, saturação, e comentários que registram calibrações descartadas |
| 7 | `Simulation/SoilMap.cs` | A melhor abstração do projeto (ambiente) e, ao mesmo tempo, a origem da falsa precisão na interface |
| 8 | `Views/MainWindow.xaml.cs` | Mostra o `enum Simulacao`, a comparação de cenários e o quanto de capacidade não está conectado |
| 9 | `Depth/IDepthSource.cs` + `SimulatedDepthSource.cs` | A abstração de hardware que funciona, e a fonte sintética que torna todo o resto testável sem sensor |
| 10 | `Config/CalibrationStore.cs` | Formato binário, gravação atômica, validação de versão e resolução — o padrão de qualidade a seguir na persistência |

*(Contexto complementar: `ROADMAP.md` para a visão, `docs/DIARIO-DE-BORDO.md` para o histórico de depuração.)*

---

# 25. VEREDITO FINAL

### 1. Qual é o estado técnico atual?

**Maduro na base, imaturo na composição, com capacidade significativa construída e desconectada.** Captura, processamento, calibração e persistência estão em nível de produção — bem pensados, bem comentados, com decisões rastreáveis. A camada de simulação tem física correta e isolada. O que falta é a costura: o renderizador conhece cada módulo pelo nome, a UI enumera modos à mão, e um terço da capacidade escrita não tem caminho até o usuário.

### 2. A arquitetura atual é boa fundação para a visão?

**Sim.** As duas fronteiras difíceis — hardware (`IDepthSource`) e física (módulos isolados, sem dependência de UI ou sensor) — já estão certas. A fronteira que falta é a mais fácil de construir: a de saída visual. Isso é sorte de projeto, não acidente: quem escreveu tinha o desenho certo na cabeça (o `<summary>` de `ISimulationModule` descreve exatamente a arquitetura-alvo) e parou antes de terminar de aplicá-lo.

### 3. Maior gargalo arquitetural?

**`TopographicRenderer.Render()` com 16 parâmetros e três blocos de composição nomeados.** É o que faz um fenômeno novo custar seis arquivos em vez de um. Não é a CPU, não é a falta de ECS, não é a ausência de GPU.

### 4. Maior risco técnico?

**A ausência simultânea de testes e de log**, num sistema que acabou de sair para uso real em sala de aula. Toda mudança em física é uma aposta sem rede, e todo problema em aula vira relato sem evidência. Em segundo lugar: os números apresentados com aparência de medição (§5.2), que são um risco à credibilidade do produto — mais difícil de reparar que qualquer bug.

### 5. Maior oportunidade pedagógica?

**A comparação controlada de cenários.** O embrião existe em `MainWindow.Registrar`/`AtualizarComparacao` e já produz a frase certa — *"a cidade teve 2,4× o resultado da mata, na mesma simulação"*. Transformar isso num `Experimento` de primeira classe, que **sabe quando o relevo mudou e avisa**, converte o software de "simulador bonito" em instrumento de método científico. E ensina controle de variáveis de graça, como efeito colateral de funcionar direito.

### 6. As 5 primeiras mudanças

1. **Log em arquivo** (P0.1) — horas de trabalho; torna todo o resto diagnosticável
2. **Timeout do sensor → `Faulted`** (P0.2) — o modo de falha mais provável em sala, hoje não tratado
3. **Projeto de testes com os 4 invariantes de física** (P1.1) — rede antes de mexer no que importa
4. **Camadas visuais no renderizador** (P1.2) — destrava tudo
5. **Catálogo de módulos, matando o `enum Simulacao`** (P1.3) — e, na sequência imediata, conectar a queimada e os cenários, que é o maior retorno por hora do projeto

### 7. O que eu não mexeria agora

O P/Invoke do NUI. O `CalibrationStore`. O modelo de tubos virtuais. O `SimulatedDepthSource`. O fallback silencioso do `AppConfig`. E o estilo de comentários — que é o maior ativo desta base e deveria virar norma escrita, não hábito informal.

### 8. Quanto pode ser preservado?

**~75%.**

| Camada | Preservação |
|---|---|
| `Depth/*` | ~95% |
| `Processing/*` | ~90% (paralelização por dentro) |
| `Config/*` | ~95% |
| `Simulation/*` (física) | ~85% |
| `Rendering/*` | ~60% (o laço de pixels fica; a interface muda) |
| `Views/*` | ~50% (a estrutura fica; os `switch` por modo saem) |

### 9. Evolução incremental ou reestruturação?

**Incremental, sem hesitação.** Uma reescrita jogaria fora meses de depuração de interop que não se recupera lendo documentação — as três armadilhas do NUI, a calibração dos filtros, os limiares descobertos por tentativa. As mudanças necessárias são cirúrgicas e localizadas: duas assinaturas de método, um enum e um catálogo. Nada aqui justifica começar de novo.

### 10. O primeiro PR concreto

**Objetivo:** dar a `ISimulationModule` um contrato de saída visual e fazer o renderizador consumir camadas em vez de parâmetros nomeados — sem alterar em nada o que aparece na tela.

**Por que este primeiro:** é o pré-requisito de todo o resto. Enquanto o renderizador conhecer os módulos pelo nome, cada fenômeno novo custa seis arquivos.

**Arquivos afetados:**
- `Rendering/CamadaVisual.cs` — novo
- `Rendering/TopographicRenderer.cs` — assinatura de 16 → 7 parâmetros; três blocos nomeados → laço ordenado
- `Simulation/ISimulationModule.cs` — adicionar `IReadOnlyList<CamadaVisual> Camadas { get; }`
- `Simulation/WaterSimulation.cs`, `FireSimulation.cs`, `EarthquakeSimulation.cs` — implementar `Camadas`
- `SandboxEngine.cs` — `OnTick` coleta camadas dos módulos ativos

**Alterações:**
1. `readonly record struct CamadaVisual(float[] Campo, int Largura, int Altura, int Ordem, ModoDeCor Modo, float Limiar)`
2. `ModoDeCor`: `Agua`, `Correnteza`, `Calor`, `Risco`, `Clarao` — os cinco tratamentos que já existem hoje, extraídos como enum
3. Cada módulo expõe suas camadas com a ordem que hoje está implícita no encadeamento dos `if` (água 100, sismo 200/210, fogo 300)
4. O laço de pixels itera sobre as camadas ordenadas, aplicando o `ModoDeCor`

**Testes:**
- **Regressão visual:** campo sintético canônico + cada combinação de módulos ativos; hash do buffer **idêntico** ao baseline capturado antes do PR. Este é o critério que importa.
- Renderizar sem camadas produz o mesmo resultado que hoje com todos os parâmetros nulos
- Ordem das camadas respeitada independentemente da ordem de inserção
- Benchmark: ms/quadro antes e depois

**Critério de aceite:**
- `TopographicRenderer` não menciona "water", "quake" nem "fire" em nenhum identificador
- `Render` tem no máximo 8 parâmetros
- Todos os hashes de regressão visual batem
- Desempenho dentro de 5% do baseline
- `EarthquakeSimulation` continua funcionando — a remoção dele é outro PR, e misturar os dois esconderia regressões

**Deliberadamente fora deste PR:** remover o terremoto, mexer na UI, conectar a queimada, tocar em performance. Cada um é seu próprio PR, verificável isoladamente.
