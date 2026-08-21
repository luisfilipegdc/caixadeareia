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
| Salvar e carregar calibrações | ⬜ | `SnapshotBasePlane`/`RestoreBasePlane` já existem em `DepthProcessor`, mas não estão ligados à persistência nem à interface. É o item de maior impacto: hoje, cada abertura exige recalibrar. |
| Assistente de configuração inicial | ⬜ | Guiar posicionamento, calibração e alinhamento na primeira execução |
| Mostrar cobertura e qualidade da leitura | ✅ | `CoveragePercent` no painel, com aviso abaixo de 80% |
| Detectar automaticamente a perda do Kinect | 🟡 | Evento `Faulted` existe e é reportado; falta **reconexão automática** |
| Melhorar a suavização da profundidade | ✅ | Três etapas: buracos, α adaptativo, box blur separável. Revisitar com areia real |
| Criar modo de diagnóstico | ⬜ | Existe como testes avulsos; falta trazer para dentro do app |
| Testar por longos períodos | ⬜ | Verificar vazamento de memória e estabilidade em sessões de horas |
| Garantir inicialização simples | 🟡 | Atalho na Área de Trabalho criado; falta **auto-iniciar a fonte salva** |
| Registrar desempenho e erros | ⬜ | Sem log em arquivo hoje |
| Criar backup das configurações | ⬜ | Cópia versionada do `config.json` e do plano-base |

**Legenda:** ✅ pronto · 🟡 parcial · ⬜ não iniciado

### Prioridade dentro da Fase 1

1. **Persistir o plano-base** — sem isso, o marco da fase não é atingível
2. **Auto-iniciar a fonte salva** — completa o "abrir e funcionar"
3. **Reconexão automática do sensor** — um cabo esbarrado não pode encerrar a aula
4. **Log em arquivo** — sem ele, um problema em sala vira relato sem evidência

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

# Fase 4 — Água, chuva e enchentes

**Marco:** os estudantes constroem um território, provocam uma chuva e investigam por que
determinadas áreas alagam.

- [ ] Adicionar água em um ponto
- [ ] Criar chuva em toda a área
- [ ] Calcular o sentido do escoamento
- [ ] Formar rios, lagos e áreas alagadas
- [ ] Controlar intensidade e duração da chuva
- [ ] Exibir profundidade da água por cores
- [ ] Comparar relevos antes e depois
- [ ] Criar obstáculos, barragens e canais
- [ ] Simular ocupação urbana
- [ ] Testar intervenções contra enchentes

> **Decisão técnica pendente:** a simulação de água é iterativa e provavelmente exige GPU.
> A renderização atual é em CPU, escolha adequada para colorização mas insuficiente para
> um solucionador de águas rasas em tempo real. Esta fase deve começar por essa decisão de
> arquitetura, antes de qualquer código de simulação.

---

# Fase 5 — Solos, infiltração e erosão

**Marco:** a mesma chuva produz consequências diferentes dependendo do solo e da ocupação.

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

# Fase 7 — Terremotos e fenômenos geológicos

O relevo físico representa o território; a projeção mostra ondas, intensidade, risco e
consequências.

- [ ] Epicentro e ondas sísmicas
- [ ] Intensidade conforme a distância
- [ ] Diferentes tipos de solo
- [ ] Amplificação das ondas em solo menos resistente
- [ ] Áreas de risco
- [ ] Falhas geológicas
- [ ] Vulcões
- [ ] Fluxo de lava
- [ ] Deslizamentos
- [ ] Estabilidade das encostas

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
