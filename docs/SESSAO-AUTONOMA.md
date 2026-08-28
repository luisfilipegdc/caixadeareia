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
