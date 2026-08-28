# Relatório da sessão de validação de campo

**Branch:** `claude/autonomous-platform-foundation`
**Commit inicial:** `d039695` · **Commit final:** ver item 21
**Data:** 28 de agosto de 2026

---

## O bloqueio, de novo

**O Kinect continua desconectado.** Verificado por dois caminhos independentes antes de
qualquer trabalho:

| Nível | Resultado |
|---|---|
| PnP (`Get-PnpDevice`) | Os três nós (`PID_02BE`, `02BF`, `02AD`) presentes, **todos com `Status: Unknown`** — entradas fantasma, não hardware |
| `NuiGetSensorCount` | `HRESULT = 0x00000000` (S_OK), **`sensores = 0`** |

Chamei a função nativa direto da `Kinect10.dll`, sem passar pelo aplicativo, para não
depender do código do projeto na verificação.

Isso bloqueou as Etapas 2 a 10, que dependem inteiramente do sensor. A Etapa 1 era
explicitamente "antes de conectar hardware", e é onde a sessão se concentrou.

---

## 1. Screenshots da UI em 1366×768

| Antes | Depois |
|---|---|
| [`21-sidebar-antes-1366x768.png`](img/21-sidebar-antes-1366x768.png) | [`22-sidebar-depois-1366x768.png`](img/22-sidebar-depois-1366x768.png) |

**Antes:** a barra lateral mostrava apenas calibração e terreno. A seção "SIMULAÇÕES" não
aparecia. Medido por UI Automation: o botão de executar ficava em **y=862** numa tela de
**768** — 94 pixels abaixo da borda.

**Depois:** todos os sete controles essenciais da lista de prioridade visíveis sem rolar.

```
Ligar a caixa        y=113   base=163   VISIVEL
Abrir projeção       y=173   base=223   VISIVEL
Calibrar de novo     y=319   base=369   VISIVEL
combo cobertura      y=471   base=498   VISIVEL
combo simulação      y=517   base=544   VISIVEL
Fazer chover         y=555   base=605   VISIVEL
Limpar simulação     y=611   base=641   VISIVEL
```

### O que foi feito

Três mudanças, nenhuma reduzindo fonte nem escondendo controle:

1. **Barra de ação fixa fora do `ScrollViewer`**, presa ao rodapé da sidebar: seletor de
   simulação, executar e limpar. Sempre alcançáveis, independentemente da rolagem.
2. **"Aparência do mapa"** — três deslizadores de ajuste fino que o professor mexe uma vez
   e esquece — foi para dentro do expander "Ajustes técnicos", onde já moram os controles
   secundários.
3. **A instrução de calibração some depois de calibrada.** Já cumpriu o papel, e devolve
   duas linhas de altura.

### Também verificado

| Tamanho | Resultado |
|---|---|
| 1366×768 | ✅ todos os controles essenciais visíveis |
| 1440×900 | ✅ |
| 960×560 (mínimo declarado da janela) | ✅ botões de ação continuam visíveis |

**DPI diferente de 100% não foi testado** — exigiria alterar a configuração do Windows da
sua máquina, o que está fora do que eu deveria mexer. Fica registrado como não verificado.

---

## 2. Screenshots da projeção

| Arquivo | O que mostra |
|---|---|
| [`23-projecao-grade.png`](img/23-projecao-grade.png) | Tela cheia com a grade de alinhamento (10 divisões, bordas vermelhas) e o painel de atalhos |
| [`24-projecao-queimada.png`](img/24-projecao-queimada.png) | Fogo em propagação com o painel de dados: área queimada, em chamas agora, direção do vento |

**Primeira validação visual da janela de projeção.** Dois pontos que importam:

- **A grade de alinhamento existe e é legível.** É ela que o procedimento de calibração
  física usa para ler que fração do quadro os marcadores ocupam — o método está
  fundamentado, não é hipótese.
- **O painel de dados é legível de longe:** tipografia grande, poucos números
  (`ÁREA QUEIMADA 5%`, `EM CHAMAS AGORA 6,7%`, `Vento de nordeste · 6s`).

---

## 3–8. Kinect, FPS, profundidade, near mode, timeout, reconexão

**Todos bloqueados.** Sem sensor:

| Item | Estado |
|---|---|
| Sensor detectado | Não — 0 sensores |
| PID / modelo | Entradas de `PID_02BE/02BF` presentes mas fantasma |
| Status do driver | `Unknown` nos três nós |
| FPS real do Kinect | Não medido. Com simulador: **16–17 fps** |
| Profundidade mínima | Não medida |
| Cobertura válida | Não medida com sensor |
| Near mode confirmado | **Não.** O diagnóstico está implementado e testado em lógica (9 testes), e verificado que fica calado no simulador — mas nunca viu um sensor |
| Timeout | **Não validado em campo.** Implementado com 8 testes; limite de 3 s registrado no código como ponto de partida conservador |
| Reconexão | Não exercitada |

O FPS de 16–17 do simulador não deve ser lido como o FPS do sistema: o simulador gera
relevo em CPU num laço próprio, e a máquina estava rodando captura de tela e automação em
paralelo.

---

## 9–15. Calibração real, largura física, ROI, células, litros

**Todos bloqueados pela ausência do sensor.**

| Item | Estado |
|---|---|
| Cobertura de calibração | 100% **com o simulador** — não diz nada sobre areia real |
| Largura física medida | Não. `larguraCobertaPeloSensorMm` continua **1250 mm**, `larguraMedida: false` |
| ROI | Não medida, não configurada |
| mm/pixel horizontal | Não medido |
| mm/pixel vertical | Não medido |
| Erro da hipótese de célula quadrada | Continua a **estimativa** de 3,38%, derivada do FOV nominal. **Não confirmada por medição** |
| Litros revalidados | Não. Continuam marcados com "≈" na interface |

A Etapa 8 (UI de ROI) dependia da Etapa 7 provar que a ROI resolve o problema. Como a
Etapa 7 não pôde ser feita, **não implementei o editor** — construir um editor de ROI sem
poder verificar o resultado contra uma caixa real produziria algo que parece pronto e não
foi verificado contra nada.

---

## 16. Resultado do teste de sala

Executei os passos 1 a 13 com o simulador. Os passos 14 e 15 (desconectar e reconectar)
dependem do sensor.

| Passo | Resultado |
|---|---|
| 1. Abrir aplicação | ✅ |
| 2. Iniciar sensor | ✅ com ressalva — sem Kinect, aparece um diálogo perguntando se quer o simulador |
| 3. Calibrar | ✅ 100% de cobertura, salva sozinha |
| 4. Abrir projeção | ✅ tela cheia; avisou que só há um monitor e pediu confirmação |
| 5. Criar relevo com a mão | ⛔ não aplicável ao simulador |
| 6. Selecionar chuva | ✅ agora sem rolagem |
| 7. Executar | ✅ |
| 8–9. Observar | ✅ "A água escoou. Pico de alagamento: 48% · Infiltrado: ≈ 43,9 L" |
| 10. Parar | ✅ |
| 11. Selecionar queimada | ✅ |
| 12. Iniciar fogo | ✅ frente de fogo visível na projeção |
| 13. Barreira de água | ⚠️ não observável nesta rodada — a água da chuva já tinha infiltrado quando o fogo começou |
| 14–15. Desconectar / reconectar | ⛔ bloqueado |

### Pontos de fricção encontrados

1. **O painel de atalhos aparece projetado sobre a areia** ao abrir a projeção. Faz
   sentido para alinhar, mas depois de alinhado o professor precisa lembrar de `F1` para
   escondê-lo. Está no manual; ainda assim é a primeira coisa que a turma vê.
2. **O seletor de cobertura ainda exige uma pequena rolagem** em 1366×768. Ele não estava
   na sua lista de controles essenciais, mas é pedagogicamente central — "mata contra
   cidade" é a comparação principal da aula.
3. **Testar a barreira de água exige coordenação de tempo** que não é óbvia: é preciso
   chover e atear fogo *antes* de a água infiltrar. Nada na interface sugere isso.

### "Um professor consegue fazer isso sem entender a arquitetura?"

**Para chuva, sim.** Ligar → calibrar → projetar → escolher → executar é um caminho de
cinco cliques, todos visíveis, com textos em linguagem de aula.

**Para a queimada, com uma pegadinha:** se a cobertura não tiver combustível, o programa
explica o que fazer — mas isso só funciona porque a cobertura agora é sincronizada de
verdade (corrigido na sessão anterior).

**Para a comparação água×fogo, não ainda.** O ponto 3 acima exige entender que a água
infiltra.

---

## 17. Commits

```
1e17e80 fix: make control panel usable at 1366x768
```

Um só. As demais etapas não produziram código porque dependiam do sensor.

## 18. Build final

```
dotnet build CaixaInterativa.sln -c Release   →  êxito · 0 avisos · 0 erros
```

## 19. Testes finais

```
dotnet test CaixaInterativa.sln -c Release    →  105 aprovados · 0 falhas
```

Nenhum teste novo: a mudança foi de layout XAML, que a suíte não cobre. A verificação foi
por medição de posição via UI Automation, registrada no item 1.

---

## 20. Pendências

| # | Pendência | Estado |
|---|---|---|
| P1 | Medir largura coberta pelo sensor | **Bloqueada** — procedimento pronto, falta sensor e fita |
| P3 | `EscoadoLitros` nunca calculado | Aberta — Etapa 10 condicionava à geometria validada |
| P4 | ROI sem interface | Aberta — Etapa 8 condicionava à Etapa 7 |
| P5 | Erosão calculada e descartada | Aberta |
| P6 | Sensor que emudece | Corrigido, **falta validar com hardware** |
| P7 | Near mode | Diagnóstico implementado, **falta validar com hardware** |
| P8 | Cenários pedagógicos | Aberta — depende de relevo real |
| P9 | Células não quadradas | Estimada em 3,38%, **falta confirmar por medição** |
| P10 | Duas pastas de saída de build | Documentada |
| **P11** | **Sidebar em 1366×768** | ✅ **corrigida nesta sessão** |
| P12 | Seletor de cobertura exige rolagem | **Nova** — ver item 16 |
| P13 | Painel de atalhos projetado na areia ao abrir | **Nova** — ver item 16 |

---

## 22–23. Log e diff

Ver itens 17 e, no repositório, `git diff --stat d039695..HEAD`.

---

## O que a próxima sessão precisa

**Uma coisa só: o Kinect conectado.**

Três sessões seguidas registraram o mesmo bloqueio. O software acumulou correções,
diagnósticos e procedimentos que só podem ser fechados com o sensor na mesa. Sem ele,
o que resta é trabalho de interface e documentação — e o mais valioso disso já foi feito.

Com o sensor conectado, a ordem é: medir a largura (P1) → medir a ROI (P4) → confirmar o
erro de célula (P9) → validar timeout e near mode em campo (P6, P7) → e só então litros e
`EscoadoLitros`.
