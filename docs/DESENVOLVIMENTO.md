# Guia de desenvolvimento

Para quem vai mexer no código. O [README](../README.md) cobre visão, instalação e estrutura
de pastas — isto aqui não repete nada disso. Aqui está o que só importa na hora de editar:
o caminho de um quadro, como acrescentar um fenômeno, e as regras que protegem o que já
funciona.

---

## 1. O caminho de um quadro

Vale conhecer antes de mexer em qualquer coisa no laço principal.

```
THREAD "KinectDepthCapture"  (prioridade AboveNormal)
  WaitForSingleObject(evento, 200ms)
  NuiImageStreamGetNextFrame  →  ponteiro para struct do runtime
  TextureLockRect (vtable slot 5)
  CopyDepth: unsafe, >>3 para tirar os bits de player index, saturado → 0
  clone do buffer  →  RawDepthFrame
  FrameArrived
      │
      │  Volatile.Write(_latestFrame)     ← ÚNICO ponto entre threads
      │  slot único: quadro novo sobrescreve quadro não consumido
      ▼
UI THREAD  (DispatcherTimer, 16 ms, DispatcherPriority.Render)
  SandboxEngine.OnTick
    descarta se FrameNumber == último renderizado
    DepthProcessor.ProcessFrame        → float[] alturas em mm
    dt = min(0,1s, relógio)
    Fogo.Agua = Agua.Profundidade       ← reaponta buffer que a água troca
    para cada módulo ativo: Atualizar(alturas, largura, altura, dt)
    ColetarCamadas()                    → List<CamadaVisual>
    TopographicRenderer.Render(...)     → byte[] BGRA
    WriteableBitmap.WritePixels
      │
      ├─► MainWindow.Preview
      └─► ProjectionWindow → projetor
```

### O que isso implica na prática

- **Tudo o que é pesado roda na UI thread.** Uma simulação lenta não trava só a projeção:
  trava a janela do professor, no meio da aula. Se você acrescentar algo caro, meça.
- **O timer roda a ~60 Hz e o sensor entrega ~30.** Metade dos ticks sai cedo pelo teste
  de `FrameNumber`. Isso é proposital: um timer alinhado a 33 ms teria até 33 ms de espera
  ociosa, e numa caixa de areia atraso é pior que perda de quadro.
- **As simulações só avançam quando chega quadro novo.** Se o sensor engasgar, a chuva
  para junto. O `dt` acumulado é medido corretamente e limitado a 100 ms, então não há
  explosão numérica — mas o fenômeno congela. É uma limitação conhecida.
- **O clone do buffer na captura é load-bearing.** É ele que garante que a UI nunca leia
  um array sendo reescrito pela thread do sensor. Se for trocado por um pool para reduzir
  alocação, a garantia precisa ser preservada de outra forma.

---

## 2. Como acrescentar um fenômeno

O objetivo da arquitetura de camadas é que isso custe **um arquivo novo e uma linha de
registro**. Não deveria ser preciso tocar no renderizador.

### 2.1 Implemente `ISimulationModule`

```csharp
public sealed class MinhaSimulacao : ISimulationModule
{
    private readonly int _w, _h;
    private readonly float[] _campo;
    private readonly CamadaVisual[] _camadas;

    public string Nome => "Nome que o professor lê";
    public int Width => _w;
    public int Height => _h;
    public bool Ativo { get; set; }

    public MinhaSimulacao(int larguraSensor, int alturaSensor)
    {
        // Metade da resolução do sensor é a convenção do projeto: cabe no orçamento
        // de quadro e o resultado é um campo suave, que a reamostragem bilinear
        // recupera sem degrau visível.
        _w = Math.Max(2, larguraSensor / 2);
        _h = Math.Max(2, alturaSensor / 2);
        _campo = new float[_w * _h];

        _camadas = [ new CamadaVisual(_campo, _w, _h, ordem, modo, limiar) ];
    }

    public IReadOnlyList<CamadaVisual> Camadas => _camadas;

    public void Atualizar(float[] terrenoMm, int larguraTerreno, int alturaTerreno, float dt)
    {
        if (!Ativo) return;
        // ...
    }

    public void Limpar() { /* volta ao estado inicial, mantendo configuração */ }
}
```

### 2.2 Registre no `SandboxEngine`

Em `StartSource`, depois de criar:

```csharp
_modulos.Add(MinhaSimulacao);
```

**A ordem de registro é a ordem de composição visual.** A lista precisa produzir
`CamadaVisual.Ordem` crescente na concatenação — hoje água (100), terremoto (200, 210),
fogo (300). O renderizador **não ordena**, porque ordenar dentro do laço de pixels seria
caro e desnecessário. O teste `ConcatenacaoNaOrdemDoEngineJaSaiCrescente` protege isso.

### 2.3 A armadilha do buffer trocado

Se a sua simulação trocar o array entre quadros — como `WaterSimulation` faz no
`MoverAgua` com `(_agua, _aguaNova) = (_aguaNova, _agua)` — **não guarde a `CamadaVisual`
no construtor.** Remonte a struct a cada acesso:

```csharp
public IReadOnlyList<CamadaVisual> Camadas
{
    get
    {
        _camadas[0] = new CamadaVisual(_campoAtual, _w, _h, ordem, modo, limiar);
        return _camadas;
    }
}
```

Escrever a struct num array já alocado não aloca nada. Esse defeito exato já apareceu duas
vezes no projeto: uma na camada da água, outra na referência que o fogo guardava da
água — nesta última, medimos que **7 de 20 quadros** liam o buffer errado.

---

## 3. Como acrescentar um modo de cor

Só quando nenhum dos existentes serve. Hoje há quatro: `Agua`, `Risco`, `Clarao`, `Calor`.

1. Acrescente o valor em `ModoDeCor` (`Rendering/CamadaVisual.cs`).
2. Acrescente um `case` no `switch` dentro do laço de pixels de `TopographicRenderer`.
3. **Capture o baseline de regressão antes**, e confirme que os oito cenários existentes
   continuam com o mesmo hash depois.

Regras do laço de pixels — ele roda ~307 mil vezes por quadro:

- Sem delegate, sem lambda, sem interface por pixel.
- Sem alocação. Nada de `new` dentro do laço.
- Aritmética simples. Evite `pow`, trigonometria e divisão onde uma multiplicação resolve.
- Compare com `if (valor > limiar)`, e não `if (!(valor <= limiar))` invertido sem pensar:
  as duas formas divergem para `NaN`.

---

## 4. Classificação: medição, derivação, modelo, efeito

Todo número que chega à tela pertence a uma destas categorias, e o projeto se compromete a
não confundi-las.

| Categoria | Significa | Exemplos no código |
|---|---|---|
| **Medição** | Veio do sensor | Profundidade em mm; plano-base da calibração |
| **Derivação** | Conta exata sobre medição | Altura = base − distância; curvas de nível; `AssinaturaDoRelevo` |
| **Modelo didático** | Parâmetro escolhido para ensinar | Infiltração por solo; propagação do fogo; ondas sísmicas |
| **Efeito visual** | Só para ler melhor | Rampa hipsométrica; sombreamento; clarão da onda |

### Regras ao escrever código novo

- **Modelo didático nunca vai à tela com falsa precisão.** `PropriedadesDoSolo.Resumo` é o
  exemplo a seguir: internamente há `3.2f`, mas a interface diz *"absorve muita água"*.
- **Valor absoluto que dependa de geometria não calibrada vem marcado.** É o caso dos
  litros, que dependem de `Config.Caixa.LarguraCobertaPeloSensorMm`. Enquanto
  `LarguraMedida` for falso, a interface usa "≈".
- **Porcentagens são seguras.** São razões entre contagens de células e não passam pelo
  tamanho da célula.
- **Parâmetro didático novo precisa declarar que é didático**, no comentário, com a razão
  da ordem de grandeza. Não copie número de fonte não verificada.

---

## 5. Testar sem Kinect

Nenhum teste da suíte precisa de hardware.

```bash
dotnet test CaixaInterativa.sln -c Release
```

### O que já está coberto

| Arquivo | Protege |
|---|---|
| `RegressaoVisualTests` | Oito combinações de fenômenos, hash SHA-256 do buffer BGRA |
| `MapeamentoDeCamadasTests` | Cada módulo declara a camada certa, na ordem certa |
| `AssinaturaDoRelevoTests` | Imunidade a ruído, detecção de escavação, limiar declarado |
| `HonestidadeCientificaTests` | Coeficientes intactos; resumo sem dígitos; litros escalam com a largura |
| `AcoplamentoFogoAguaTests` | Fogo lê o buffer vivo da água; barreira de água funciona |
| `PipelineComSimuladorTests` | Pipeline inteira com `SimulatedDepthSource`, menos WPF |

### Regressão visual — como funciona, e o que não fazer

`RegressaoVisualTests` guarda o SHA-256 do buffer de cada cenário, capturado antes da
refatoração de camadas. Se um hash mudar:

**Não reescreva o baseline para o teste passar.** Ou a composição visual mudou de verdade
— e isso é uma regressão a investigar — ou a mudança é intencional e o novo valor precisa
vir acompanhado da justificativa.

Os campos sintéticos em `CenariosDeRegressao` foram escolhidos para cruzar **todos** os
limiares do renderizador (0,25 mm de água; 35 mm de saturação; 0,15 de dano; 0,04 de onda;
0,03 de calor). Se você mudar um limiar, confirme que o campo correspondente ainda passa
dos dois lados dele.

### Escrever um teste determinístico

- Nada de `Random` sem semente. `FireSimulation` aceita `semente:` no construtor
  justamente para isso; sem ela, sorteia.
- Nada de relógio. `Atualizar` recebe `dt` como parâmetro — passe valores fixos.
- Simulações de propagação são caras em grade cheia. Testes de fogo usam resolução menor
  (160×120 → grade 80×60); a geometria é a mesma e o custo cai muito.

---

## 6. O que não mexer sem motivo forte

Estas partes foram depuradas com hardware real, e três dos bugs custaram dias:

- **`Depth/NuiNative.cs`** — o P/Invoke. Em especial: `NuiImageStreamGetNextFrame` devolve
  um **ponteiro**, não a struct; a profundidade vem deslocada **3 bits**; e near mode é
  `0x00020000`, não `0x00040000`. Cada um desses tem o sintoma documentado no comentário.
- **`Depth/KinectV1Source.cs`** — a ordem de inicialização, o desempacotamento e a
  liberação do quadro.
- **`Processing/DepthProcessor.cs`** — as três etapas (buracos → tempo → espaço) nesta
  ordem, o α adaptativo e o plano-base por pixel.
- **`Config/CalibrationStore.cs`** — gravação atômica com arquivo temporário.

Se encontrar um bug aqui, prefira **documentar** e abrir para revisão humana em vez de
corrigir às cegas. O diário de bordo tem o histórico do que já custou caro.

---

## 7. Convenções

- **Domínio em português** (`Atualizar`, `Profundidade`, `TipoDeSolo`,
  `AreaAlagadaPercent`); infraestrutura em inglês (`Render`, `Width`, `Buffer`).
- **Cabeçalho GPL de 12 linhas** em todo `.cs`.
- **Comentário explica o porquê, com o número medido junto.** O padrão é registrar a
  alternativa descartada e o motivo. Exemplo real, em `SoilMap.cs`: *"Medido: 10,87 ms com
  o switch, 10,52 ms com a tabela — cerca de 3%. Fica pela previsibilidade, não pelo
  ganho."*
- **`TipoDeSolo` é serializado por valor.** Tipos novos entram **no fim** do enum, ou as
  calibrações e configurações salvas passam a apontar para a cobertura errada.
- Mensagens de commit em português, no imperativo, dizendo a intenção.
