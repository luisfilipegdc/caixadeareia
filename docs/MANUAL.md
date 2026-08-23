# Manual do usuário

**Caixa de Areia Interativa** — da instalação à primeira aula.

Este manual não pressupõe conhecimento técnico. Se em algum ponto algo não funcionar
como descrito aqui, escreva para contato@luisfilipegdc.com.br.

---

## Sumário

1. [O que você precisa](#1-o-que-você-precisa)
2. [Instalação](#2-instalação)
3. [Montagem da caixa](#3-montagem-da-caixa)
4. [Primeira execução](#4-primeira-execução)
5. [Calibração](#5-calibração)
6. [Alinhar a projeção](#6-alinhar-a-projeção)
7. [O dia a dia](#7-o-dia-a-dia)
8. [As simulações](#8-as-simulações)
9. [Dando aula com a caixa](#9-dando-aula-com-a-caixa)
10. [Quando algo dá errado](#10-quando-algo-dá-errado)
11. [Perguntas frequentes](#11-perguntas-frequentes)

---

## 1. O que você precisa

### Equipamento

| Item | Detalhe |
|---|---|
| **Computador** | Windows 10 ou 11, 64 bits |
| **Kinect** | Kinect for Windows v1, **modelo 1517** de preferência |
| **Fonte do Kinect** | Obrigatória. O sensor **não liga** só pelo cabo USB |
| **Projetor** | Relação de projeção ≤ 1,17 (veja a seção de montagem) |
| **Caixa** | Com areia, e uma estrutura rígida sobre ela |

### Como saber qual Kinect você tem

Olhe a etiqueta embaixo do sensor:

- **Modelo 1517** — "Kinect for Windows". É o recomendado: enxerga a partir de 40 cm.
- **Modelo 1414** — "Kinect for Xbox 360". Funciona, mas só enxerga a partir de 80 cm,
  o que exige montar o sensor mais alto.

O programa detecta o modelo sozinho e avisa se o modo de proximidade não estiver
disponível.

### Areia

Areia **clara, seca e fosca** funciona melhor. O Kinect enxerga por infravermelho, e:

- areia molhada reflete mal e cria falhas na leitura
- areia escura absorve o infravermelho
- superfícies brilhantes espelham o feixe para longe do sensor

Areia de construção lavada e seca é uma escolha comum e barata.

---

## 2. Instalação

### Passo 1 — Baixar o programa

Baixe o arquivo mais recente:

**https://github.com/luisfilipegdc/caixadeareia/releases/latest**

É um arquivo único de cerca de 68 MB. **Não precisa instalar nada** — nem .NET, nem
bibliotecas. Basta salvar em qualquer pasta e executar.

> **O Windows vai avisar na primeira vez.** Aparece uma tela azul dizendo "O Windows
> protegeu o computador". Isso acontece porque o programa não tem certificado de
> assinatura comercial, que custa caro e é anual. Clique em **Mais informações** e depois
> em **Executar assim mesmo**.

### Passo 2 — Instalar o driver do Kinect

Este é o único passo que exige instalação de verdade, porque traz o driver do sensor.

Baixe o **Kinect for Windows SDK 1.8**:

**https://www.microsoft.com/en-us/download/details.aspx?id=40278**

Execute o instalador e siga adiante. Não é preciso instalar o "Developer Toolkit".

### Passo 3 — Conectar o Kinect

1. Ligue a **fonte de energia** do Kinect na tomada
2. Conecte o cabo USB no computador
3. Aguarde cerca de 30 segundos — a luz do sensor deve acender

> **Porta USB importa.** O Kinect v1 consome quase toda a banda de um controlador USB.
> Se o programa reclamar de *banda insuficiente*, mude para uma porta do **outro lado** do
> notebook: normalmente elas usam controladores diferentes.

### Passo 4 — Verificar

Abra o programa. No alto da janela há um semáforo:

- 🟡 **"Nivele a areia e toque em Calibrar"** — o sensor foi encontrado. Está tudo bem.
- 🔴 **"Kinect não encontrado"** — veja [Quando algo dá errado](#10-quando-algo-dá-errado).

---

## 3. Montagem da caixa

### O esquema

```
              [ Projetor ]      [ Kinect ]
                    \               |
                     \              |    altura H
                      \             |
        ┌──────────────────────────────────────┐
        │                areia                 │
        └──────────────────────────────────────┘
```

Os dois ficam presos numa estrutura **rígida** acima da caixa. Rigidez importa: se o
sensor se mexer depois de calibrado, o mapa sai errado e é preciso calibrar de novo.

### A que altura montar o Kinect

Depende do tamanho da sua caixa. O sensor enxerga um cone, então quanto mais alto, mais
área ele cobre — mas com mais ruído.

Para descobrir a altura mínima da **sua** caixa, meça o lado **menor** da área de areia e
divida por 0,79:

```
altura mínima = lado menor da caixa ÷ 0,79
```

Alguns exemplos:

| Lado menor da caixa | Altura mínima | Altura recomendada |
|---|---|---|
| 60 cm | 76 cm | 82 cm |
| 80 cm | 101 cm | 109 cm |
| **100 cm** | **127 cm** | **137 cm** |
| 120 cm | 152 cm | 164 cm |

A altura recomendada tem 8% de folga, para absorver imprecisão de montagem.

> **A orientação do sensor vale muito.** O Kinect enxerga mais no sentido do lado longo
> dele. Monte-o com **o lado longo do sensor paralelo ao lado longo da caixa**. Montado
> girado, a altura necessária sobe cerca de 25%.

### Profundidade da areia

**8 a 15 cm.** Menos que isso não dá para cavar; mais que isso é peso desnecessário.

### Escolha do projetor

O que importa é a **relação de projeção** (*throw ratio*) — quantos metros de distância o
projetor precisa para cada metro de largura de imagem.

Para uma caixa de 100 cm no lado menor, com o projetor a 1,35 m:

| Formato do projetor | Relação de projeção necessária |
|---|---|
| 4:3 | **≤ 1,17** |
| 16:10 | ≤ 0,97 |
| 16:9 | ≤ 0,87 |

> **Cuidado ao comprar.** Projetores comuns têm relação entre 1,4 e 1,6 — esses **não
> cobrem** a caixa. Procure por *short throw*. Se o projetor tiver zoom, o que vale é o
> **menor** número da faixa: um "1,2–1,6" serve; um "1,5–1,8" não.

### A sala

- **Evite sol direto** na caixa. A luz do sol tem infravermelho suficiente para cegar o
  sensor.
- Luz artificial normal não atrapalha.
- Quanto mais escura a sala, melhor a projeção aparece.

---

## 4. Primeira execução

Abra o programa. A janela tem três partes:

**No alto**, o semáforo — diz o que está acontecendo e o que fazer.

**À esquerda**, os controles.

**À direita**, a prévia: o que será projetado.

### Ligar

Toque em **▶ Ligar a caixa**.

O programa procura o Kinect e começa a ler. A prévia fica **verde uniforme** — isso está
correto: sem calibração, o sistema ainda não sabe onde é o fundo da caixa.

> **Sem Kinect?** O programa oferece um **simulador**, que reproduz o funcionamento sem o
> sensor. Serve para preparar a aula, testar a projeção e conhecer o sistema. Fica em
> *Ajustes técnicos → Usar simulador*.

---

## 5. Calibração

Calibrar é ensinar ao sistema onde é o "chão" da sua caixa. Sem isso, ele não sabe o que é
morro e o que é vale.

### Como fazer

1. **Alise a areia** deixando a superfície o mais plana possível
2. **Tire as mãos** de dentro da caixa — e peça que os alunos façam o mesmo
3. Toque em **Nivelar e calibrar**
4. Aguarde cerca de 2 segundos, sem mexer em nada

Pronto. A prévia passa a mostrar o relevo colorido.

### A cobertura

Ao terminar, o programa informa a **cobertura**: quanto da caixa ele conseguiu medir.

| Cobertura | O que significa |
|---|---|
| **Acima de 90%** | Ótimo |
| 80% a 90% | Aceitável |
| **Abaixo de 80%** | O programa avisa — algo está atrapalhando |

Se a cobertura estiver baixa, verifique:

- o sensor está **perpendicular** à caixa, não inclinado?
- há **sol** entrando na caixa?
- a areia está **molhada** ou muito escura?
- o sensor está na altura recomendada para o tamanho da sua caixa?

### A calibração fica salva

**Você só precisa calibrar uma vez.** Nas próximas aulas, abra o programa e ele carrega a
calibração sozinho — o relevo aparece sem você tocar em nada.

Recalibre apenas se:

- o sensor ou a caixa foram movidos
- você mudou a quantidade de areia
- a cobertura piorou sem explicação

---

## 6. Alinhar a projeção

Alinhar é fazer a imagem projetada coincidir com a caixa de verdade.

1. Toque em **🖵 Abrir projeção**
2. Escolha o monitor do projetor, se houver mais de um
3. Na janela que abriu, tecle **G** — aparece uma grade com borda vermelha
4. **Ajuste o projetor fisicamente** até a borda vermelha coincidir com a borda da caixa
5. Refine com o teclado (veja abaixo)
6. Tecle **S** para salvar
7. Tecle **G** de novo para tirar a grade

O alinhamento fica salvo. Nas próximas aulas não precisa repetir.

### Teclas da janela de projeção

| Tecla | O que faz |
|---|---|
| **Setas** | Move a imagem (segure Shift para mover 10× mais) |
| **+** e **−** | Aumenta e diminui a imagem |
| **Ctrl + Setas** | Estica em largura ou altura separadamente |
| **R** e **E** | Gira |
| **H** e **V** | Espelha na horizontal ou vertical |
| **G** | Grade de alinhamento |
| **D** | Mostra os números da simulação sobre a projeção |
| **C** | Calibra |
| **S** | Salva o alinhamento |
| **F1** | Mostra ou esconde a lista de teclas |
| **Esc** | Fecha a projeção |

> Se a imagem sair **espelhada** em relação à areia, tecle **H**. Isso é comum e depende de
> como o projetor está posicionado.

---

## 7. O dia a dia

Depois de configurado uma vez, o uso é:

1. Ligar o projetor e o computador
2. Abrir o programa pelo atalho
3. Pronto

O programa liga o sensor sozinho, carrega a calibração e mostra o relevo. Toque em **Abrir
projeção** e a aula pode começar.

### O semáforo

| Luz | Significado | O que fazer |
|---|---|---|
| 🟢 **Pronto** | Tudo funcionando | Nada |
| 🟡 **Nivele a areia e toque em Calibrar** | Lendo, mas sem referência | Calibrar |
| 🔵 **Calibrando** | Capturando o plano da areia | Não mexa na areia |
| 🟡 **Reconectando** | O sensor caiu | Verifique cabo e fonte; ele religa sozinho |
| 🔴 **Erro** | Precisa de atenção | Veja a mensagem na tela |

### Ajustar a aparência do mapa

Três controles mudam como o relevo aparece:

- **Altura das montanhas** — a partir de que altura a areia fica marrom e branca. Se os
  alunos não conseguem fazer picos brancos, diminua.
- **Profundidade dos vales** — a partir de que profundidade aparece água. Se tudo fica
  azul, aumente.
- **Linhas de altitude** — o espaçamento entre as curvas de nível.

Ajuste com a turma presente: os valores certos dependem de quanta areia há e de quão fundo
as crianças conseguem cavar.

---

## 8. As simulações

Todas funcionam sobre o relevo que os alunos moldam **e** sobre a cobertura do solo que
você escolhe.

### O terreno vem primeiro

Antes de simular, escolha em **Terreno** o que cobre o solo. São doze opções, da que mais
protege à que menos protege:

| Cobertura | Absorve | O que representa |
|---|---|---|
| **Mata** | muito | Floresta preservada |
| **Várzea** | muito | Planície alagável do rio |
| Pastagem | médio | Campo, pasto |
| Agricultura | médio | Lavoura |
| Solo arenoso | alto | Areia solta |
| Solo argiloso | baixo | Argila, satura rápido |
| Cidade drenada | médio | Piso permeável, jardins de chuva |
| Solo compactado | muito baixo | Pisoteado, de obra |
| Rocha exposta | quase nada | Afloramento rochoso |
| Desmatado | baixo | Solo exposto |
| **Queimado** | muito baixo | Crosta que repele água |
| **Área urbana** | nada | Asfalto e telhados |

**É aqui que está a lição.** A mesma chuva, sobre o mesmo relevo, produz resultados muito
diferentes conforme a cobertura.

### 🌧 Chuva e enchente

Escolha a intensidade e a duração, e toque em **Fazer chover**.

A chuva tem começo e fim. Durante ela, a água cai, escorre pelo relevo, acumula nos vales
e alaga as partes baixas. Quando para, você vê o escoamento — que é metade do fenômeno.

**O que observar:**
- por onde a água desce
- onde ela se acumula
- quanto o solo absorve
- o **solo encharcando**: repita a chuva e veja que a segunda alaga mais que a primeira,
  porque o solo já está cheio

### 🔥 Queimada

Toque em **Provocar queimada**. O fogo começa num ponto sorteado e se espalha.

**O que observar:**
- o fogo sobe encostas mais rápido do que desce
- o vento empurra a frente de fogo
- **um rio barra o fogo** — se houver água na caixa, ela contém o incêndio
- rocha e asfalto não queimam

**Depois que o fogo apaga, o solo muda.** A área queimada vira crosta que repele a água.
Faça chover em seguida e compare com antes: a diferença é grande.

### ⚡ Terremoto

Escolha a magnitude e toque em **Provocar terremoto**.

**O que observar:**
- a onda se espalha a partir do centro e enfraquece com a distância
- **o solo muda tudo**: solo mole amplifica o tremor, rocha não
- o **risco de deslizamento** exige três coisas juntas — tremor, encosta e solo que não
  segura. Terreno plano não desliza; encosta com mata também não.

### Comparar cenários

O quadro **Comparação** guarda o resultado de cada simulação. Rode a mesma chuva em duas
coberturas diferentes e ele diz quantas vezes uma alagou mais que a outra.

**É esse número que fecha a aula.**

---

## 9. Dando aula com a caixa

### O formato que funciona

1. **Os alunos constroem** o território com as mãos — vales, morros, rios
2. **Você faz uma pergunta** antes de simular: *onde vocês acham que a água vai chegar?*
3. **Executa a simulação** e todos observam
4. **Compara** com o que eles previram
5. **Muda uma coisa** — a cobertura do solo, a intensidade da chuva — e repete
6. **Discute** por que o resultado mudou

O passo 2 é o mais importante. Sem a previsão, vira demonstração; com ela, vira
investigação.

### Um roteiro pronto: por que aquele bairro alagou?

**Pergunta-problema:** *uma cidade foi construída no fundo de um vale. Por que ela alaga,
e o que poderia ser diferente?*

**Etapas:**

1. Peça que construam um vale com encostas dos dois lados
2. Escolha a cobertura **Mata** e faça chover. Anote a área alagada.
3. Troque para **Área urbana** e faça a **mesma** chuva. Anote de novo.
4. Mostre o quadro de comparação
5. Pergunte: *o que mudou? A chuva foi a mesma. O relevo foi o mesmo.*
6. Teste **Cidade drenada** e discuta o que muda numa cidade planejada

**O que os alunos descobrem:** a enchente não depende só de quanto chove. Depende de para
onde a água pode ir — e o que cobre o solo decide isso.

### Outro roteiro: o fogo e a chuva

1. Construam uma área com morros
2. Cobertura **Mata**, faça chover, anote
3. **Provoque uma queimada** e observe o fogo se espalhar
4. Faça a **mesma** chuva de novo
5. Compare erosão e área alagada

**O que descobrem:** o incêndio não termina quando o fogo apaga. A consequência continua
na próxima chuva.

### Perguntas para discussão

- Por que as cidades foram construídas justamente nas planícies que alagam?
- Se derrubarmos a mata da encosta, quem sente o efeito?
- O que uma cidade pode fazer para conviver com a chuva em vez de lutar contra ela?
- Por que dois bairros à mesma distância do epicentro sofrem danos tão diferentes?

---

## 10. Quando algo dá errado

### "Kinect não encontrado"

Na ordem:

1. **A fonte está ligada na tomada?** O Kinect não liga só pelo USB. É a causa mais comum.
2. **A luz do sensor está acesa?** Se não, é energia.
3. **Aguarde 30 segundos** depois de conectar — o sensor demora a inicializar.
4. **Instalou o SDK 1.8?** Sem ele, o Windows não tem o driver.
5. **Troque de porta USB**, preferencialmente do outro lado do computador.
6. Toque em **Procurar Kinect** em *Ajustes técnicos*.

### "Banda USB insuficiente"

O Kinect precisa de quase toda a banda de um controlador USB. Mude para uma porta que use
outro controlador — em notebooks, normalmente as de lados opostos. Desconecte outros
dispositivos USB que estejam em uso.

### A cobertura ficou baixa

O sistema conseguiu medir menos de 80% da caixa. Verifique:

- **Sol na caixa** — a causa mais comum. Feche a cortina.
- **Areia molhada** — deixe secar.
- **Sensor inclinado** — quanto mais perpendicular, melhor.
- **Sensor baixo demais** — confira a tabela de altura na seção 3.
- **Areia escura ou brilhante** — troque por areia clara e fosca.

### O mapa fica "fervendo" mesmo com a areia parada

Aumente a **suavização espacial** em *Ajustes técnicos → Estabilidade da leitura*. Valores
entre 3 e 5 costumam resolver.

### A projeção não coincide com a caixa

Tecle **G** na janela de projeção para ver a grade e ajuste com as setas. Se houver
distorção que as setas não corrigem — a imagem fica em formato de trapézio —, o projetor
está muito inclinado em relação à caixa. Aproxime-o do eixo vertical.

### A imagem está espelhada

Tecle **H** (espelha na horizontal) ou **V** (na vertical) na janela de projeção. Depois
**S** para salvar.

### O sensor caiu no meio da aula

O programa tenta religar sozinho, a cada 3 segundos. Verifique o cabo e a fonte — assim
que voltar, ele retoma sem precisar de nada.

### A projeção ocupou a tela do computador

Isso acontece quando só há um monitor. Tecle **Esc** para fechar a projeção.

---

## 11. Perguntas frequentes

**Preciso calibrar toda aula?**
Não. A calibração fica salva. Só recalibre se mover o sensor ou a caixa.

**Posso usar sem o Kinect?**
Sim, com o simulador — em *Ajustes técnicos → Usar simulador*. Serve para preparar a aula
e testar a projeção, mas não responde às mãos dos alunos.

**Preciso instalar o .NET?**
Não. Ele já vem dentro do programa.

**Funciona com Kinect v2 (o do Xbox One)?**
Ainda não. Esta versão suporta apenas o Kinect v1.

**Funciona em Mac ou Linux?**
Não. O programa depende do driver Kinect da Microsoft, que só existe para Windows.

**Os números das simulações são reais?**
São **didáticos**. Seguem a ordem de grandeza da literatura — mata infiltra muito, asfalto
não infiltra nada — mas foram escolhidos para que a diferença apareça numa aula de meia
hora. Servem para o estudante enxergar a relação, não para prever uma cheia real.

**A caixa simula o terremoto de verdade?**
Não. Ela não tem falhas geológicas nem camadas de subsuperfície. O terremoto é um modelo
das **consequências** — como o solo e a encosta mudam o dano — e não uma previsão sísmica.
Vale dizer isso à turma: a diferença entre medir e modelar é uma boa aula em si.

**Posso modificar o programa?**
Sim. É software livre sob licença GPL-2.0-or-later. O código está em
https://github.com/luisfilipegdc/caixadeareia — trabalhos derivados devem manter a mesma
licença e preservar os avisos de autoria.

**Onde peço ajuda?**
contato@luisfilipegdc.com.br — informe a versão, que aparece no rodapé do programa.

---

## Documentação relacionada

| Documento | Para quê |
|---|---|
| [Página do projeto](https://github.com/luisfilipegdc/caixadeareia/blob/main/docs/PROJETO.md) | Visão geral, arquitetura e resultados |
| [Montagem física](https://github.com/luisfilipegdc/caixadeareia/blob/main/docs/MONTAGEM-FISICA.md) | Cálculos de altura, campo de visão e projetor |
| [Roadmap](https://github.com/luisfilipegdc/caixadeareia/blob/main/ROADMAP.md) | O que vem pela frente |
| [Diário de bordo](https://github.com/luisfilipegdc/caixadeareia/blob/main/docs/DIARIO-DE-BORDO.md) | Como o sistema foi construído |
