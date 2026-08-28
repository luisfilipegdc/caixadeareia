# Caixa de Areia Interativa — Diário de Bordo

**Construção de um sistema de projeção topográfica com Kinect v1, do zero ao primeiro relevo na tela.**

Documento gerado em 21 de agosto de 2026. Cobre a sessão completa de desenvolvimento,
do levantamento de hardware até o app rodando com o sensor real.

---

## Sumário

1. [O que é o projeto](#1-o-que-é-o-projeto)
2. [Ponto de partida](#2-ponto-de-partida)
3. [Fase 1 — Levantamento do terreno](#fase-1--levantamento-do-terreno)
4. [Fase 2 — Decisões de arquitetura](#fase-2--decisões-de-arquitetura)
5. [Fase 3 — A pipeline de profundidade](#fase-3--a-pipeline-de-profundidade)
6. [Fase 4 — O primeiro teste falhou, e estava certo](#fase-4--o-primeiro-teste-falhou-e-estava-certo)
7. [Fase 5 — O ambiente resistiu](#fase-5--o-ambiente-resistiu)
8. [Fase 6 — SDK e driver](#fase-6--sdk-e-driver)
9. [Fase 7 — Corrupção de heap](#fase-7--corrupção-de-heap)
10. [Fase 8 — O deslocamento de três bits](#fase-8--o-deslocamento-de-três-bits)
11. [Fase 9 — A flag errada](#fase-9--a-flag-errada)
12. [Fase 10 — Rodando de verdade](#fase-10--rodando-de-verdade)
13. [Fase 11 — Os botões invisíveis](#fase-11--os-botões-invisíveis)
14. [Placar final](#placar-final)
15. [Lições](#lições)
16. [Estado atual e próximos passos](#estado-atual-e-próximos-passos)
17. [Sessão 2 — A auditoria](#sessão-2--a-auditoria-27-de-agosto-de-2026)
18. [Apêndices](#apêndices)

---

## 1. O que é o projeto

Uma caixa com areia. Um Kinect apontado para ela de cima, medindo o relevo. Um projetor
pintando de volta sobre a mesma areia um mapa topográfico colorido — azul nas depressões,
verde nas planícies, marrom nas encostas, branco nos picos — com curvas de nível.

O aluno cava um vale com as mãos e vê água aparecer. Empilha areia e vê a montanha ganhar
curvas de nível e neve no topo. A geografia deixa de ser uma figura no livro e passa a ser
algo que responde ao toque.

O objetivo desta sessão foi construir esse sistema **do zero**, em vez de adaptar um dos
projetos existentes de AR Sandbox.

---

## 2. Ponto de partida

### Recursos disponíveis

| Item | Situação inicial |
|---|---|
| Notebook | Windows 10 Home Single Language 10.0.19045, tela 1366×768 |
| Kinect | Modelo indeterminado, conectado |
| Projetor | Disponível, não conectado durante a sessão |
| Pasta do projeto | Vazia |

### As três decisões que abriram o trabalho

Antes de escrever qualquer linha, três perguntas mudavam materialmente o que seria
construído. Perguntar custou uma rodada; errar teria custado o projeto inteiro.

**1. Qual Kinect?** Determina o SDK, a resolução de profundidade e a precisão.
Resposta: *"não sei"* — o que virou a primeira tarefa técnica.

**2. Qual arquitetura?** Nativo C#, híbrido Python+Web, ou Python puro.
Resposta: **100% nativo C# (.NET + WPF)**.

**3. Qual o MVP visual?** Topografia, água, vulcão, bacia hidrográfica.
Resposta: **mapa topográfico colorido com curvas de nível** — a base clássica, e a única
que faz sentido como primeira etapa, porque todas as outras dependem de um campo de
alturas calibrado e estável.

---

## Fase 1 — Levantamento do terreno

### Identificando o Kinect

Em vez de pedir para o usuário procurar o número de série gravado no aparelho, consultei
o registro de dispositivos do Windows:

```powershell
Get-PnpDevice | Where-Object { $_.InstanceId -match 'VID_045E&PID_02(A[DE]|B[0EF]|C2)' }
```

Resultado:

```
Status   Class                 FriendlyName                        InstanceId
Unknown  Kinect for Windows    Kinect for Windows Audio Control    VID_045E&PID_02BE
OK       libusb-win32 devices  Microsoft Kinect Camera             VID_045E&PID_02BF
Unknown  (vazio)               Xbox Kinect Audio                   VID_045E&PID_02AD
OK       USB                   Generic USB Hub                     VID_045E&PID_02C2
```

Dois achados, ambos importantes.

**O modelo.** `PID_02BE` e `02BF` identificam um **Kinect for Windows, modelo 1517** — não
um sensor de Xbox 360 (`02AE`/`02AD`/`02B0`). Isso é sorte: o 1517 é o único da geração v1
que suporta *near mode*, com alcance de 0,4–3,0 m em vez de 0,8–4,0 m. Para uma caixa de
areia com o sensor a cerca de 1 m, essa diferença é decisiva.

**O problema.** A câmera estava vinculada ao driver **`libusb-win32`**, não ao da
Microsoft. Resíduo de um dos projetos anteriores baseados em libfreenect ou OpenNI.
Enquanto isso não mudasse, `NuiGetSensorCount` retornaria zero e o SDK oficial não veria
o sensor.

> **Decisão:** documentar o problema do driver no README e seguir escrevendo o código.
> Nenhuma linha de código dependia da resolução do driver, e parar ali teria desperdiçado
> a sessão inteira esperando um passo administrativo.

### O ambiente de build

```
Python 3.13.2 · Node v24.15.0 · .NET Host 8.0.24
```

O `dotnet --info` mostrava um host 8.0.24, mas eu truncara a saída em 5 linhas e não vi o
aviso. Só na primeira tentativa de build o problema apareceu: **havia apenas o runtime, não
o SDK**. Lição menor, mas real — truncar saída de diagnóstico esconde exatamente a linha
que importa.

---

## Fase 2 — Decisões de arquitetura

### Decisão 1 — API nativa NUI em vez do wrapper gerenciado

**O caminho óbvio** seria referenciar `Microsoft.Kinect.dll`, o wrapper gerenciado que
acompanha o SDK 1.8.

**O problema:** esse assembly tem como alvo o .NET Framework 4.0. Carregá-lo num processo
.NET 8 é uma fonte conhecida de atrito.

**As alternativas consideradas:**

| Opção | Custo | Risco |
|---|---|---|
| Referenciar `Microsoft.Kinect.dll` no .NET 8 | Zero | Alto — incompatibilidades conhecidas |
| Migrar o projeto para .NET Framework 4.8 | Médio — `UseWPF` em projetos SDK-style com `net48` exige incluir os itens XAML manualmente | Médio |
| P/Invoke direto na API nativa NUI | ~200 linhas de interop | Determinístico, mas exige acertar layout de struct e vtable |

**Escolha: P/Invoke direto.** A justificativa: precisávamos **apenas do stream de
profundidade**. Não há rastreamento de esqueleto, nem áudio, nem stream de cor. O contrato
nativo necessário era pequeno o suficiente para valer a troca, e o resultado roda em .NET 8
limpo, sem camada de compatibilidade.

Essa decisão foi a fonte dos três bugs mais caros da sessão. Ainda assim, eu a tomaria de
novo — as alternativas tinham risco difuso e difícil de diagnosticar, enquanto o interop
tem risco concentrado e, como se viu, diagnosticável com método.

### Decisão 2 — Renderização em CPU

640×480 são 307.200 pixels. A 30 fps, com `Parallel.For`, isso cabe folgado num núcleo
moderno. Uma GPU só se justifica quando entrar a simulação de água, que é iterativa.

**Justificativa:** introduzir um pipeline gráfico agora significaria depurar shaders antes
de ter certeza de que a leitura de profundidade funcionava. Ordem errada de problemas.

### Decisão 3 — Abstrair a fonte de profundidade

```csharp
public interface IDepthSource : IDisposable
{
    event Action<RawDepthFrame>? FrameArrived;
    event Action<string>? Faulted;
    void Start();
    void Stop();
}
```

Duas implementações: `KinectV1Source` e `SimulatedDepthSource`.

**Justificativa:** o simulador não é enfeite. No momento em que foi escrito, o driver
estava trocado e o SDK não estava instalado — o hardware era inalcançável. O simulador
permitiu construir e validar **toda a pipeline** (calibração, suavização, colorização,
curvas de nível, alinhamento de projeção) antes de o sensor existir para o programa. Todo
o trabalho de renderização foi verificado sem hardware.

### Decisão 4 — Alinhamento afim, não homografia

A janela de projeção suporta escala, deslocamento, rotação e espelhamento — não correção
de perspectiva de 4 cantos.

**Justificativa:** a homografia é a solução correta para um projetor oblíquo, mas o WPF não
oferece transformação projetiva 2D nativa; implementá-la exigiria um truque com `Viewport3D`
ou renderização própria. Com o projetor aproximadamente perpendicular à caixa — a montagem
recomendada de qualquer forma — o afim resolve. Registrado como limitação conhecida.

---

## Fase 3 — A pipeline de profundidade

### O problema real

O Kinect v1 tem ruído de 2–4 mm a um metro e produz pixels inválidos nas bordas de
objetos. Sem tratamento, a projeção "ferve": as curvas de nível piscam mesmo com a areia
parada. Três etapas, nesta ordem.

**Etapa 1 — Buracos.** Pixel inválido mantém o último valor bom.

> Zerar criaria crateras piscando nas bordas das mãos e dos montes de areia. Um buraco de
> leitura não é uma depressão no terreno.

**Etapa 2 — Tempo.** Filtro exponencial com α adaptativo.

```csharp
float delta = raw - _smoothed[i];
float alpha = Math.Abs(delta) > s.JumpThresholdMm ? s.FastAlpha : s.SmoothingAlpha;
_smoothed[i] += delta * alpha;
```

> Um α único obriga a escolher entre tremor e arrasto. Areia parada quer α lento
> (α = 0,15); uma mão entrando na caixa produz um salto legítimo acima do limiar (25 mm) e
> quer α rápido (α = 0,65). O adaptativo entrega os dois.

**Etapa 3 — Espaço.** Box blur separável, custo O(1) por pixel independente do raio.

### O plano-base é por pixel, não um número

```csharp
private float[] _basePlaneMm;  // distância sensor→fundo, por pixel
```

> **Justificativa:** uma caixa levemente torta, ou um sensor não perfeitamente
> perpendicular, viraria um gradiente falso atravessando o mapa inteiro. Armazenar a
> distância medida em cada pixel absorve a geometria real da montagem.

### A rampa de cores

Rampa hipsométrica clássica, de azul profundo a branco, passando por areia na linha d'água.

> **Justificativa:** é a convenção dos atlas escolares. O aluno chega sabendo ler. Inventar
> uma paleta "bonita" custaria a leitura imediata.

---

## Fase 4 — O primeiro teste falhou, e estava certo

Escrevi um teste que exercita simulador → processador → renderizador sem WPF, gravando o
resultado como imagem. Primeira execução:

```
Quadros capturados: 39
Calibrado: True, plano-base medio: 816,2 mm
Alturas: min=-5,7mm  max=8,9mm  media=1,2mm
FALHA: relevo achatado - o campo de alturas nao tem variacao.
```

**O teste estava certo. Eu estava errado.**

O simulador gerava relevo continuamente. Calibrei o plano-base **com as colinas
presentes** — então as colinas viraram o novo zero, e o campo de alturas ficou plano. O
algoritmo fez exatamente o que devia.

Mas isso revelou um buraco real: **com o simulador não dava para ensaiar o fluxo de
calibração**, porque ele nunca produzia areia plana. Adicionei `ReliefScale`:

```csharp
/// Em 0 a superficie fica plana, que e' o unico jeito de ensaiar a calibracao do
/// plano-base sem hardware: calibrar com o relevo presente tornaria as colinas o
/// novo zero e o mapa sairia todo achatado.
public double ReliefScale { get; set; } = 1.0;
```

Isso virou uma funcionalidade do painel — *"Simulador: areia plana"* — que permite
reproduzir o fluxo completo sem hardware.

Segunda execução:

```
Quadros planos: 48 | quadros com relevo: 49
Calibrado: True, plano-base medio: 830,0 mm
Alturas: min=-44,9mm  max=72,1mm  media=8,9mm
Deriva media entre quadros consecutivos: 0,130 mm
Render: 640x480, 1228800 bytes
Cores distintas: 4075
TODOS OS CHECKS PASSARAM
```

Deriva de **0,13 mm** entre quadros consecutivos — a suavização segurando o ruído.

![Mapa topográfico gerado pelo simulador](img/01-simulador-topografia.png)

*Primeira validação completa da pipeline, ainda sem hardware: duas colinas, uma bacia
inundada com praia na linha d'água, curvas de nível fechadas.*

![O mesmo relevo sem curvas nem sombreamento](img/02-simulador-sem-curvas.png)

*O mesmo campo de alturas, só com a rampa de cores. A comparação mostra quanto as curvas
de nível e o sombreamento acrescentam à leitura da inclinação.*

> **Lição:** um teste que falha logo na primeira execução por um motivo que você não
> previu é o teste fazendo o trabalho dele. A tentação é ajustar o limiar até passar. O
> certo foi perguntar por que falhou — e a resposta apontou uma funcionalidade faltante,
> não um limiar mal escolhido.

---

## Fase 5 — O ambiente resistiu

### Sem SDK

```
No .NET SDKs were found.
```

Instalado via winget, com autorização explícita: `Microsoft.DotNet.SDK.8` → **8.0.424**.

### Colisão de namespaces

Primeira compilação:

```
error CS0104: "Application" é uma referência ambígua entre
              "System.Windows.Forms.Application" e "System.Windows.Application"
error CS0104: "KeyEventArgs" é uma referência ambígua entre
              "System.Windows.Forms.KeyEventArgs" e "System.Windows.Input.KeyEventArgs"
```

**Causa:** habilitei `UseWindowsForms` para ter acesso a `System.Windows.Forms.Screen` —
enumeração de monitores, que o WPF não expõe. Isso trouxe junto o `using` implícito de
`System.Windows.Forms`, que colide com o WPF em vários tipos.

**Correção** — remover o `using` global e manter a referência qualificada:

```xml
<Using Remove="System.Windows.Forms" />
<Using Remove="System.Drawing" />
```

Segunda rodada trouxe a mesma colisão em `Color` (`System.Drawing` vs
`System.Windows.Media`), resolvida do mesmo jeito. E um aviso:

```
warning WFAC010: Remova as configurações de DPI alto do app.manifest e configure
                 através de Application.SetHighDpiMode
```

**Decisão: suprimir.** `SetHighDpiMode` é uma API do WinForms; este é um app WPF, cuja
consciência de DPI vem justamente do manifesto. O aviso não se aplica — e o manifesto
importa: sem `PerMonitorV2`, com o notebook a 125% e o projetor a 100%, a imagem projetada
sairia borrada e deslocada.

---

## Fase 6 — SDK e driver

O Kinect SDK 1.8 não está no winget. Baixei do servidor oficial da Microsoft e **verifiquei
a assinatura antes de executar**:

```
Status   : Valid
Assinante: CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond
Emissor  : CN=Microsoft Code Signing PCA 2011
SHA256   : 0574651D5576EBD332307DF57C44922F85C569416CD0D75B59D1F99BBE8D1B53
Tamanho  : 222,4 MB
```

Instalação silenciosa, exit code 0. Resultado: `Kinect10.dll` versão **1.8.0.595** em
`System32` e `SysWOW64`.

**E o driver se resolveu sozinho.** A instalação do SDK reivindicou a câmera:

```
Status  Class               FriendlyName
OK      Kinect for Windows  Kinect for Windows Camera
```

Saiu de `libusb-win32`. O passo manual documentado no README não foi necessário — mas
continua registrado, porque em outra máquina pode ser.

---

## Fase 7 — Corrupção de heap

Com o SDK instalado, o primeiro teste contra hardware real:

```
--- 1. Deteccao ---
1 sensor(es) Kinect detectado(s).  (ok=True, count=1)

--- 2. Captura ---
Fonte: Kinect v1 (near mode). Capturando 5s...
[processo morre]
```

Exit code `-1073740940` = **`0xC0000374`, STATUS_HEAP_CORRUPTION**.

A detecção funcionava — `NuiGetSensorCount` retornava 1. O problema estava na captura.

### A investigação

Corrupção de heap não diz onde. Escrevi um diagnóstico que faz cada chamada isoladamente e
imprime o resultado antes do passo seguinte. Duas técnicas foram decisivas:

**Técnica 1 — validar a vtable sem tocar em memória.**

`INuiFrameTexture` expõe `BufferLen()` e `Pitch()`, que retornam inteiros e **não escrevem
memória**. Os valores esperados são conhecidos: 614400 (640×480×2) e 1280 (640×2). Se os
slots 3 e 4 da vtable retornam esses números, a ordem está certa e `LockRect` no slot 5 é
confiável. Se estivessem errados, eu teria valores absurdos em vez de um crash.

**Técnica 2 — marcar o buffer antes da chamada.**

```csharp
for (int i = 0; i < BufSize; i++) Marshal.WriteByte(buf, i, 0xCD);
```

Preencher com `0xCD` e depois verificar quais bytes mudaram revela exatamente quanto a
função nativa escreveu. Resultado:

```
offset | bytes                    | int64
    0  | F0 97 52 EF 3E 02 00 00 | 2469326395376
    8  | CD CD CD CD CD CD CD CD | (não escrito)
   16  | CD CD CD CD CD CD CD CD | (não escrito)
   24  | CD CD CD CD CD CD CD CD | (não escrito)
```

**Apenas 8 bytes escritos.** E `0x23EEF5297F0` é um endereço de heap, não um timestamp.

### A causa

A API flat não preenche a struct — ela devolve um **ponteiro** para ela:

```c
HRESULT NuiImageStreamGetNextFrame(
    HANDLE hStream,
    DWORD dwMillisecondsToWait,
    CONST NUI_IMAGE_FRAME **ppcImageFrame   // ponteiro para ponteiro
);
```

Eu havia assumido a assinatura do método homônimo da **interface `INuiSensor`**, que
preenche `NUI_IMAGE_FRAME*` por valor. Os dois têm o mesmo nome e semânticas diferentes.

Declarando `out NuiImageFrame`, o runtime escrevia os 8 bytes do ponteiro nos primeiros
bytes da struct; o resto ficava com lixo, o `pFrameTexture` aparente virava endereço
inválido, e a primeira leitura destruía o heap.

**O detalhe cruel:** o layout da minha struct estava **correto o tempo todo**. Verificado:

```
sizeof(NuiImageFrame) = 48   (esperado 48 em x64)  ✓
offset pFrameTexture  = 24   (esperado 24)         ✓
offset dwFrameNumber  = 8    (esperado 8)          ✓
```

Passei tempo suspeitando do layout porque era a hipótese mais natural para corrupção de
memória. A causa estava um nível acima.

**Correção:**

```csharp
public static extern int NuiImageStreamGetNextFrame(
    IntPtr hStream, uint dwMillisecondsToWait, out IntPtr ppcImageFrame);

public static extern int NuiImageStreamReleaseFrame(IntPtr hStream, IntPtr pImageFrame);
```

Com a indireção corrigida, tudo bateu de primeira:

```
eImageType    = 4      (DEPTH)     ✓
eResolution   = 2      (640x480)   ✓
BufferLen()   = 614400             ✓
Pitch()       = 1280               ✓
LockRect()    -> hr=0x00000000, size=614400
Pixels validos: 230361 (75,0%)
```

---

## Fase 8 — O deslocamento de três bits

A captura funcionava, mas os números estavam errados:

```
Distancia: min=30400mm  max=65528mm  media=58462mm
```

Trinta metros. Sessenta e cinco metros. Fisicamente impossível para um Kinect.

**A pista:** `65528 = 0x1FFF << 3`. E **todos** os valores eram múltiplos de 8.

Mesmo com `NUI_IMAGE_TYPE_DEPTH` — o tipo que, pela documentação, não inclui índice de
jogador — os três bits inferiores continuam reservados. A profundidade em milímetros está
nos bits 15..3.

```csharp
ushort raw = (ushort)(src[i] >> NUI_IMAGE_PLAYER_INDEX_SHIFT);
dst[i] = raw >= NUI_DEPTH_SATURATED ? (ushort)0 : raw;
```

> **Por que era diagnosticável:** dados corrompidos são aleatórios. Dados *deslocados* têm
> assinatura — se cada valor é múltiplo de 8 e o máximo é exatamente uma potência de dois
> menos um, deslocada, não é corrupção: é um campo empacotado.

---

## Fase 9 — A flag errada

Este foi o mais insidioso dos três, porque **não gerava erro nenhum**.

Apontado o sensor para a mesa, a leitura passou a ser fisicamente plausível — 801 a
1265 mm, média 900 mm. A escala estava confirmada. Mas:

```
Validos: 3731 (1,2%)   zeros: 303469
Diferenca media entre 2 quadros: 236,33 mm
```

Apenas 1,2% da imagem tinha leitura. O padrão sugeria superfície brilhante, sol na cena, ou
sensor perto demais.

![Leitura quase vazia](img/03-cobertura-baixa-near-mode-quebrado.png)

*Azul marca pixels sem leitura. Um erro de constante imitando perfeitamente um problema
físico de superfície.*

### O teste que separou as hipóteses

Em vez de adivinhar, escrevi um comparativo: near mode ligado versus desligado, com
cobertura e distância média por região da imagem.

```
NEAR MODE LIGADO      → 6,9% de cobertura, min=801mm
NEAR MODE DESLIGADO   → 6,9% de cobertura, min=801mm
```

**Idênticos.** E o mínimo era sempre exatamente **801 mm** — que não é um dado, é o piso
duro do modo padrão (0,8 m). O near mode nunca havia sido ativado.

### A causa

```c
#define NUI_IMAGE_STREAM_FLAG_SUPPRESS_NO_FRAME_DATA          0x00010000
#define NUI_IMAGE_STREAM_FLAG_ENABLE_NEAR_MODE                0x00020000  // ← correto
#define NUI_IMAGE_STREAM_FLAG_TOO_FAR_IS_NONZERO              0x00040000  // ← o que eu usei
#define NUI_IMAGE_STREAM_FLAG_DISTINCT_OVERFLOW_DEPTH_VALUES  0x00080000
```

Eu havia usado `0x00040000`, que é `TOO_FAR_IS_NONZERO`. E
`NuiImageStreamSetImageFrameFlags` **retorna `S_OK` de qualquer forma**. Não há sinal de
erro. O sintoma engana: o alcance mínimo permanece em 800 mm e tudo mais perto lê zero,
exatamente como se a superfície não devolvesse infravermelho.

### O resultado da correção

Medido na mesma cena, sem mover nada:

| Métrica | Flag errada | Flag correta |
|---|---|---|
| Cobertura | 6,9 % | **66,4 %** |
| Distância mínima | 801 mm | **455 mm** |
| Metade inferior do quadro | morta | 76–100 % |

| | |
|---|---|
| ![Near mode desligado](img/04-near-mode-desligado.png) | ![Near mode ligado](img/05-near-mode-ligado.png) |
| **Flag errada** — 11% de cobertura, mínimo 801 mm, metade inferior morta | **Flag correta** — 66,4% de cobertura, mínimo 455 mm |

*A mesma cena, sem mover nada. A diferença é um único valor de constante.*

E o gradiente por região passou a ser fisicamente coerente — 856 mm no topo da imagem
caindo suavemente até 655 mm na base, o padrão exato de um sensor inclinado olhando uma
superfície plana.

```
Distancia media por regiao (mm):
lin0    856   848     -     -     -     -   863   866
lin1    817   818     -     -     -   742   772   758
lin2    782   786   780     -   752   769   806   804
lin3    750   754   618   527   724   768   771   769
lin4    719   710   653   662   731   737   737   720
lin5    693   693   696   707   707   708   707   655
```

> **Registrado no código:** o retorno de `SetImageFrameFlags` não prova que o near mode foi
> aplicado. A verificação confiável é empírica — com near mode ativo, aparecem leituras
> abaixo de 800 mm.

---

## Fase 10 — Rodando de verdade

Com os três bugs corrigidos, era hora de ver o app. WPF nativo não é coberto pelos padrões
usuais de automação, então dirigi o aplicativo por **UI Automation**, usando os `x:Name` do
XAML como `AutomationId`:

```powershell
$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $automationId)
$el = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
$el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
```

O roteiro abre o app, inicia o Kinect, calibra, abre a projeção, liga a grade de
alinhamento e captura a tela em cada etapa.

```
Janela: 'Caixa de Areia Interativa - Painel de Controle'
  sensor: 1 sensor(es) Kinect detectado(s).
  [click] BtnStartKinect
  status: Fonte iniciada: Kinect v1 (near mode)
  fps:    21 fps
  [click] BtnCalibrate
  calib:  Calibrado. Distancia media sensor-fundo: 1818 mm.
  [click] BtnProject
```

### O quarto bug, que só aparece rodando

A projeção saiu **branca chapada**.

**A causa:** pixels que nunca leram durante a calibração recebiam a média global como
plano-base — 1818 mm naquele caso. Quando depois liam 700 mm de verdade, a altura calculada
dava 1118 mm, saturava no topo da escala e virava pico nevado.

Esse é um bug que **nenhum teste sintético pegaria**, porque o simulador tem cobertura
quase total. Só aparece com um sensor real olhando uma cena real com sombras de
infravermelho.

E ia acontecer na caixa de areia definitiva, nos cantos — que toda caixa tem.

**Correção em três partes:**

```csharp
// 1. Rastrear quais pixels realmente ganharam plano-base
private bool[] _baseValid;

// 2. Sem plano-base, o pixel fica permanentemente no nível zero
if (!_baseValid[i]) { _smoothed[i] = 0f; continue; }

// 3. Exigir amostras suficientes — um pixel que leu 1 vez em 60 quadros
//    está na borda do alcance e seria ruído promovido a referência
private const int MinCalibrationSamples = 5;
```

Aproveitei para expor a **cobertura da calibração** no painel, com aviso abaixo de 80%:

```
Calibrado. Distancia media sensor-fundo: 787 mm.
Cobertura: 33% da area do sensor.

Cobertura baixa. Verifique: sensor perpendicular e a 0,9-1,2 m,
sem sol direto na caixa, areia seca e fosca.
```

> **Justificativa:** cobertura é o diagnóstico mais útil na hora de posicionar o sensor.
> Sem ele, o professor descobriria o problema com a turma na frente.

![Relevo real calibrado](img/08-relevo-calibrado.png)

*O sistema funcionando com o Kinect real: objetos sobre a mesa viram elevações e
depressões, com curvas de nível acompanhando o contorno. O painel reporta a cobertura da
calibração e avisa quando está baixa.*

![Grade de alinhamento](img/10-projecao-grade-alinhamento.png)

*A janela de projeção com a grade de alinhamento ligada (tecla `G`), usada para casar a
projeção com a borda física da caixa. O branco chapado é o próprio bug do plano-base,
registrado antes da correção.*

---

## Fase 11 — Os botões invisíveis

Com o app aberto, o usuário apontou: os botões estavam ilegíveis.

**A causa:** `Foreground` é uma propriedade **herdada** em WPF. Meu estilo de `GroupBox`
define o azul de destaque para o cabeçalho — e todo o conteúdo dentro do grupo herda essa
cor. Como eu nunca dera `Background`/`Foreground` explícitos ao `Button`, o texto herdava o
azul e caía sobre o cinza-claro do chrome padrão.

**Correção:** `ControlTemplate` próprio, com estados de hover, pressionado e desabilitado
visivelmente distintos — porque "desabilitado" e "com baixo contraste" estavam parecendo a
mesma coisa.

> **Lição:** este bug sobreviveu a uma execução automatizada inteira. O roteiro de UI
> Automation clicava nos botões por `AutomationId` e reportava sucesso — a automação não
> lê contraste. Capturei as telas e olhei, mas interpretei o cinza como estado
> desabilitado. Foi preciso um humano dizer "esses botões estão sem leitura".

---

## Placar final

### Bugs encontrados e corrigidos

| # | Bug | Sintoma | Como foi encontrado |
|---|---|---|---|
| 1 | Indireção de ponteiro na API flat | Crash `0xC0000374` | Marcador `0xCD` no buffer |
| 2 | Profundidade deslocada 3 bits | Distâncias 8× maiores | Todos os valores múltiplos de 8 |
| 3 | Flag de near mode errada | 6,9% de cobertura, sem erro algum | Comparativo A/B com near mode |
| 4 | Plano-base por média global | Projeção branca chapada | Rodar o app de verdade |
| 5 | `Foreground` herdado do `GroupBox` | Botões ilegíveis | Usuário olhando a tela |
| 6 | Calibração com relevo presente | Teste falhou | O próprio teste |

Nenhum dos seis foi pego por compilação. Três precisaram de hardware real. Um precisou de
olho humano.

### Medições finais

| Métrica | Valor |
|---|---|
| Taxa de captura | 20–29 fps |
| Deriva entre quadros (simulador) | 0,13 mm |
| Cobertura com near mode | 66,4 % |
| Alcance mínimo | 455 mm |
| Resolução | 640 × 480 |
| Build | Release, 0 avisos, 0 erros |
| Linhas de interop nativo | ~200 |

---

## Lições

**1. Perguntar três coisas no início economizou a sessão.** Modelo do Kinect, arquitetura e
MVP visual. Cada uma mudava materialmente o que seria construído.

**2. O simulador não foi luxo, foi o que permitiu trabalhar.** Enquanto o hardware estava
inalcançável, toda a pipeline de renderização foi construída e validada.

**3. Erros silenciosos são os caros.** Dos três bugs de interop, o que mais custou foi o
único que retornava `S_OK`. Um crash aponta para si mesmo; um `S_OK` mentiroso não.

**4. Dados errados têm assinatura.** Múltiplos de 8 apontaram um campo empacotado. Um
mínimo sempre igual a 801 apontou um piso de configuração, não uma medição. Vale olhar a
*forma* dos números antes de teorizar.

**5. Teste o que não escreve memória primeiro.** `BufferLen()` e `Pitch()` validaram a
vtable inteira sem risco de crash. Em interop, ordenar os testes do mais seguro para o mais
perigoso transforma um crash mudo em um relatório.

**6. Automação não substitui olhar.** O roteiro de UI Automation clicava e reportava
sucesso enquanto os botões estavam ilegíveis.

**7. Rodar de verdade encontra o que teste nenhum encontra.** O bug do plano-base exigia um
sensor real, uma cena real e sombras de infravermelho reais.

---

## Estado atual e próximos passos

### Pronto e verificado

- Captura do Kinect v1 a 20–29 fps, near mode ativo, sem crash
- Calibração de plano-base por pixel, com relatório de cobertura
- Pipeline de suavização em três etapas
- Mapa topográfico com rampa hipsométrica, curvas de nível e sombreamento
- Janela de projeção em tela cheia, alinhável por teclado, persistida em `config.json`
- Simulador completo para ensaio sem hardware
- Build Release limpo

### Limitações conhecidas

- **Alinhamento apenas afim.** Projetor muito oblíquo deixa distorção residual.
- **Sem simulação de água.** É a próxima etapa, e provavelmente pede GPU.
- **Sem correção de distorção da lente.** Erro de alguns milímetros nas bordas.
- **`MaxValidDepthMm` em 2000.** Sensor mais alto que 2 m exige ajuste no `config.json`.

### Próximos passos, em ordem

1. **Montar a caixa** — sensor perpendicular a 0,9–1,2 m, areia seca e fosca, 8–15 cm de
   profundidade
2. **Calibrar com areia real** e verificar se a suavização precisa de ajuste — areia
   espalha infravermelho de forma diferente de uma superfície lisa
3. **Homografia de 4 cantos**, se a montagem do projetor exigir
4. **Simulação de água** — o módulo que transforma o mapa em aula de bacia hidrográfica

---

## Sessão 2 — A auditoria (27 de agosto de 2026)

Seis dias depois da sessão de construção, o projeto estava em outro lugar: versão 1.3, com
módulos de água, solo, terremoto e queimada, manual do usuário, página de projeto e
executável publicado. E a caixa física finalmente montada — a Fase 2 do roadmap, que tinha
sido pulada, foi concluída em campo.

Antes de abrir novas frentes (temas de bioma, queimada interativa, degelo das calotas),
valia parar e responder uma pergunta que ninguém tinha feito ainda:

> **Esta base de código consegue virar uma plataforma com dezenas de experiências, ou vai
> virar um monólito de modos hardcoded?**

Esta seção registra o que a leitura integral do código respondeu. O relatório completo está
em **[AUDITORIA-TECNICA.md](AUDITORIA-TECNICA.md)**; aqui fica a narrativa e o que ela
mudou de decisão.

### O achado que mudou o plano: capacidade construída e desconectada

A primeira surpresa não foi arquitetural. Foi de integração.

`FireSimulation` tem 350 linhas funcionais — propagação por autômato celular, vento,
efeito de encosta, barreira de água, e a gravação da cicatriz no mapa de solo para que a
chuva seguinte encontre um território diferente. Está instanciada no `SandboxEngine`,
atualizada no tick, desenhada pelo renderizador e com painel próprio na projeção.

**E nada no projeto chama `Atear()`.** O combo de simulações oferece "Chuva" e "Terremoto".
A queimada roda no motor e não tem interruptor.

Puxando esse fio, apareceram mais:

| O que existe | Chamadas de fora |
|---|---|
| `FireSimulation.Atear` | 0 |
| `Cenario.Todos` — os seis cenários pedagógicos | 0 |
| `WaterSimulation.DespejarEm` | 0 |
| `WaterSimulation.PreSaturar` | 0 |
| `SoilMap.Pintar` / `Composicao` | 0 |
| `WaterSimulation.Erosao` / `ErosaoTotal` | calculados todo quadro, nunca exibidos |
| `WaterSimulation.EscoadoLitros` | **nunca calculado — é sempre 0** |

Os seis cenários são o caso mais caro. "Enchente no Rio Grande do Sul", "A mesma enchente
com a várzea preservada", "Cidade que planejou a drenagem" — cada um com contexto real,
pergunta investigativa, composição de solo por altitude e saturação inicial. É exatamente
a camada pedagógica que o roadmap descreve como o diferencial do projeto, e ela está
inalcançável.

**A lição:** o gargalo não era falta de capacidade. Era falta de costura. Conectar o que já
existe é a maior razão custo/benefício do projeto inteiro, e é trabalho de interface, não
de arquitetura.

### O gargalo arquitetural real

`ISimulationModule` foi escrita para ser o ponto de extensão. Três classes a implementam.

**Zero as usam polimorficamente.** Nenhuma `List<ISimulationModule>`, nenhum parâmetro
desse tipo, em todo o repositório. O `SandboxEngine` guarda campos concretos: `Agua`,
`Terremoto`, `Fogo`. A interface é documentação com sintaxe de C#.

O motivo dela não poder ser usada ficou claro ao ler o renderizador: **a interface não diz
nada sobre saída visual**. Um módulo produz `Profundidade`, outro `Calor`, outro
`Intensidade` — e quem sabe compor isso é o `TopographicRenderer`, por código escrito à
mão. A assinatura de `Render()` tem **16 parâmetros, 11 deles específicos de módulos**, e o
corpo tem três blocos `if` nomeados: água, terremoto, fogo.

Resultado prático: **adicionar um fenômeno hoje custa seis arquivos**, com cadeias de
`switch` em quatro deles. E o padrão já começou:

```csharp
// Views/MainWindow.xaml.cs
private enum Simulacao { Chuva, Terremoto }
```

Esse enum é a semente da explosão de modos. Com dois itens ainda é inofensivo; com doze,
é o monólito que a pergunta da auditoria queria evitar.

**A boa notícia:** o acoplamento não está espalhado por 6.000 linhas. Está em três pontos —
a assinatura do `Render`, o laço de pixels e o enum da UI. As simulações em si são classes
autocontidas que não conhecem a UI, nem o renderizador, nem o Kinect. A cirurgia é pequena.

### O problema de honestidade científica que ninguém tinha visto

O projeto declara, no roadmap, que o software deve distinguir medição de modelo. Auditando
cada etapa contra esse critério, o código passa bem: `EarthquakeSimulation` declara no
próprio `<summary>` que é modelo didático, `PropriedadesDoSolo` declara que os números não
são medições de campo, `VelocidadeOndaMmPorSegundo` explica por que é deliberadamente
irreal.

**O problema é que essa ressalva nunca sai do código-fonte.**

Duas violações concretas:

**1. Falsa precisão na tela.** `PropriedadesDoSolo.Resumo` monta a string que o professor
lê ao escolher uma cobertura:

> *"Mata — Absorve 3,2 mm/s · guarda até 160 mm · resiste 95% à erosão"*

Uma casa decimal em mm/s comunica precisão hidrológica. O comentário que diz que são
valores didáticos está doze linhas acima, no código. É exatamente o risco de *"Cerrado =
infiltração 0,65"* — e já está em produção.

**2. Litros calculados com uma constante que ninguém configurou.**

```csharp
public WaterSimulation(int larguraSensor, int alturaSensor, float larguraCaixaMm = 1250f)
```

O `SandboxEngine` sempre usa o padrão. Esse 1250 mm vira o tamanho de célula, que vira a
área, que vira `VolumeLitros` — projetado na parede em fonte 34, como se fosse medição. Mas
a caixa tem 101 cm de largura, e o eixo de 640 px do sensor cobre `1,0859 × distância` —
a 1,28 m, cerca de 139 cm. **A área de célula tem erro sistemático de ordem de 25%.**

E não existe nenhuma relação estabelecida entre o campo de visão do sensor e as bordas
físicas da caixa: a ROI existe em `ProjectionSettings`, tem padrão de quadro inteiro, e
**não tem interface** — só é editável à mão no `config.json`.

**A decisão que isso força:** enquanto a geometria real da caixa não for medida e
configurada, nenhum valor absoluto deveria ser exibido. Grandezas relativas (% alagado,
razão entre cenários) continuam válidas e são, aliás, as que respondem à pergunta da aula.

### A comparação de cenários — a melhor ideia, com o pior bug

`MainWindow.Registrar` e `AtualizarComparacao` implementam a comparação controlada que o
projeto sempre quis, e produzem a frase certa:

> *"Área urbana teve 2,4× o resultado de Mata, na mesma simulação."*

Pedagogicamente é o melhor do projeto. Mas a chave do histórico é `(Simulação, Cobertura)`
— **o relevo não entra**. Se os alunos mexerem na areia entre as duas execuções, o sistema
compara terrenos diferentes e apresenta a razão como se fosse causada pela cobertura.

**É uma conclusão cientificamente falsa apresentada como resultado da aula.** Num software
cujo princípio declarado é honestidade científica, esse é o bug mais grave encontrado — e
é de lógica, não de física.

A correção é também uma oportunidade: quando o relevo mudar entre execuções, o software
deveria dizer *"a comparação não isola o efeito da cobertura"*. Isso ensina controle de
variáveis de graça, como efeito colateral de funcionar direito.

### O que a auditoria confirmou que está certo

Nem tudo foi problema. Vale registrar o que passou no escrutínio, porque isso define o que
**não** mexer:

- **O timer a 60 Hz sobre um sensor de 30 fps está correto.** Há um guard por
  `FrameNumber` que impede reprocessar o mesmo quadro; o timer rápido só reduz a espera
  média de ~16 ms para ~8 ms. É otimização de latência real, não desperdício.
- **A abstração de hardware cumpre o que promete.** As simulações não têm nenhuma
  dependência de Kinect. Trocar de sensor ou adicionar replay de aula exigiria mexer só na
  `MainWindow`.
- **O solver de água é sólido.** O limitador que impede uma célula de entregar mais água do
  que tem é a diferença entre conservar massa e "criar água do nada". Os substeps por CFL
  preservam o fenômeno em vez de deformá-lo para caber no orçamento.
- **`CalibrationStore` grava em `.tmp` e move por cima** — atômico, protegido contra queda
  de energia no meio da gravação.
- **`AppConfig.Load` engole exceção e volta aos padrões** — porque config corrompida não
  pode impedir o app de abrir numa sala de aula.

E o interop do NUI, que custou os três bugs desta mesma sessão, está correto e — mais
valioso — documentado **com o sintoma**, não só com a solução. Foi a única parte do código
que a auditoria recomendou explicitamente não tocar.

### Dois riscos novos, encontrados na leitura

**O sensor que para de falar sem desconectar.** O laço de captura trata `WAIT_TIMEOUT` com
`continue`, indefinidamente. Desconexão dura dispara exceção e aciona a reconexão
automática; mas um sensor que simplesmente emudece produz **tela congelada, sem mensagem e
sem reconexão**. É o modo de falha mais provável numa sala de aula, e é o único não
tratado.

**A barreira de água do fogo é um trinco permanente.** Quando `TentarAcender` encontra
água, marca a célula como `NaoQueima` — estado terminal. A decisão é tomada uma vez. Isso
significa que **um canal cavado durante o incêndio não barra o fogo** — justamente a
interação que se queria construir.

### O que mudou de decisão

| Antes da auditoria | Depois |
|---|---|
| "Construir a simulação de queimada" | Ela existe. **Ligar** na UI e corrigir o trinco da água |
| "Adicionar temas de bioma (cor)" | Temas trocam cor **e** cobertura padrão **e** faixas de parâmetro — senão é enfeite |
| "Começar pelos biomas" | Antes: camadas visuais no renderizador. Sem isso, cada fenômeno novo custa 6 arquivos |
| "Otimizar / considerar GPU" | Sem evidência de que a CPU seja o limite. O gargalo é arquitetural. GPU acrescenta dependência de driver numa máquina de escola |
| "A comparação de cenários funciona" | Funciona e pode mentir. Precisa da assinatura do relevo na chave |
| "Os números da interface estão ok" | Falsa precisão e litros com erro de ~25%. Corrigir antes de novas aulas |

### Placar da auditoria

| Categoria | Achados |
|---|---|
| Abstrações mortas | 1 (`ISimulationModule`) |
| Capacidade construída e inalcançável | 7 membros públicos + 6 cenários + 1 módulo inteiro |
| Bugs de severidade alta ou crítica | 5 |
| Gargalos de performance comprovados | 6 (o maior: 18 MB/s de alocação em LOH) |
| Violações do princípio de honestidade | 3 |
| Testes automatizados existentes | **0** |
| Linhas de log em arquivo | **0** |
| Código estimado como preservável | **~75%** |

### Veredito

A fundação está certa: captura, processamento, calibração e persistência estão em nível de
produção. As duas fronteiras difíceis — hardware e física — já estão desacopladas. A
fronteira que falta, a de saída visual, é a mais fácil de construir.

Curiosamente, o desenho correto já estava escrito: o `<summary>` de `ISimulationModule`
descreve **exatamente** a arquitetura-alvo que a auditoria recomenda. Quem escreveu tinha o
plano certo e parou antes de terminar de aplicá-lo.

**Evolução incremental, sem reescrita.** Uma reescrita jogaria fora meses de depuração de
interop que não se recupera lendo documentação — as três armadilhas do NUI, a calibração
dos filtros, os limiares descobertos por tentativa. As mudanças necessárias são duas
assinaturas de método, um enum e um catálogo.

### Próximos passos revistos

1. **Log em arquivo** — a caixa física está pronta e as aulas começam; sem log, problema em
   sala vira relato sem evidência
2. **Timeout do sensor → `Faulted`** — o modo de falha mais provável, hoje não tratado
3. **Projeto de testes** com os quatro invariantes de física — rede antes de mexer no que
   importa
4. **Camadas visuais no renderizador** — destrava todo o resto
5. **Catálogo de módulos**, matando o `enum Simulacao` — e, na sequência, conectar a
   queimada e os seis cenários

### Pendência de registro

A montagem física foi concluída, mas as medidas de campo ainda não entraram nos
documentos. Falta registrar: comprimento final da viga, altura do sensor até a areia,
cobertura medida em %, modelo e posição do projetor, e o que de fato aconteceu com os
riscos previstos — sombra de infravermelho das mãos, ruído da areia real, escala do
relevo. Até que isso seja feito, `ROADMAP.md` e `MONTAGEM-FISICA.md` continuam afirmando
que nada rodou sobre areia de verdade.

E há uma medição que virou pré-requisito de honestidade, não de performance: **as
dimensões reais cobertas pelo sensor**. Sem elas, todo valor em litros projetado na parede
é decorativo.

---

## Apêndices

### A. Referência de flags NUI

| Constante | Valor | Efeito |
|---|---|---|
| `NUI_INITIALIZE_FLAG_USE_DEPTH` | `0x00000020` | Habilita o stream de profundidade |
| `NUI_IMAGE_TYPE_DEPTH` | `4` | Profundidade sem índice de jogador |
| `NUI_IMAGE_RESOLUTION_640x480` | `2` | Resolução do stream |
| `SUPPRESS_NO_FRAME_DATA` | `0x00010000` | — |
| **`ENABLE_NEAR_MODE`** | **`0x00020000`** | **0,4–3,0 m; só no modelo 1517** |
| `TOO_FAR_IS_NONZERO` | `0x00040000` | Além do alcance retorna saturado |
| `DISTINCT_OVERFLOW_DEPTH_VALUES` | `0x00080000` | — |

### B. Layout de `NUI_IMAGE_FRAME` em x64

| Offset | Campo | Tamanho |
|---|---|---|
| 0 | `liTimeStamp` | 8 |
| 8 | `dwFrameNumber` | 4 |
| 12 | `eImageType` | 4 |
| 16 | `eResolution` | 4 |
| 20 | *(padding)* | 4 |
| 24 | `pFrameTexture` | 8 |
| 32 | `dwFrameFlags` | 4 |
| 36 | `ViewArea` | 12 |
| | **Total** | **48** |

### C. vtable de `INuiFrameTexture`

| Slot | Método | Seguro para sondar? |
|---|---|---|
| 0–2 | `QueryInterface`, `AddRef`, `Release` | — |
| 3 | `BufferLen()` → 614400 | **Sim** — retorna int |
| 4 | `Pitch()` → 1280 | **Sim** — retorna int |
| 5 | `LockRect()` | Não — escreve memória |
| 6 | `GetLevelDesc()` | Não |
| 7 | `UnlockRect()` | Não |

### D. Estrutura do projeto

```
CAIXA INTERATIVA/
├── CaixaInterativa.sln
├── README.md
├── docs/
│   └── DIARIO-DE-BORDO.md
└── src/CaixaInterativa/
    ├── Depth/
    │   ├── IDepthSource.cs           contrato da fonte de profundidade
    │   ├── NuiNative.cs              P/Invoke para Kinect10.dll
    │   ├── KinectV1Source.cs         captura real
    │   └── SimulatedDepthSource.cs   relevo sintético
    ├── Processing/
    │   └── DepthProcessor.cs         calibração, buracos, suavização
    ├── Rendering/
    │   └── TopographicRenderer.cs    rampa, curvas, sombreamento
    ├── Config/
    │   └── AppConfig.cs              persistência em config.json
    ├── Views/
    │   ├── MainWindow.xaml           painel de controle
    │   └── ProjectionWindow.xaml     tela cheia no projetor
    └── SandboxEngine.cs              orquestração
```

### E. Ambiente final

```
Windows 10 Home Single Language 10.0.19045
.NET SDK 8.0.424
Kinect for Windows SDK 1.8 — Kinect10.dll 1.8.0.595
Kinect for Windows modelo 1517 (VID_045E, PID_02BE/02BF)
Alvo: net8.0-windows, x64, WPF
```
