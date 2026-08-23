# Roadmap — Caixa de Areia Interativa

## Visão

Evoluir a Caixa de Areia Interativa de um sistema de visualização topográfica para uma
**plataforma autoral de simulações ambientais, geográficas e climáticas voltada ao ensino**.

## Três horizontes

| Horizonte | Foco |
|---|---|
| **Agora** | Estabilizar o núcleo e montar a caixa física |
| **Próxima versão** | Água, enchentes e tipos de solo |
| **Longo prazo** | Laboratório de fenômenos ambientais e geológicos |

---

## Onde estamos — agosto de 2026

**Versão 1.3.** As simulações avançaram muito além do previsto para esta altura, e as
etapas de campo ficaram para trás. Vale registrar o desvio em vez de escondê-lo.

| Fase | Situação |
|---|---|
| 1. Estabilização | 🟡 O marco foi atingido — abre e funciona. Faltam log em arquivo, modo de diagnóstico, teste de longa duração e backup |
| 2. Montagem física | ⬜ **Não iniciada.** A caixa existe, mas o sistema nunca rodou sobre areia real |
| 3. Topografia 2.0 | ⬜ **Não iniciada.** Sem homografia, sem legenda de altitude, sem captura de imagem |
| 4. Água e enchentes | ✅ Completa, com saturação do solo |
| 5. Solo e erosão | ✅ Completa, doze coberturas |
| 6. Clima e temperatura | ⬜ Não iniciada |
| 7. Fenômenos geológicos | 🟡 Terremoto pronto; vulcão e fluxo de lava não |
| 8. Camada pedagógica | 🟡 Seis cenários e dois roteiros no manual; falta o alinhamento com a BNCC |
| 9. Plataforma aberta | 🟡 Documentação, manual e releases públicos; falta instalador e biblioteca compartilhada |

Um módulo entrou fora do plano: **a queimada**, que não estava em nenhuma fase. Ela se
justificou porque fecha o ciclo do módulo de solo — o fogo altera a cobertura, e a chuva
seguinte encontra outro território.

### O desvio, e por que ele importa

Este documento estabelece, na seção *Princípio de execução*, que a prioridade era
**concluir a estabilidade e a montagem física antes de abrir frentes de simulação**.
Não foi o que aconteceu: as Fases 2 e 3 foram puladas, e as Fases 4, 5 e 7 foram
construídas primeiro.

A decisão foi consciente e faz sentido — o software podia avançar enquanto a caixa não
estava disponível, e as simulações são o que dá sentido ao projeto. Mas ela cobra um preço
que precisa estar visível:

> **As três simulações foram validadas apenas sobre terreno sintético.** Nenhuma delas
> rodou sobre areia de verdade, lida pelo sensor, numa caixa montada.

O que só aparece na areia real:

- **Sombra de infravermelho das mãos.** Enquanto o aluno molda, a mão bloqueia o sensor e
  cria uma região sem leitura. A água vai reagir a isso — e ninguém sabe como ainda.
- **Ruído da areia real.** Areia espalha infravermelho de forma diferente de uma superfície
  lisa. A suavização pode precisar de outro ajuste.
- **Escala do relevo.** Os limiares de alagamento e as alturas de cor foram calibrados em
  terreno sintético. Uma caixa com 10 cm de areia tem outra amplitude.
- **Alinhamento sob projeção real.** O mapa pode estar certo e a projeção, deslocada.

Nada disso invalida o que foi feito: a física está verificada, com massa conservada e
comportamento coerente. Mas **verificado em simulação não é o mesmo que validado em
campo**, e o roadmap não deve sugerir que está.

### Prioridade recomendada agora

1. **Fase 2 — rodar sobre a caixa real.** É a única forma de descobrir o que os testes não
   pegam, e destrava tudo o que vem depois.
2. **Fase 1 — log em arquivo.** Sem ele, um problema durante uma aula vira relato sem
   evidência. É o que torna a Fase 2 diagnosticável.
3. **Fase 3 — homografia**, se o projetor não puder ficar perpendicular à caixa.
4. Só então novos módulos.

---

## Etapas

| # | Etapa | Objetivo | Principais entregas |
|---|---|---|---|
| 1 | Estabilização | Funcionamento confiável | Captura, calibração, desempenho, configuração, recuperação de erros |
| 2 | Montagem física | Integrar sensor, areia e projetor | Caixa, pórtico, projetor adequado, calibração real |
| 3 | Topografia 2.0 | Melhorar a leitura do relevo | Homografia, correção de lente, curvas configuráveis, legenda |
| 4 | Água e enchentes | Primeira simulação dinâmica | Chuva, escoamento, acúmulo, rios, áreas inundadas |
| 5 | Solo e erosão | Simular diferentes superfícies | Infiltração, permeabilidade, erosão, sedimentos |
| 6 | Clima e temperatura | Fenômenos atmosféricos | Temperatura, seca, El Niño, eventos extremos |
| 7 | Fenômenos geológicos | Ciências da Terra | Terremotos, vulcões, movimentos do terreno |
| 8 | Camada pedagógica | Simulações viram aulas | Roteiros, objetivos, perguntas, desafios, BNCC |
| 9 | Plataforma aberta | Evolução contínua | Novos modos, documentação, instalação simplificada, comunidade |

---

## Princípio de execução

> **Um modo completo por vez.** O erro seria tentar construir terremoto, El Niño, enchente
> e temperatura simultaneamente. O caminho é criar uma **arquitetura de módulos** e
> desenvolver um modo inteiro — simulação, interface e material pedagógico — antes de
> começar o próximo.

**Água e Enchentes deve ser o primeiro grande módulo**, porque exercita toda a base de que
os módulos seguintes dependem: mapa de altura, fluxo, acúmulo, infiltração, erosão e
intervenção humana.

---

## Ordem recomendada

1. Estabilidade do Kinect e do programa
2. Caixa física e projetor
3. Calibração avançada
4. Água e enchentes
5. Tipos de solo e erosão
6. Camada pedagógica do primeiro modo
7. Temperatura e clima
8. Terremotos e geologia
9. Expansão da biblioteca de aulas

---

# Fase 1 — Estabilizar o núcleo

**Marco:** ligar o computador, abrir o programa e ter o relevo funcionando sem precisar
alterar código.

| Item | Status | Observação |
|---|---|---|
| Salvar e carregar calibrações | ✅ | `CalibrationStore` grava plano-base e máscara de validade em binário. Salva ao calibrar, carrega ao ligar. |
| Assistente de configuração inicial | 🟡 | O painel de ajuda já orienta o passo seguinte conforme o estado; falta um assistente guiado de primeira instalação. |
| Mostrar cobertura e qualidade da leitura | ✅ | Cobertura no painel, com aviso explicando o que verificar abaixo de 80%. |
| Detectar automaticamente a perda do Kinect | ✅ | Evento `Faulted` mais reconexão automática a cada 3 s, sem limite de tentativas. |
| Melhorar a suavização da profundidade | ✅ | Três etapas: buracos, α adaptativo, box blur separável. Revisitar com areia real. |
| Criar modo de diagnóstico | ⬜ | Existe como testes avulsos; falta trazer para dentro do app. |
| Testar por longos períodos | ⬜ | Verificar vazamento de memória e estabilidade em sessões de horas. |
| Garantir inicialização simples | ✅ | Abre, liga a fonte salva e carrega a calibração sozinho. Atalho na Área de Trabalho. |
| Registrar desempenho e erros | ⬜ | Sem log em arquivo hoje. |
| Criar backup das configurações | ⬜ | Cópia versionada do `config.json` e da calibração. |

**Legenda:** ✅ pronto · 🟡 parcial · ⬜ não iniciado

### Prioridade dentro da Fase 1

O marco — *"abrir o programa e ter o relevo funcionando"* — **foi atingido**: o programa
abre, liga a fonte salva, carrega a calibração e mostra o relevo sem intervenção.
Verificado com duas execuções consecutivas do executável.

O que resta da fase, em ordem:

1. **Log em arquivo** — sem ele, um problema em sala vira relato sem evidência
2. **Modo de diagnóstico dentro do app** — hoje só existe como testes avulsos
3. **Teste de longa duração** — verificar vazamento de memória numa sessão de horas
4. **Backup da configuração e da calibração**
5. **Assistente de primeira instalação** — guiar o posicionamento do sensor

---

# Fase 2 — Validar a caixa física

**Marco:** modificar a areia com as mãos e ver a projeção acompanhar corretamente o relevo.

- [ ] Conferir medidas da caixa
- [ ] Definir profundidade e quantidade de areia
- [ ] Validar altura do Kinect
- [ ] Escolher o projetor pela relação de projeção
- [ ] Verificar sombras causadas pelas mãos
- [ ] Construir um pórtico rígido
- [ ] Proteger sensor e projetor
- [ ] Organizar cabos e alimentação
- [ ] Testar iluminação da sala
- [ ] Calibrar com a areia real

### Parâmetros já determinados pelo software

| Parâmetro | Valor | Origem |
|---|---|---|
| Altura do sensor | 0,9–1,2 m | Near mode alcança 0,4–3,0 m; medido 455 mm de mínimo real |
| Profundidade de areia | 8–15 cm | Precisa haver o que cavar e o que empilhar |
| Faixa de leitura aceita | até 2000 mm | `MaxValidDepthMm` — sensor mais alto exige ajustar `config.json` |
| Cobertura alvo | acima de 90% | Abaixo de 80% o app já avisa |

### Riscos conhecidos a validar em campo

- **Sombra de infravermelho das mãos.** O Kinect projeta IR de um ponto; mãos criam
  oclusão. Documentar o comportamento e decidir se compensa com preenchimento temporal.
- **Sol na sala.** A luz solar tem IR suficiente para cegar o sensor.
- **Areia úmida ou brilhante.** Reflexão especular derruba a cobertura.
- **Rigidez do pórtico.** Qualquer deslocamento do sensor invalida a calibração.

---

# Fase 3 — Topografia 2.0

**Marco:** o sistema vira uma ferramenta confiável para ensinar relevo, altitude, curvas de
nível e bacias hidrográficas.

- [ ] Calibração pelos quatro cantos
- [ ] Homografia para corrigir perspectiva
- [ ] Correção da distorção da lente
- [ ] Legenda de altitude
- [ ] Escolha do intervalo das curvas de nível *(já ajustável ao vivo no painel)*
- [ ] Marcação de picos, vales e depressões
- [ ] Diferentes paletas hipsométricas
- [ ] Escala de altitude relativa
- [ ] Captura de imagem do relevo
- [ ] Comparação entre dois terrenos

> **Nota técnica:** a homografia é a limitação conhecida mais relevante hoje. O alinhamento
> atual é afim — escala, deslocamento, rotação, espelhamento — e não corrige perspectiva.
> Se o pórtico permitir montar o projetor aproximadamente perpendicular à caixa, essa etapa
> pode ser adiada sem prejuízo.

---

# Fase 4 — Água, chuva e enchentes ✅

**Marco atingido** — em terreno sintético. Falta a validação sobre areia real.

- [x] Criar chuva em toda a área, com intensidade e duração configuráveis
- [x] Calcular o sentido do escoamento
- [x] Formar rios, lagos e áreas alagadas
- [x] Exibir profundidade da água por cores
- [x] Simular ocupação urbana (via cobertura do solo)
- [x] Testar intervenções (comparação entre cenários)
- [x] **Saturação do solo** — não estava previsto, e é o que explica a enchente real:
      o solo enche, para de absorver, e a chuva seguinte alaga mais
- [ ] Adicionar água num ponto escolhido pelo professor
- [ ] Obstáculos e barragens desenhados na interface
- [ ] Comparação lado a lado de dois relevos

> **Decisão técnica pendente:** a simulação de água é iterativa e provavelmente exige GPU.
> A renderização atual é em CPU, escolha adequada para colorização mas insuficiente para
> um solucionador de águas rasas em tempo real. Esta fase deve começar por essa decisão de
> arquitetura, antes de qualquer código de simulação.

---

# Fase 5 — Solos, infiltração e erosão ✅

**Marco atingido:** a mesma chuva produz consequências diferentes conforme o solo.
Medido — mata alaga 7,5%, desmatado alaga 51,2%, com a mesma chuva.

Doze coberturas implementadas, cada uma com infiltração, capacidade de armazenamento,
rugosidade e resistência à erosão. A erosão é calculada como previsão, não aplicada ao
relevo: mexer no terreno faria o mapa divergir do que está fisicamente na caixa.

Cada região da caixa poderá receber propriedades distintas:

| Tipo de superfície | Propriedades a modelar |
|---|---|
| Solo arenoso | Alta infiltração, baixa retenção |
| Solo argiloso | Baixa infiltração, alta retenção |
| Solo compactado | Infiltração reduzida |
| Área impermeabilizada | Escoamento total |
| Vegetação | Retenção, redução de erosão, resfriamento |

- [ ] Capacidade de infiltração
- [ ] Saturação do solo
- [ ] Escoamento superficial
- [ ] Erosão
- [ ] Transporte e deposição de sedimentos

---

# Fase 6 — Temperatura e fenômenos climáticos

Nesta fase, parte das simulações passa a ser baseada em **modelos didáticos**, não apenas
na forma física da areia.

- [ ] Mapa de temperatura
- [ ] Ilhas de calor
- [ ] Vegetação e resfriamento
- [ ] Seca e umidade do solo
- [ ] Massas de ar simplificadas
- [ ] Aquecimento dos oceanos
- [ ] El Niño e La Niña
- [ ] Eventos extremos
- [ ] Elevação do nível do mar
- [ ] Cenários climáticos comparativos

> **Honestidade científica.** Para o El Niño, é importante **não fingir que a caixa
> reproduz todo o sistema climático**. Ela pode apresentar uma simulação didática
> simplificada, mostrando relações entre temperatura do Pacífico, circulação atmosférica e
> mudanças nos padrões de chuva.
>
> Recomendação: cada modo desta fase deve declarar explicitamente na interface o que é
> medição do relevo real e o que é modelo didático. Um estudante que sai da aula achando
> que a caixa "calculou" o El Niño aprendeu algo errado.

---

# Fase 7 — Terremotos e fenômenos geológicos 🟡

O relevo físico representa o território; a projeção mostra ondas, intensidade, risco e
consequências.

- [x] Epicentro e ondas sísmicas
- [x] Intensidade conforme a distância
- [x] Amplificação das ondas em solo menos resistente
- [x] Áreas de risco (mapa de dano acumulado)
- [x] Deslizamentos e estabilidade das encostas
- [ ] Epicentro escolhido clicando no mapa
- [ ] Falhas geológicas
- [ ] Vulcões e fluxo de lava

**Medido:** magnitude 7 sobre solo solto dá 22,99% de risco de deslizamento numa encosta,
0,00% em terreno plano, e 0,00% numa encosta com mata.

---

# Fase 8 — Camada pedagógica

**É a parte que diferencia o projeto de uma demonstração tecnológica.**

Cada modo deverá trazer:

- Tema
- Faixa etária
- Objetivos de aprendizagem
- Conhecimentos prévios
- Tempo estimado
- Materiais
- Pergunta-problema
- Etapas da experiência
- Hipóteses
- Desafios para os estudantes
- Perguntas para discussão
- Formas de avaliação
- Relação com a BNCC
- Registro do antes e do depois

### O que muda na prática

Um modo não seria apenas *"Enchente"*, mas uma experiência:

> **Construa uma cidade capaz de resistir a uma chuva extrema sem transferir o problema
> para outra região.**

> **Implicação de arquitetura:** se cada modo carrega metadados pedagógicos, eles precisam
> ser dados, não código. Um formato declarativo — um arquivo por modo — permite que
> professores criem e compartilhem experiências sem recompilar. Vale desenhar isso já na
> Fase 4, quando o primeiro módulo real for construído.

---

# Fase 9 — Plataforma aberta

- [ ] Arquitetura de módulos documentada
- [ ] Novos modos como plugins ou arquivos declarativos
- [ ] Documentação de instalação simplificada
- [ ] Instalador que dispense conhecimento técnico
- [ ] Biblioteca compartilhada de aulas
- [ ] Comunidade de professores

---

## Arquitetura de módulos — proposta

A decisão de "um modo completo por vez" só funciona se a base for extensível desde já.
A estrutura atual já separa captura, processamento e renderização; um módulo de simulação
encaixa entre os dois últimos:

```
IDepthSource → DepthProcessor → [ ISimulationModule ] → IRenderer → ProjectionWindow
   sensor        campo de           estado do            camadas      projetor
                 alturas            fenômeno             visuais
```

Um módulo receberia o campo de alturas calibrado e devolveria camadas de visualização,
mantendo estado próprio entre quadros. O mapa topográfico atual passa a ser simplesmente
o primeiro módulo — o que valida o desenho antes de escrever o segundo.

**Momento de fazer essa refatoração:** no início da Fase 4, junto com a decisão sobre GPU.
Antes disso seria abstração especulativa; depois, retrabalho.

---

## Estado atual

Consulte o **[diário de bordo](docs/DIARIO-DE-BORDO.md)** para o registro completo da
construção — decisões, bugs e medições — e o **[catálogo de imagens](docs/img/README.md)**
para as capturas de cada etapa.

**Pronto e verificado:** captura do Kinect v1 a 20–29 fps com near mode ativo, calibração
de plano-base por pixel com relatório de cobertura, suavização em três etapas, mapa
topográfico com curvas de nível e sombreamento, janela de projeção alinhável e persistida,
simulador completo para ensaio sem hardware.

**Próximo passo imediato:** persistir o plano-base e auto-iniciar a fonte salva — os dois
itens que faltam para atingir o marco da Fase 1.
