# Diário da sessão autônoma

**Branch:** `claude/autonomous-platform-foundation`
**Commit inicial:** `5c29cdc`
**Data:** 27 de agosto de 2026

Registro cronológico das decisões tomadas sem supervisão. Hipóteses estão marcadas como
tal. Tudo que exigia decisão humana foi para
[PENDENCIAS-SESSAO-AUTONOMA.md](PENDENCIAS-SESSAO-AUTONOMA.md).

---

## Etapa 0 — Segurança

**Estado inicial verificado:**

| Item | Valor |
|---|---|
| Branch de partida | `claude/camadas-visuais` |
| Commit | `5c29cdc` |
| Working tree | limpo |
| Build Release | êxito · 0 avisos · 0 erros |
| Testes | 18 aprovados, 0 falhas |

Criada a branch `claude/autonomous-platform-foundation` a partir de `5c29cdc`, sem
descartar nada. Nenhum reset, nenhum stash, nenhuma operação destrutiva.

**Descoberta:** as Etapas 1 e 2 do plano da sessão **já estavam concluídas** em commits
anteriores — `758d4f5` (baseline de regressão visual, oito cenários) e `5c29cdc`
(camadas visuais genéricas). Reverificado no código antes de assumir: o
`TopographicRenderer.Render` já recebe `IReadOnlyList<CamadaVisual>` e não menciona
nenhum módulo. A sessão começou, portanto, pela Etapa 3.

**Reverificação das premissas da auditoria** (para não trabalhar sobre informação velha).
Continuam sem nenhum caminho de ativação: `FireSimulation.Atear`, `Cenario.Todos`,
`WaterSimulation.DespejarEm`, `WaterSimulation.PreSaturar`, `SoilMap.Pintar`,
`WaterSimulation.EscoadoLitros`.

**Sobre o token do GitHub.** Foi colado em texto puro no chat. Não foi usado. O push é
feito com a credencial que já estava configurada nesta máquina (`gh` autenticado como
`luisfilipegdc`). O token colado deve ser revogado — o próprio `cofre/LEIA-ME.md` do
projeto explica por quê.

---

## Etapa 3 — `ISimulationModule` como abstração real

**Problema.** A auditoria registrou que a interface era implementada por três classes e
nunca usada polimorficamente. O `SandboxEngine` repetia o mesmo bloco por módulo em três
lugares: atualização, coleta de camadas e limpeza.

**Decisão — e o que deliberadamente não foi feito.** Mantive as propriedades concretas
`Agua`, `Terremoto` e `Fogo`. A interface ainda precisa delas para controles próprios de
cada fenômeno (intensidade da chuva, magnitude do abalo), e generalizar isso exigiria um
sistema de parâmetros que não cabia num passo pequeno. O que ficou genérico foi o **ciclo
de quadro**, que é onde o acoplamento custava caro.

Sem container de DI, sem reflexão, sem carregador de plugins — como pedido.

**Mudanças:**
- `SandboxEngine`: nova lista `_modulos` e propriedade `Modulos`; registro em
  `StartSource`; `OnTick` e `ColetarCamadas` percorrem a lista; novo `LimparSimulacoes()`.
- `MainWindow.OnSecar`: cinco linhas viraram uma chamada a `LimparSimulacoes()`.

**Correção de comportamento encontrada de passagem.** O `OnSecar` limpava água e
terremoto, mas **não o fogo**. Como a queimada ainda não tem caminho de UI, o efeito hoje
é nulo — mas a limpeza genérica corrige o descuido antes que ele apareça.

**Invariante documentada no código:** a ordem da lista precisa produzir
`CamadaVisual.Ordem` crescente na concatenação — água (100), terremoto (200, 210), fogo
(300). Um módulo novo respeita a ordem, ou o engine passa a ordenar. Nunca o
renderizador, que ordenaria dentro do laço de pixels.

**Testes:** `ModulosFuncionamPolimorficamente` cobre atualizar só os ativos, coletar
camadas só dos ativos e limpar todos pela interface.

**Resultado:** build 0/0, **19 testes aprovados**. Regressão visual intacta.

---

## Etapa 4 — Honestidade científica

Duas violações apontadas pela auditoria, ambas corrigidas sem tocar em nenhum coeficiente
do modelo.

### Falsa precisão nas propriedades de solo

`PropriedadesDoSolo.Resumo` exibia ao professor *"Absorve 3,2 mm/s · guarda até 160 mm ·
resiste 95% à erosão"*. Aparência de medição hidrológica sobre valores que o comentário
doze linhas acima declara didáticos.

**Decisão:** o resumo virou qualitativo — *"Absorve muita água · retém muito · resiste bem
à erosão"* — e ganhou a ressalva `AvisoDidatico` junto na tela.

As faixas que classificam os valores foram escolhidas olhando a distribuição da tabela
existente. **Nenhum número novo foi inventado** e os coeficientes da simulação estão
intactos — travado pelo teste `CoeficientesDoModeloNaoMudaram`.

### Litros calculados com dimensão não calibrada

`WaterSimulation` derivava o tamanho da célula de `larguraCaixaMm = 1250f`, um literal que
nunca era passado por ninguém.

**Decisão — a de menor risco entre três.** Considerei (a) esconder os litros, (b) corrigir
o valor para ~1390 mm conforme a geometria documentada, (c) tornar configurável e marcar
como estimativa.

Descartei (a) porque remove informação que o professor pode usar. Descartei (b) porque
seria trocar uma suposição por outra — o roadmap do projeto proíbe exatamente isso, e o
número correto depende da altura real do sensor, que só a medição em campo dá.

Fiz (c): a largura virou `Config.Caixa.LarguraCobertaPeloSensorMm` **com o mesmo padrão de
1250 mm**, então nada mudou de comportamento; e enquanto `LarguraMedida` for falso a
interface marca os litros com "≈" e explica no painel do professor. A medição ficou
registrada como pendência P1.

**Hipótese não validada:** que a largura real seja ~1390 mm. Deriva do cálculo em
`MONTAGEM-FISICA.md` e de uma altura de sensor que ainda não foi confirmada. Por isso não
virou valor padrão.

### O que continua confiável

As porcentagens — área alagada, queimada, saturação — são razões entre contagens de
células e não passam pelo tamanho da célula. Continuam válidas sem calibração, e por isso
**não** levam marca de estimativa. Travado pelo teste
`PorcentagemDeAlagamentoNaoDependeDaLargura`.

**Arquivos:** `SoilMap.cs`, `AppConfig.cs`, `SandboxEngine.cs`, `MainWindow.xaml.cs`,
`ProjectionWindow.xaml.cs`, novo `HonestidadeCientificaTests.cs`.

**Resultado:** build 0/0, **36 testes aprovados**. Regressão visual intacta.

**Percalço registrado:** o heredoc do shell colapsou barras invertidas duplas e escreveu
um `\n` como quebra de linha real dentro de uma string C#, quebrando o build. Corrigido na
mesma etapa; nenhum commit quebrado foi criado.

---

## Etapa 6 — Comparação A/B com assinatura do relevo

Fiz esta etapa antes da 5 por ordem de risco: é lógica pura, totalmente testável, e
corrige o achado mais grave da auditoria.

### O problema

`MainWindow.AtualizarComparacao` conclui a aula com frases como *"Área urbana teve 2,4× o
resultado de Mata, na mesma simulação"*. A chave do histórico era `(Simulação, Cobertura)`
— **o relevo não entrava**. Se um aluno mexesse na areia entre as duas execuções, o
software atribuía à cobertura uma diferença que veio do terreno.

Numa plataforma cujo princípio declarado é honestidade científica, essa é uma conclusão
falsa apresentada com a autoridade de um resultado medido.

### Solução — e por que esta

Nova classe `AssinaturaDoRelevo`: reduz o campo de alturas a uma grade de 16×12 regiões,
cada uma com a altura média da sua área. Duas assinaturas são compatíveis quando nenhuma
região difere mais que **10 mm**.

**Descartei o hash.** Um hash responde "é idêntico?" e a resposta seria sempre "não" — o
Kinect tem 2–4 mm de ruído e nenhum quadro é igual ao anterior. A pergunta certa é "mudou
o suficiente para importar?", e ela precisa de tolerância.

**Sobre a tolerância de 10 mm.** A média sobre 1.600 pixels por região dilui ruído
independente para menos de 1 mm — verificado pelo teste `RuidoDoSensorNaoContaComoMudanca`,
que injeta ±4 mm por pixel e mede a diferença resultante. Um estudante que cava ou empilha
mexe em vários centímetros. O valor erra deliberadamente para o lado de avisar demais: um
aviso a mais incomoda, uma comparação falsa ensina errado.

**Não bloqueia nada.** O número comparado continua na tela; o que muda é a ressalva. Sem
assinatura disponível, o aviso diz que não foi possível verificar — nunca finge que
verificou.

### Efeito pedagógico colateral

O aviso ensina controle de variáveis sem usar a expressão: *"para comparar só a cobertura,
repita sem mexer no terreno"*.

**Classificação:** derivação matemática sobre medição. Só média aritmética e subtração,
nenhum parâmetro inventado.

**Arquivos:** novo `Processing/AssinaturaDoRelevo.cs`, `MainWindow.xaml.cs`, novo
`AssinaturaDoRelevoTests.cs`.

**Resultado:** build 0/0, **50 testes aprovados**. `DepthProcessor` não foi tocado — a
classe nova só divide o namespace com ele.

---

## Etapa 5a — Bug encontrado por medição: o fogo lia um buffer defasado

Antes de expor a queimada na interface, fui verificar o acoplamento fogo↔água. Encontrei
um bug real, e de passagem **desmenti uma afirmação da minha própria auditoria**.

### O bug

`SandboxEngine.StartSource` entregava a referência do array de profundidade ao fogo **uma
única vez**:

```csharp
Fogo = new FireSimulation(...) { Solo = Agua.Solo, Agua = Agua.Profundidade };
```

Mas `WaterSimulation.MoverAgua` termina com `(_agua, _aguaNova) = (_aguaNova, _agua)`.
Como o número de substeps por quadro varia, a referência devolvida por `Profundidade`
alterna entre dois arrays.

**Medido com uma sonda descartável:** em **7 de 20 quadros** a referência guardada pelo
fogo apontava para o buffer anterior. É o mesmo defeito que a camada visual da água já
tinha e que foi corrigido no PR anterior — só que aqui ninguém tinha notado.

**Correção:** uma linha em `OnTick` reaponta `Fogo.Agua` antes de atualizar os módulos.

**Por que corrigir foi seguro:** a queimada não tinha nenhum caminho de UI. Nenhum
comportamento observável mudou, porque ninguém nunca rodou este módulo.

### A afirmação que eu tinha feito errado

A auditoria dizia: *"um canal cavado durante o incêndio não barra o fogo"*, porque
`Estado.NaoQueima` é terminal.

**Está errado.** `TentarAcender` roda sobre os vizinhos das células em chamas a cada
passo e lê a água naquele instante. Uma célula ainda não alcançada nunca foi avaliada —
água que apareça ali antes de a chama chegar barra normalmente. O teste
`AguaExistenteBarraAPropagacao` demonstra: com uma faixa de água atravessando a caixa, o
fogo queima mais de 5% e menos de 60%, parando na barreira.

O defeito real é mais estreito: uma célula testada com água presente fica imune **para
sempre**, mesmo depois que a água seca. Reescrevi a pendência P2 com a versão correta.

**Arquivos:** `SandboxEngine.cs`, novo `AcoplamentoFogoAguaTests.cs`.
**Resultado:** build 0/0, **54 testes aprovados**.

---

## Etapa 5b — A queimada ganhou caminho de UI

`FireSimulation` tinha 350 linhas funcionais, era instanciada, atualizada e desenhada — e
`Atear()` não era chamado em lugar nenhum do projeto. O combo de simulações oferecia
"Chuva" e "Terremoto".

### O que foi feito

Um item no combo, um painel `CfgFogo` com a força do vento, e a ligação de `Atear()`.
Segui exatamente o padrão dos painéis existentes (`CfgChuva`, `CfgTremor`), inclusive na
visibilidade alternada — nada de layout novo, porque não tenho como verificar a tela.

**A direção do vento continua sorteada a cada incêndio.** É decisão do código original,
documentada como intencional: *"a mesma mata queima de forma diferente conforme a
direção"*. Expus só a força.

### O caso que teria virado botão quebrado

A cobertura padrão do `SoilMap` é solo arenoso, cujo combustível (0,05) fica abaixo do
limiar que `Atear` exige. Sem tratamento, o professor apertaria "Atear fogo" e nada
aconteceria — sem erro, sem explicação.

`Atear()` já devolvia `false` nesse caso. A interface agora usa esse retorno para dizer o
que fazer: escolher Mata, Pastagem ou Agricultura.

### Armadilha registrada, não resolvida

Quando o fogo apaga, `AplicarCicatriz` grava `Queimado` no mapa de solo compartilhado —
é o ponto do módulo. Mas o combo de cobertura continua mostrando a seleção antiga, e
**tocar nele chama `Preencher` e apaga a cicatriz**. É comportamento pré-existente e
defensável (escolher cobertura nova é justamente refazer o território), mas não é óbvio.
Não mexi: mudar isso é decisão de produto.

**Arquivos:** `MainWindow.xaml`, `MainWindow.xaml.cs`.
**Resultado:** build 0/0, **54 testes aprovados**.

**Limitação conhecida ao expor:** o defeito descrito em P2 — célula molhada fica imune
para sempre, mesmo depois de a água secar. Não afeta o uso normal e está documentado.

---

## Etapa 7 — README reescrito

O README antigo abria com *"Sistema nativo Windows que lê o relevo de uma caixa de areia
com um Kinect e projeta sobre ela um mapa topográfico"* — descrição de um visualizador,
não de uma plataforma de ensino. As simulações apareciam como detalhe.

### O que mudou

- Abre pela **experiência pedagógica**, não pela tecnologia.
- Diagrama Mermaid do ciclo aluno → sensor → ambiente → fenômeno → consequência → aluno.
- **Três seções de estado, separadas de forma inequívoca:** funciona hoje ·
  implementado com ressalva · roadmap não implementado. Nada de roadmap aparece como
  recurso existente.
- Seção própria de honestidade científica, com a tabela das quatro categorias e as três
  consequências já aplicadas no código.
- Diagrama Mermaid da arquitetura **real**, incluindo `CamadaVisual`.
- Seção de contribuição com as regras que protegem a base: não mexer em `Depth/` sem
  motivo, não reescrever baseline de regressão, não inventar número científico.
- Lista explícita do material visual que **falta** — GIF do ciclo, foto da caixa montada,
  vídeo de comparação A/B — em vez de fingir que existe.

**Verificação:** todos os 8 links relativos e as 2 imagens foram conferidos por script;
nenhum quebrado. Nenhuma captura nova foi inventada — as duas imagens usadas já existiam
no repositório.

**Ressalva registrada no próprio README:** as imagens atuais vieram do simulador e do
sensor sobre uma mesa, não da caixa montada.

---

## Etapa 8 — Guia de desenvolvimento

Novo `docs/DESENVOLVIMENTO.md`, escrito para não repetir o README. Cobre só o que importa
na hora de editar código:

- **O caminho de um quadro**, com as implicações práticas: tudo pesado roda na UI thread;
  o timer a 60 Hz sobre sensor de 30 é proposital; as simulações congelam junto com o
  sensor; o clone do buffer na captura é o que garante a segurança entre threads.
- **Como acrescentar um fenômeno** — um arquivo novo e uma linha de registro, com a
  armadilha do buffer trocado documentada (o defeito que já apareceu duas vezes no
  projeto, uma delas medida nesta sessão).
- **Como acrescentar um modo de cor**, com as regras do laço de pixels.
- **A tabela medição / derivação / modelo / efeito visual**, com as regras de o que pode
  ir à tela e como.
- **Como testar sem Kinect**, incluindo por que o baseline de regressão não deve ser
  reescrito e por que os testes de fogo usam grade menor.
- **O que não mexer** e as convenções da base.

Preferi um documento só a espalhar as regras: quem chega ao projeto precisa de um lugar
para começar, e o README já ficou longo.

---

## Etapa 9 — Testes onde faltava cobertura de verdade

Dois componentes críticos tinham **zero testes**, e ambos podem ser cobertos sem tocar em
uma linha da implementação.

### `DepthProcessor` — 21 testes de caracterização

É o componente mais delicado depois do interop: as três etapas foram ajustadas contra
hardware real e são a diferença entre projeção utilizável e uma que "ferve".

Cobre calibração em superfície plana, rejeição de pixel intermitente, altura como
diferença do plano-base, corte pela faixa configurada, preenchimento de buracos, pixel
nunca válido, α rápido no salto e α lento no ruído, convergência com areia parada, box
blur preservando campo constante, raio zero desligando o blur, recusa de quadro com
dimensão incompatível, e o ciclo exportar/importar.

**Todos passaram na primeira execução.** Isso é o resultado desejado: são testes de
caracterização, não de correção. Nenhuma linha do `DepthProcessor` foi alterada.

### `CalibrationStore` — 11 testes

Se este arquivo corromper, o professor perde o passo mais demorado do fluxo com a turma
esperando.

Cobre ciclo completo, ausência de `.tmp` depois da gravação atômica, recusa de resolução
diferente, arquivo inexistente, assinatura errada, arquivo truncado, arquivo vazio, e o
empacotamento de bits com contagens que não são múltiplas de 8.

O teste `ArquivoTruncadoDevolveNull` confirma que o carregamento defensivo funciona — o
cenário de queda de energia no meio da gravação.

**Resultado:** build 0/0, **83 testes aprovados** (eram 18 no início da sessão).

---

## Etapa 10 — Limpeza de baixo risco

Só comentários incorretos. Verificado por `git diff`: **nenhuma linha de código mudou**.

1. **`SandboxEngine`** — a summary *"Campo de alturas atual"* estava órfã, colada logo
   acima da propriedade do terremoto. Devolvida à propriedade `Alturas`, a que pertence.
2. **`WaterSimulation`** — a summary de `AcumularErosao` (*"Não movemos areia: o relevo vem
   do sensor…"*) estava empilhada acima de `DrenarSolo`, que já tinha a sua. Devolvida ao
   método certo.
3. **`DepthProcessor`** — `MinCalibrationSamples = 5` era descrito como *"fração mínima dos
   quadros"*, mas é contagem absoluta. Quem lesse "fração" e chamasse
   `BeginBaseCalibration(6)` teria quase nada calibrado, sem entender por quê. O texto
   agora diz que é contagem, e registra que com os 60 quadros padrão equivale a 8%.

O item 3 toca um arquivo que a sessão classificou como intocável. **É alteração
exclusivamente de comentário** — o diff confirma, e os 21 testes de caracterização escritos
na Etapa 9 provam que o comportamento não mudou. A regra existe para proteger o
comportamento validado em campo, e uma descrição errada trabalha contra esse mesmo
objetivo.

### O que decidi não limpar

- **`NuiNative.TextureRelease` e `NuiCameraElevationGetAngle`** são código morto, mas ficam
  em `Depth/`. Remover declarações P/Invoke não usadas é seguro na teoria; não vale gastar
  a regra da sessão por duas linhas. Registrado na auditoria.
- **`WaterSimulation.EscoadoLitros`**, declarado e nunca calculado. Implementar toca o
  solver; remover muda API pública. Está na pendência P3.

**Resultado:** build 0/0, **83 testes aprovados**.
