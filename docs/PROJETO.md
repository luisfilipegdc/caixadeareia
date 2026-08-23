# Caixa de Areia Interativa

**Uma caixa com areia que responde ao toque, e transforma geografia em algo que se
constrói com as mãos.**

Um sensor lê o relevo que os estudantes moldam. Um projetor devolve, sobre a própria
areia, um mapa topográfico colorido com curvas de nível — e simulações de enchente,
queimada e terremoto que reagem ao território construído.

O aluno cava um vale e vê água aparecer. Empilha areia e vê a montanha ganhar curvas de
nível. Derruba a mata da encosta, provoca a mesma chuva de antes, e descobre que agora
alaga sete vezes mais.

**Versão 1.3** · Software livre sob GPL-2.0-or-later
[Baixar](https://github.com/luisfilipegdc/caixadeareia/releases/latest) ·
[Código-fonte](https://github.com/luisfilipegdc/caixadeareia) ·
[Manual do usuário](https://github.com/luisfilipegdc/caixadeareia/blob/main/docs/MANUAL.md)

![Mapa topográfico projetado sobre o relevo](https://raw.githubusercontent.com/luisfilipegdc/caixadeareia/main/docs/img/01-simulador-topografia.png)

*O mapa topográfico gerado a partir do relevo: azul nas depressões, areia na linha d'água,
verde nas planícies, marrom nas encostas, branco nos picos — com curvas de nível.*

---

## Por que existe

Mapas topográficos, bacias hidrográficas, erosão e risco ambiental costumam chegar aos
estudantes como figuras estáticas num livro. Curvas de nível são um dos conceitos que mais
resistem à explicação no quadro — porque exigem imaginar uma terceira dimensão que a
página não tem.

A caixa resolve isso pela via mais direta: a terceira dimensão passa a existir de verdade,
nas mãos de quem aprende.

Mas o objetivo não é o efeito visual. É a **investigação**: o estudante constrói um
território, faz uma previsão, testa, erra, muda uma variável e testa de novo. Sem essa
sequência, vira demonstração — e demonstração não ensina.

---

## O que o sistema faz

### Lê o relevo em tempo real

O Kinect mede a distância até cada ponto da areia, 640 × 480 vezes por quadro, entre 20 e
29 vezes por segundo. Uma calibração inicial ensina ao sistema onde é o fundo da caixa;
tudo acima disso vira relevo.

![Relevo real lido pelo Kinect](https://raw.githubusercontent.com/luisfilipegdc/caixadeareia/main/docs/img/08-relevo-calibrado.png)

*Leitura do sensor real: objetos sobre a superfície viram elevações e depressões, com as
curvas de nível acompanhando o contorno.*

### Projeta um mapa topográfico

Rampa hipsométrica clássica — azul nas depressões, areia na linha d'água, verde nas
planícies, marrom nas encostas, branco nos picos — com curvas de nível e sombreamento de
relevo. É a convenção dos atlas escolares, escolhida de propósito: o aluno já chega
sabendo ler.

### Simula fenômenos sobre o território construído

Três simulações, todas afetadas pela cobertura do solo que o professor escolhe.

**💧 Chuva e enchente.** A água cai, escorre pelo relevo, acumula nos vales e alaga as
partes baixas. Quando a chuva para, o escoamento continua — que é metade do fenômeno.

![Simulação de enchente em andamento](https://raw.githubusercontent.com/luisfilipegdc/caixadeareia/main/docs/img/11-chuva-em-andamento.png)

*Chuva em andamento: a água acumula nas depressões enquanto as elevações permanecem secas.*

**🔥 Queimada.** O fogo começa num foco sorteado e se espalha conforme combustível, vento
e encosta. Um rio o barra, como no território real. E quando apaga, **altera o solo**: a
área queimada vira crosta que repele a água.

**⚡ Terremoto.** Ondas sísmicas a partir do epicentro, com intensidade caindo pela
distância — e **amplificada pelo solo**. Solo mole vibra muito mais que rocha. O risco de
deslizamento exige três condições juntas: tremor, encosta e solo que não segura.

### Doze coberturas de solo

Mata, várzea, pastagem, agricultura, solo arenoso, argiloso, cidade drenada, solo
compactado, rocha, desmatado, queimado e área urbana. Cada uma com infiltração, capacidade
de armazenamento, rugosidade e resistência à erosão próprias.

![Tipos de cobertura do solo](https://raw.githubusercontent.com/luisfilipegdc/caixadeareia/main/docs/img/13-tipos-de-solo.png)

**É aqui que mora a lição.** A mesma chuva, sobre o mesmo relevo, produz resultados muito
diferentes conforme o que cobre o solo.

---

## Os números que a aula usa

Todos medidos pelo próprio sistema, sobre o mesmo terreno e a mesma chuva.

### Desmatar multiplica o alagamento

| Cobertura | Alagou | Absorveu | Erosão |
|---|---|---|---|
| **Mata** | **7,5%** | 61,9 L | 467 |
| Solo arenoso | 11,1% | 58,6 L | 27.895 |
| Desmatado | 51,2% | 26,8 L | 197.698 |
| Queimado | 58,6% | 13,0 L | 323.337 |
| **Área urbana** | **64,5%** | 0,7 L | 0 |

### O solo encharca e para de absorver

Três chuvas **iguais e seguidas** sobre o mesmo território:

| Chuva | Alagou | Absorveu | Solo cheio |
|---|---|---|---|
| 1ª | 56,8% | 16,21 L | 37,6% |
| 2ª | 60,6% | 10,19 L | 60,6% |
| 3ª | 62,7% | **6,43 L** | 74,3% |

É o que explica as enchentes reais: chove por dias, o solo satura, e aí a chuva seguinte
não tem para onde ir.

### O fogo não termina quando apaga

| | Alagou | Absorveu | Erosão |
|---|---|---|---|
| Mata intacta | 8,6% | 60,0 L | 1.250 |
| **Depois do incêndio** | **59,1%** | **11,8 L** | **329.299** |

A erosão multiplicada por 263. É a consequência que não aparece na notícia do incêndio.

### A mata segura a encosta

Mesmo terremoto de magnitude 7, mesmo solo:

| Cenário | Risco de deslizamento |
|---|---|
| Terreno plano | 0,00% |
| **Encosta com solo solto** | **22,99%** |
| **Encosta com mata** | **0,00%** |

### Cenário: a enchente do Rio Grande do Sul

Mesma chuva, mesma encosta — muda apenas quem ocupa a planície do rio:

| Cenário | Alagou | Absorveu |
|---|---|---|
| Bacia preservada | 52,4% | **28,8 L** |
| Várzea preservada | 53,2% | 26,7 L |
| Cidade drenada | 54,5% | 19,5 L |
| **Cidade na várzea** | 54,9% | **15,2 L** |

---

## Como funciona por dentro

```
Kinect ──► Captura ──► Processamento ──► Simulação ──► Renderização ──► Projetor
            NUI          calibração        água,          cores,
                         suavização        fogo,          curvas,
                                           sismo          sombreamento
```

### Aplicação nativa

C# com .NET 8 e WPF, para Windows 64 bits. Um único executável de 68 MB que **não exige
instalação** — o runtime vai embutido. Apenas o driver do Kinect é instalação à parte.

### Acesso ao sensor

Feito por chamada direta à API nativa do Kinect SDK 1.8, em vez do wrapper gerenciado. A
razão: só precisamos do fluxo de profundidade, e o contrato nativo é pequeno o bastante
para valer a troca — o resultado roda em .NET 8 sem camada de compatibilidade.

### Estabilização da leitura

O Kinect v1 tem ruído de 2 a 4 mm a um metro de distância. Sem tratamento, a projeção
"ferve": as curvas de nível piscam mesmo com a areia parada. Três etapas resolvem, nesta
ordem:

1. **Buracos** — pixel sem leitura mantém o último valor bom. Zerar criaria crateras
   piscando nas bordas das mãos.
2. **Tempo** — filtro com resposta adaptativa: areia parada usa suavização lenta, uma mão
   entrando produz salto legítimo e usa resposta rápida.
3. **Espaço** — suavização local de custo constante.

Deriva medida entre quadros consecutivos: **0,13 mm**.

### Calibração por pixel

O plano de referência é armazenado **para cada ponto**, não como um número único. Assim uma
caixa levemente torta, ou um sensor não perfeitamente perpendicular, não vira um gradiente
falso atravessando o mapa inteiro.

A calibração é salva em disco: abrir o programa numa nova aula carrega tudo sozinho.

### Simulação de água

Modelo de tubos virtuais: cada célula tem uma coluna de água e quatro tubos ligando-a aos
vizinhos, e a diferença de nível acelera o fluxo. Isso dá o que a aula precisa — água
contornando morros, acumulando em vales, formando rios que seguem o terreno — sem o custo
e a instabilidade de um solver de fluidos completo.

Roda em metade da resolução do sensor. A decisão veio de uma medição: em resolução cheia
seriam 86 milhões de operações por quadro; em metade, 11 milhões. A água é um campo suave,
então a perda desaparece na reamostragem, e tudo continua cabendo na CPU.

![Saída do solver de água](https://raw.githubusercontent.com/luisfilipegdc/caixadeareia/main/docs/img/14-solver-de-agua.png)

*Saída direta do modelo, sem a rampa de cores: a água contorna os dois morros — secos, em
amarelo — e forma um canal de drenagem no vale entre eles.*

Custo medido: **7,5 ms por quadro**, dentro do orçamento de 33 ms.

---

## Honestidade sobre o que é medido e o que é modelo

Esta separação é parte do valor pedagógico do projeto, não uma ressalva burocrática.

**O que a caixa mede de verdade:** o relevo. Cada milímetro de areia é lido pelo sensor, e
o mapa topográfico é uma representação fiel do que está ali.

**O que a caixa modela:** as simulações. Os valores de infiltração, erosão e amplificação
sísmica são **didáticos**. Seguem a ordem de grandeza da literatura — mata infiltra muito,
asfalto não infiltra nada — mas foram escolhidos para que a diferença apareça numa aula de
meia hora. Servem para o estudante enxergar a relação, não para prever uma cheia real.

**O que a caixa não faz:** o terremoto não parte de falhas geológicas reais, porque a
caixa não tem subsuperfície. É um modelo das consequências — como solo e encosta mudam o
dano — e não uma previsão sísmica.

Um estudante que sai da aula achando que a caixa calculou uma enchente real aprendeu algo
errado. Dizer isso à turma transforma a limitação numa boa aula sobre a diferença entre
medir e modelar.

---

## Autoria e referências

**Projeto autoral**, inspirado em iniciativas acadêmicas anteriores. A autoria não depende
de negar as referências: está nas decisões de arquitetura, na implementação, nos testes com
hardware real, na adaptação ao contexto escolar brasileiro e no planejamento pedagógico.

O que foi construído especificamente para este projeto:

- Aplicação nativa em C# / .NET 8 com WPF
- Captura do sensor implementada diretamente pela API nativa
- Calibração por pixel, suavização em três etapas e renderização próprias
- Simulações de água, solo, fogo e sismo, com os módulos compartilhando o mapa de cobertura
- Interface e fluxo de operação desenhados para uso em sala de aula
- Cálculo da geometria de montagem a partir da estrutura física real

### Referências reconhecidas

| Referência | Contribuição histórica |
|---|---|
| [Augmented Reality Sandbox](https://arsandbox.ucdavis.edu/) — UC Davis / KeckCAVES | Conceito de medir relevo com sensor de profundidade e projetar topografia e água |
| Caixa e-Água — FURB, 2017 | Aplicação universitária brasileira baseada em Vrui, Kinect e SARndbox |
| Magic-Sand | Porte parcial do SARndbox para openFrameworks |

Estas iniciativas estabeleceram o conceito. A implementação aqui é independente — não
deriva do código-fonte de nenhuma delas.

---

## Estado atual

### Funcionando e verificado

- Captura do Kinect v1 entre 20 e 29 quadros por segundo, com modo de proximidade ativo
- Calibração por pixel, persistente entre sessões, com relatório de cobertura
- Mapa topográfico com curvas de nível e sombreamento
- Projeção em tela cheia, alinhável por teclado e persistida
- Três simulações — água, fogo e sismo — compartilhando a cobertura do solo
- Doze tipos de cobertura e seis cenários pedagógicos prontos
- Reconexão automática quando o sensor cai
- Simulador embutido, para preparar aula sem hardware

### Limitações conhecidas

- **Alinhamento apenas afim.** Projetor muito oblíquo deixa distorção residual, que exigiria
  correção de perspectiva por quatro cantos.
- **Sem correção da distorção da lente.** Erro de alguns milímetros nas bordas do campo.
- **Apenas Kinect v1.** O v2 exigiria outra camada de captura.
- **Apenas Windows.** O driver do Kinect só existe para essa plataforma.

### O que vem pela frente

Correção de perspectiva, camada pedagógica com roteiros alinhados à BNCC, simulações de
temperatura e clima, e uma biblioteca de aulas compartilhável entre professores.

O roadmap completo está em
[ROADMAP.md](https://github.com/luisfilipegdc/caixadeareia/blob/main/ROADMAP.md).

---

## Para começar

| | |
|---|---|
| **Baixar** | [Última versão](https://github.com/luisfilipegdc/caixadeareia/releases/latest) — arquivo único, sem instalação |
| **Manual** | [Da instalação à primeira aula](https://github.com/luisfilipegdc/caixadeareia/blob/main/docs/MANUAL.md) |
| **Montagem** | [Cálculo de altura do sensor e escolha do projetor](https://github.com/luisfilipegdc/caixadeareia/blob/main/docs/MONTAGEM-FISICA.md) |
| **Código** | [github.com/luisfilipegdc/caixadeareia](https://github.com/luisfilipegdc/caixadeareia) |
| **Suporte** | contato@luisfilipegdc.com.br |

Software livre sob **GPL-2.0-or-later**: você pode usar, estudar, modificar e redistribuir,
inclusive em escolas e projetos próprios. Trabalhos derivados devem permanecer sob a mesma
licença e preservar os avisos de autoria.

### Citação sugerida

> **Caixa de Areia Interativa**: plataforma de projeção topográfica para ensino de
> geografia e ciências ambientais. Versão 1.3. Projeto Caixa de Areia, Brasília, 2026.
> Disponível em: https://luisfilipegdc.com.br/caixa-de-areia
