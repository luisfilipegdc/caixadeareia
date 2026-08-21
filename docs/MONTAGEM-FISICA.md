# Montagem física — cálculo da geometria

Análise da estrutura existente contra os requisitos ópticos do Kinect v1 e do projetor.

---

## Medidas informadas

### Caixa

| Dimensão | Valor |
|---|---|
| Largura | 101 cm |
| Comprimento | 125 cm |
| Altura | 19 cm (sem rodinhas) |
| Rodinhas | *a medir* |

### Pórtico (em U invertido)

| Dimensão | Valor |
|---|---|
| Altura total do chão | 179 cm |
| Vão | 133,5 cm |
| Viga central que segura o Kinect | 40 cm |

---

## Campo de visão do Kinect v1

O sensor enxerga **57° na horizontal** e **43° na vertical**. A área coberta cresce
linearmente com a distância:

```
largura coberta = 1,0859 × distância
altura  coberta = 0,7878 × distância
```

O aspecto do campo de visão é **1,378**; o da caixa é **1,238**. Como a caixa é mais
“quadrada” que o campo, **quem limita é o eixo estreito** — os 43°.

### Distância mínima para enxergar a caixa inteira

| Requisito | Distância necessária |
|---|---|
| Cobrir 125 cm no eixo de 57° | 115,1 cm |
| **Cobrir 101 cm no eixo de 43°** | **128,2 cm** ← limitante |
| Com 8% de margem de segurança | 138,5 cm |

---

## ⚠️ Achado principal: a viga de 40 cm deixa o Kinect baixo demais

> ✅ **Confirmado em campo.** O cálculo abaixo previu que o sensor na posição atual não
> cobriria a caixa inteira. Verificado na estrutura montada: **não cobre a altura total**.
> A previsão e a observação batem, o que dá confiança para usar este mesmo modelo ao
> decidir a nova altura em vez de tentar por tentativa e erro.

Com o Kinect pendurado 40 cm abaixo do topo, ele fica a **139 cm do chão**. Descontando a
altura das rodinhas e a camada de areia, a distância até a superfície fica entre 112 e
121 cm — **abaixo dos 128,2 cm necessários**.

| Rodinha | Areia | Superfície | Viga 0 cm | Viga 15 cm | Viga 25 cm | **Viga 40 cm** |
|---|---|---|---|---|---|---|
| 8 cm | 10 cm | 18 cm | 161,0 ✅ | 146,0 ✅ | 136,0 ✅ | **121,0 ❌** |
| 8 cm | 12 cm | 20 cm | 159,0 ✅ | 144,0 ✅ | 134,0 ✅ | **119,0 ❌** |
| 12 cm | 10 cm | 22 cm | 157,0 ✅ | 142,0 ✅ | 132,0 ✅ | **117,0 ❌** |
| 12 cm | 12 cm | 24 cm | 155,0 ✅ | 140,0 ✅ | 130,0 ✅ | **115,0 ❌** |
| 15 cm | 10 cm | 25 cm | 154,0 ✅ | 139,0 ✅ | 129,0 ✅ | **114,0 ❌** |
| 15 cm | 12 cm | 27 cm | 152,0 ✅ | 137,0 ✅ | 127,0 ❌ | **112,0 ❌** |

✅ = cobre a caixa inteira · ❌ = sobra caixa fora do campo de visão

**Consequência prática:** com a viga de 40 cm, o sensor enxergaria cerca de 121 × 88 cm.
Faltariam aproximadamente **13 cm de cada lado** no eixo estreito — duas faixas da caixa
simplesmente não existiriam no mapa projetado.

### Recomendação

**Encurtar a viga para 15 cm**, ou montar o Kinect direto no topo do pórtico.

| Opção | Distância à areia | Cobertura | Ruído estimado | Avaliação |
|---|---|---|---|---|
| Viga 40 cm (atual) | 112–121 cm | insuficiente | ~2,0 mm | ❌ não cobre a caixa |
| Viga 25 cm | 127–136 cm | justa | ~2,4 mm | 🟡 sem margem |
| **Viga 15 cm** | **137–146 cm** | **boa** | **~2,9 mm** | ✅ **recomendado** |
| Sem viga (topo) | 152–161 cm | folgada | ~3,7 mm | 🟡 ruído maior |

A viga de 15 cm é o melhor equilíbrio: cobre a caixa com margem para erro de
posicionamento, e mantém o ruído em torno de 3 mm — bem dentro do que a suavização em três
etapas já absorve.

---

## ⚠️ Segundo achado: a orientação do Kinect importa muito

O sensor é retangular — 57° no eixo dos 640 pixels, 43° no eixo dos 480. **O lado largo
precisa ficar paralelo ao comprimento de 125 cm da caixa.**

| Orientação | Distância mínima necessária |
|---|---|
| **Correta** — eixo de 57° ao longo dos 125 cm | **128,2 cm** |
| Girada 90° — eixo de 57° ao longo dos 101 cm | 158,7 cm |

Montar girado exigiria 30 cm a mais de altura, que o pórtico de 179 cm mal comportaria — e
com ruído bem maior. É um erro fácil de cometer na hora de parafusar e caro de descobrir
depois.

---

## Cobertura e qualidade por distância

| Distância | Área coberta | Cabe a caixa? | Resolução | Ruído |
|---|---|---|---|---|
| 110 cm | 119,5 × 86,7 cm | ❌ | 1,87 mm/px | 1,8 mm |
| 120 cm | 130,3 × 94,5 cm | ❌ | 2,04 mm/px | 2,2 mm |
| 128 cm | 139,0 × 100,8 cm | ❌ (por 2 mm) | 2,17 mm/px | 2,5 mm |
| 135 cm | 146,6 × 106,4 cm | ✅ | 2,29 mm/px | 2,7 mm |
| **139 cm** | **150,9 × 109,5 cm** | ✅ | 2,36 mm/px | 2,9 mm |
| 145 cm | 157,5 × 114,2 cm | ✅ | 2,46 mm/px | 3,2 mm |
| 159 cm | 172,7 × 125,3 cm | ✅ | 2,70 mm/px | 3,8 mm |

O ruído do Kinect v1 cresce aproximadamente com o quadrado da distância. A 139 cm ficamos
em torno de 3 mm — a suavização atual dá conta, mas talvez valendo subir o raio do box blur
de 3 para 4.

> **Ajuste necessário no software:** `MaxValidDepthMm` está em **2000**. Todas as distâncias
> viáveis (127–161 cm) cabem nesse limite, então não é preciso mexer. Mas se a caixa for
> operada vazia, o fundo estará ~19 cm mais longe que a areia — ainda dentro do limite.

---

## Projetor — requisito de throw ratio

O *throw ratio* é a razão entre a distância do projetor à superfície e a largura da imagem
projetada. Para cobrir a caixa a partir do topo do pórtico:

| Formato | Largura de imagem necessária | Altura útil | **Throw ratio máximo** |
|---|---|---|---|
| **4:3** | 134,7 cm | 157 cm | **1,17** |
| 16:10 | 161,6 cm | 157 cm | 0,97 |
| 16:9 | 179,6 cm | 157 cm | 0,87 |

**Por que a largura necessária é maior que a caixa:** o projetor precisa cobrir os 101 cm
do lado curto. Num formato 4:3, uma imagem com 101 cm de altura tem 134,7 cm de largura —
sobra nas laterais, e essa sobra é normal e desejável.

### O que isso significa na hora de comprar

- Projetores de entrada costumam ter throw ratio entre **1,4 e 1,6**. A 157 cm, um throw de
  1,5 produziria imagem de apenas 105 cm de largura — **não cobriria a caixa**.
- É preciso um projetor **4:3 com throw ≤ 1,17**, ou um **short throw** (16:9 com
  throw ≤ 0,87).
- Se o projetor tiver zoom óptico, o que importa é o **menor** valor da faixa. Um projetor
  anunciado como “1,2–1,6” serve; um “1,5–1,8” não serve.

> **Alternativa se o projetor disponível não atender:** montar o projetor num braço acima do
> topo do pórtico, ganhando altura. Cada 10 cm a mais de altura aumenta a imagem em cerca de
> 8,5 cm de largura num throw de 1,17.

---

## Posicionamento de Kinect e projetor no mesmo pórtico

Os dois não podem ocupar o mesmo ponto. A montagem usual:

- **Projetor no centro do topo**, o mais alto possível — cada centímetro vira área projetada
- **Kinect ao lado**, na viga encurtada

O deslocamento lateral do Kinect cria uma leve obliquidade em relação à caixa. **Isso já é
absorvido pelo software:** o plano-base é armazenado por pixel, justamente para que um
sensor não perfeitamente perpendicular não vire um gradiente falso no mapa.

O alinhamento entre a imagem projetada e a leitura do sensor é feito pelo teclado na janela
de projeção e salvo em `config.json`.

---

## Orientação da caixa sob o pórtico

O vão do pórtico é de 133,5 cm.

| Orientação | Folga lateral | Observação |
|---|---|---|
| Comprimento (125 cm) no vão | 8,5 cm | Apertado — dificulta acesso |
| **Largura (101 cm) no vão** | **32,5 cm** | ✅ Melhor acesso dos alunos pelas laterais |

Com a largura no vão, o comprimento de 125 cm fica ao longo do pórtico — e é nesse eixo que
o lado de 57° do Kinect deve ficar alinhado.

---

## Checklist de montagem

- [x] ~~Verificar se a montagem atual cobre a caixa~~ — **não cobre**, confirmado em campo
- [ ] Medir a altura das rodinhas
- [ ] Definir a camada de areia (recomendado 10–12 cm, deixando 7–9 cm de borda livre)
- [ ] **Encurtar a viga do Kinect de 40 cm para 15 cm** ← próxima ação
- [ ] **Conferir a orientação do Kinect** — lado largo paralelo aos 125 cm
- [ ] Verificar o throw ratio do projetor disponível
- [ ] Posicionar o projetor no centro do topo, Kinect ao lado
- [ ] Verificar rigidez do pórtico — qualquer deslocamento invalida a calibração
- [ ] Organizar cabos e alimentação (o Kinect exige fonte externa)
- [ ] Testar iluminação da sala — sol direto cega o sensor
- [ ] Calibrar com areia real e conferir cobertura acima de 90%
- [ ] Documentar sombras causadas pelas mãos

---

## Dados que ainda faltam

Para fechar o cálculo com precisão:

1. **Altura das rodinhas** — determina a altura real da superfície da areia
2. **Confirmação da viga de 40 cm** — ela desce do topo, ou é uma travessa horizontal?
3. **Medida interna da caixa** — 101 × 125 é externo; a área útil é menor pela espessura
   das paredes, e é ela que precisa caber no campo de visão
4. **Modelo do projetor** — para conferir o throw ratio real

---

## Como refazer estes cálculos

O script está em `docs/geometria.py`. Ajuste as constantes do topo e rode:

```bash
python docs/geometria.py
```
