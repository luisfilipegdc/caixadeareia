# Caixa de Areia Interativa

Sistema nativo Windows que lê o relevo de uma caixa de areia com um Kinect e projeta
sobre ela um mapa topográfico colorido com curvas de nível, atualizado em tempo real
conforme os alunos moldam a areia.

**Projeto Caixa de Areia** — Brasília, DF · 2026 ·
Licenciado sob [GPL-2.0-or-later](LICENSE)

`v1.3` · [Página do projeto](https://luisfilipegdc.com.br/caixa-de-areia) ·
[Suporte](mailto:contato@luisfilipegdc.com.br) ·
[Repositório](https://github.com/luisfilipegdc/caixadeareia)

- **Sensor:** Kinect v1 / Kinect for Windows (modelo 1517), via API nativa NUI do SDK 1.8
- **Plataforma:** .NET 8 + WPF, x64
- **Renderização:** CPU (Parallel.For), ~30 fps a 640×480

### Documentação

| Documento | O que traz |
|---|---|
| 🌐 **[Página do projeto](docs/PROJETO.md)** | Visão geral, resultados medidos e arquitetura — para quem chega sem contexto |
| 📖 **[Manual do usuário](docs/MANUAL.md)** | Da instalação à primeira aula, sem pressupor conhecimento técnico |
| 🗺️ **[Roadmap](ROADMAP.md)** | As nove etapas até a plataforma de simulações ambientais, com status de cada item |
| 📐 **[Montagem física](docs/MONTAGEM-FISICA.md)** | Cálculo da altura do sensor, cobertura do campo de visão e throw ratio do projetor |
| 📓 **[Diário de bordo](docs/DIARIO-DE-BORDO.md)** | O registro da construção: decisões, justificativas, os seis bugs e as medições |
| 🖼️ **[Catálogo de imagens](docs/img/README.md)** | Capturas de cada etapa, com legenda e contexto |

---

## Baixar

**[⬇ CaixaInterativa-v1.3-win-x64.exe](https://github.com/luisfilipegdc/caixadeareia/releases/latest/download/CaixaInterativa-v1.3-win-x64.exe)** — 68 MB · Windows 10/11 64 bits

Arquivo único, **não precisa instalar nada**. O .NET já vai embutido; só o
[Kinect SDK 1.8](https://www.microsoft.com/en-us/download/details.aspx?id=40278) é
necessário à parte, porque traz o driver do sensor.

> Na primeira execução o Windows pode exibir um aviso do SmartScreen, por ser um
> executável sem certificado de assinatura comercial. Clique em *Mais informações* →
> *Executar assim mesmo*.

Todas as versões: [Releases](https://github.com/luisfilipegdc/caixadeareia/releases)

---

## Hardware detectado nesta máquina

```
USB\VID_045E&PID_02BE   Microsoft Kinect Audio
USB\VID_045E&PID_02BF   Microsoft Kinect Camera   <-- driver atual: libusb-win32
USB\VID_045E&PID_02AD   Xbox Kinect Audio          (entrada órfã de instalação anterior)
```

`PID_02BE/02BF` identifica um **Kinect for Windows (1517)** — bom, porque é o único modelo
que suporta *near mode* (0,4–3,0 m em vez de 0,8–4,0 m). Numa caixa de areia com o sensor
a cerca de 1 m, near mode é a diferença entre leitura limpa e bordas cortadas.

---

## Pré-requisitos

> **Status nesta máquina: tudo já instalado e verificado.**
> .NET 8 SDK 8.0.424 · Kinect SDK 1.8 (`Kinect10.dll 1.8.0.595`) · câmera migrada do
> `libusb-win32` para o driver **Kinect for Windows**, status OK · captura real validada
> a 26 fps. Os passos abaixo ficam registrados para reinstalação em outra máquina.

### 1. .NET 8 SDK

A máquina tem apenas o *runtime*. Para compilar é preciso o SDK:

```bash
winget install --id Microsoft.DotNet.SDK.8 --source winget
```

### 2. Kinect for Windows SDK 1.8

Baixe de `https://www.microsoft.com/en-us/download/details.aspx?id=40278` e instale.
Ele coloca a `Kinect10.dll` em `C:\Windows\System32` — é essa DLL que o projeto chama
por P/Invoke. Não é preciso instalar o Developer Toolkit.

### 3. Devolver a câmera ao driver da Microsoft

**Este passo é obrigatório nesta máquina.** A câmera está vinculada ao `libusb-win32`,
resíduo de um projeto anterior baseado em libfreenect/OpenNI. Enquanto isso não mudar,
`NuiGetSensorCount` retorna zero e o app não verá o sensor.

1. Abra o Gerenciador de Dispositivos (`devmgmt.msc`)
2. Em **libusb-win32 devices**, localize *Microsoft Kinect Camera*
3. Botão direito → **Desinstalar dispositivo** → marque *Excluir o software de driver*
4. Desconecte e reconecte o Kinect
5. O driver da Microsoft (instalado no passo 2) assume; o dispositivo deve reaparecer
   em **Kinect for Windows → Kinect for Windows Camera**

Se o libusb voltar sozinho, use *Atualizar driver → Procurar no computador → Escolher
numa lista* e selecione explicitamente o driver Kinect da Microsoft.

> As entradas duplicadas de áudio (`02AD` com status *Unknown*) são inofensivas —
> resíduo de registro do mesmo aparelho. Podem ser removidas com o Gerenciador de
> Dispositivos mostrando dispositivos ocultos.

### 4. Porta USB

O Kinect v1 consome quase toda a banda de um controlador USB 2.0. Se aparecer
*"Banda USB insuficiente"*, mude para uma porta ligada a **outro controlador** — em
notebooks, normalmente as portas de lados opostos do chassi. A fonte de energia externa
é obrigatória; só o cabo USB não alimenta o sensor.

---

## Compilar e executar

```bash
dotnet build src/CaixaInterativa/CaixaInterativa.csproj -c Release
```

```bash
dotnet run --project src/CaixaInterativa/CaixaInterativa.csproj -c Release
```

---

## Montagem física

```
            [ Projetor ]        [ Kinect ]
                  \                 |
                   \                |   ~0,9 a 1,2 m
                    \               |
        ┌────────────────────────────────────┐
        │            areia                   │
        └────────────────────────────────────┘
```

- Kinect **perpendicular** à caixa, centralizado, entre 0,9 m e 1,2 m acima da areia nivelada
- Projetor o mais próximo possível do mesmo eixo — quanto mais oblíquo, mais distorção
  de perspectiva, que o alinhamento afim atual **não** corrige (veja *Limitações*)
- Camada de areia de 8–15 cm, para que haja o que escavar e o que empilhar
- Areia clara e fosca lê melhor no infravermelho; evite areia molhada e superfícies brilhantes
- Sala com pouca luz solar direta — o sol tem infravermelho suficiente para cegar o sensor

---

## Uso

### No dia a dia

Abra pelo atalho **Caixa Interativa**. O programa liga o sensor sozinho e carrega a
calibração da última vez — o relevo aparece sem você tocar em nada.

O **semáforo no topo** diz o que está acontecendo e o que fazer:

| Luz | Significado |
|---|---|
| 🟢 Pronto | Tudo funcionando |
| 🟡 Nivele a areia e toque em Calibrar | Lendo o sensor, mas ainda sem referência |
| 🔵 Calibrando | Não mexa na areia |
| 🟡 Reconectando | O sensor caiu; religa sozinho |
| 🔴 Erro | Precisa de atenção |

### Primeira vez, ou depois de mexer no sensor

1. **Ligar a caixa**
2. **Abrir projeção** e alinhar: tecle **G** para a grade, ajuste com as setas, **S** para salvar
3. Alise a areia, tire as mãos e toque em **Nivelar e calibrar**
4. Confira a cobertura — abaixo de 80% o programa avisa o que verificar
5. Ajuste *Altura das montanhas* até as cores cobrirem o relevo que os alunos conseguem fazer

A calibração fica salva. Nas próximas aulas, basta abrir.

Só é preciso recalibrar se o sensor ou a caixa forem movidos.

### Ensaiar sem hardware

Em **Ajustes técnicos → Usar simulador**. Para reproduzir o fluxo completo de calibração,
marque *Simulador: areia plana*, calibre, e desmarque — o relevo sintético aparece já
calibrado. Foi assim que a pipeline foi validada antes de haver sensor.

### Atalhos na janela de projeção

| Tecla | Ação |
|---|---|
| Setas | mover (Shift = 10×) |
| `+` / `-` | escala uniforme |
| Ctrl + Setas | escala X / Y separadas |
| `R` / `E` | girar |
| `H` / `V` | espelhar horizontal / vertical |
| `G` | grade de alinhamento |
| `C` | calibrar plano-base |
| `S` | salvar configuração |
| `F1` | mostrar/ocultar ajuda |
| `Esc` | fechar projeção |

---

## Como funciona

```
Kinect ──► KinectV1Source ──► DepthProcessor ──► TopographicRenderer ──► ProjectionWindow
           (P/Invoke NUI)     (mm → altura)      (cor + curvas)          (WriteableBitmap)
```

### Três armadilhas do interop NUI

Todas custaram depuração e estão documentadas no código para não voltarem:

1. **`NuiImageStreamGetNextFrame` devolve um ponteiro, não a struct.** A assinatura da API
   flat é `CONST NUI_IMAGE_FRAME **ppcImageFrame` — diferente do método homônimo da
   interface `INuiSensor`, que preenche a struct por valor. Declarar `out NuiImageFrame`
   faz o runtime escrever apenas os 8 bytes do ponteiro no início da struct; o resto fica
   com lixo, o `pFrameTexture` aparente vira endereço inválido e o processo morre com
   **0xC0000374 (heap corruption)** na primeira leitura. O sintoma não aponta para a causa.

2. **A profundidade vem deslocada 3 bits, mesmo em `NUI_IMAGE_TYPE_DEPTH`.** Os bits
   reservados ao índice de jogador continuam presentes: os milímetros estão nos bits 15..3.
   Sem o `>> 3`, todas as distâncias saem 8× maiores — e o sinal de que é isso, e não outra
   coisa, é que *todos* os valores lidos são múltiplos de 8 e o máximo é exatamente
   `0x1FFF << 3 = 65528`.

3. **`NUI_IMAGE_STREAM_FLAG_ENABLE_NEAR_MODE` é `0x00020000`.** `0x00040000` é
   `TOO_FAR_IS_NONZERO`. Trocar os dois não gera erro — `NuiImageStreamSetImageFrameFlags`
   retorna `S_OK` de qualquer forma — e o sintoma engana: o alcance mínimo permanece em
   800 mm e tudo mais perto lê zero, exatamente como se a superfície não devolvesse
   infravermelho. Medido na mesa: com a flag errada, 6,9% de cobertura e mínimo de 801 mm;
   com a correta, **66,4% e mínimo de 455 mm**.

O retorno de `SetImageFrameFlags` não prova que o near mode foi aplicado. A verificação
confiável é empírica: com near mode ativo aparecem leituras abaixo de 800 mm.

Se algo parecido reaparecer, o caminho que funcionou foi: preencher o buffer com `0xCD`
antes da chamada nativa e conferir quantos bytes foram de fato escritos, e validar a vtable
com `BufferLen()`/`Pitch()` — que retornam inteiros conhecidos (614400 e 1280) sem escrever
memória, portanto não travam o processo se os slots estiverem errados.

**`DepthProcessor`** é onde mora a diferença entre uma projeção utilizável e uma que
"ferve". O Kinect v1 tem ~2–4 mm de ruído nessa distância e produz pixels inválidos nas
bordas dos objetos. Três etapas, nesta ordem:

1. **Buracos** — pixel inválido mantém o último valor bom, em vez de virar zero.
   Zerar criaria crateras piscando nas bordas das mãos.
2. **Tempo** — filtro exponencial com α adaptativo. Areia parada usa α lento (estável);
   uma mão entrando produz salto acima do limiar e usa α rápido (responsivo). Um α único
   obrigaria a escolher entre tremor e arrasto.
3. **Espaço** — box blur separável, custo O(1) por pixel independente do raio.

O **plano-base é armazenado por pixel**, não como um número único. Assim uma caixa
levemente torta ou um sensor não perfeitamente perpendicular não vira um gradiente falso
atravessando o mapa inteiro.

---

## Limitações conhecidas

- **Alinhamento apenas afim.** Escala, deslocamento, rotação e espelhamento. Se o projetor
  estiver bem oblíquo em relação à caixa, sobra distorção de perspectiva que só uma
  homografia de 4 cantos corrige. É o próximo passo natural.
- **Sem simulação de água.** O MVP é topografia. Água/chuva é iterativa e pede GPU —
  provavelmente um shader HLSL ou migração da renderização para um pipeline D3D.
- **Sem correção da distorção da lente** do Kinect. Nas bordas do campo de visão há erro
  de alguns milímetros; irrelevante para uso pedagógico, relevante para medição.
- **Faixa de leitura útil.** Com `MaxValidDepthMm = 2000`, qualquer coisa além de 2 m é
  descartada. Se o sensor for montado mais alto que isso, ajuste o valor em `config.json`.

---

## Estrutura

```
src/CaixaInterativa/
├── Depth/
│   ├── IDepthSource.cs          contrato de fonte de profundidade
│   ├── NuiNative.cs             P/Invoke para Kinect10.dll
│   ├── KinectV1Source.cs        captura real
│   └── SimulatedDepthSource.cs  relevo sintético, sem hardware
├── Processing/
│   └── DepthProcessor.cs        calibração, buracos, suavização
├── Rendering/
│   └── TopographicRenderer.cs   rampa hipsométrica, curvas, sombreamento
├── Config/
│   └── AppConfig.cs             persistência em config.json
├── Views/
│   ├── MainWindow.xaml          painel de controle
│   └── ProjectionWindow.xaml    tela cheia no projetor
└── SandboxEngine.cs             orquestração
```

---

## Suporte

| | |
|---|---|
| **Página do projeto** | https://luisfilipegdc.com.br/caixa-de-areia |
| **Suporte** | [Enviar e-mail](mailto:contato@luisfilipegdc.com.br) |
| **Repositório** | https://github.com/luisfilipegdc/caixadeareia |
| **Versão atual** | 1.0.1 |
| **Licença** | [GPL-2.0-or-later](LICENSE) |

Esses mesmos endereços estão dentro do programa, no bloco **Ajuda e suporte** do painel —
para que o professor os encontre durante a aula, sem precisar procurar aqui.

Ao relatar um problema, ajuda muito informar:

- a **versão** (aparece no título da janela e no rodapé do painel)
- o que o **semáforo de estado** mostrava no momento
- a **cobertura da calibração**, se o problema for no mapa
- o modelo do Kinect e se o *near mode* estava ligado

O link "Falar com o suporte" dentro do programa já preenche o assunto do e-mail com a
versão, para poupar essa primeira pergunta.

---

## Autoria e licença

**Caixa de Areia Interativa**
Copyright © 2026 Projeto Caixa de Areia — Brasília, DF

Este é um **projeto autoral**, inspirado em iniciativas acadêmicas anteriores. A autoria
não depende de negar as referências: ela está nas decisões de arquitetura, na
implementação, nos testes com hardware real, na adaptação ao contexto escolar brasileiro
e no planejamento pedagógico.

O que foi construído especificamente para este projeto:

- Aplicação nativa em C# / .NET 8 com WPF
- Captura do Kinect implementada diretamente pela API NUI, por P/Invoke
- Pipeline próprio de calibração por pixel, suavização em três etapas e renderização
- Interface, diagnóstico e fluxo de operação desenhados para uso em sala de aula
- Cálculo da geometria de montagem a partir da estrutura física real
- Documentação técnica e roteiro pedagógico

### Referências reconhecidas

| Referência | Contribuição histórica |
|---|---|
| [Augmented Reality Sandbox](https://arsandbox.ucdavis.edu/) — UC Davis / KeckCAVES | Conceito de medir o relevo com sensor de profundidade e projetar topografia e água |
| Caixa e-Água — Universidade Regional de Blumenau (FURB), 2017 | Aplicação universitária brasileira baseada em Vrui, Kinect e SARndbox |
| Magic-Sand | Porte parcial do SARndbox para openFrameworks/Windows |

Estas iniciativas estabeleceram o conceito. A implementação aqui é independente — não
deriva do código-fonte de nenhuma delas.

### Licença

Distribuído sob a **Licença Pública Geral GNU, versão 2 ou posterior**
(GPL-2.0-or-later). O texto completo está em [LICENSE](LICENSE).

Em termos práticos, você pode usar, estudar, modificar e redistribuir este software,
inclusive em escolas e projetos próprios. Em contrapartida, **trabalhos derivados devem
permanecer sob a mesma licença e preservar os avisos de autoria** — foi essa a escolha:
manter o projeto aberto e garantir que continue aberto.

Se usar este trabalho em pesquisa, material didático ou apresentação, a citação sugerida é:

> **Caixa de Areia Interativa**: plataforma de projeção topográfica para ensino de
> geografia e ciências ambientais. Versão 1.0.1. Projeto Caixa de Areia, Brasília, 2026.
> Disponível em: https://luisfilipegdc.com.br/caixa-de-areia

Este programa é distribuído na esperança de que seja útil, mas **sem qualquer garantia**;
sem sequer a garantia implícita de comercialização ou adequação a uma finalidade
específica. Consulte a Licença Pública Geral GNU para mais detalhes.
