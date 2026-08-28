# Calibração física da caixa

Procedimento para medir a largura de areia que o sensor enxerga, e assim tornar os
valores em litros confiáveis. Vale para qualquer instalação, não só esta.

Enquanto este procedimento não for feito, o software marca os volumes com **"≈"** e
avisa que são estimativa. As porcentagens — área alagada, área queimada, saturação — não
dependem desta medição e já são confiáveis.

---

## Por que isso é necessário

A simulação de água divide a caixa numa grade de 320×240 células. Para converter lâmina
de água em litros, ela precisa saber **quantos milímetros cada célula representa**:

```
tamanho da célula = larguraCobertaPeloSensorMm / 320
área da célula    = tamanho × tamanho
volume em litros  = lâmina (mm) × área (mm²) × 1e-6
```

O valor padrão de `larguraCobertaPeloSensorMm` é **1250 mm**, que é a suposição que
estava embutida no código desde o início e **nunca foi medida**. O erro entra ao
quadrado: se a largura real for 10% maior, o volume sai 21% maior.

> **Não estime pelo campo de visão teórico.** Dá para calcular que o eixo horizontal do
> Kinect v1 (57°) cobre `1,0859 × distância`, e concluir que a 1,28 m ele veria 139 cm.
> Mas isso depende de a altura do sensor estar medida, do sensor estar perpendicular, e
> de o FOV nominal bater com o da unidade. Trocar uma suposição por outra não resolve —
> **meça**.

---

## Material

- Fita métrica
- Dois objetos que apareçam no mapa de profundidade: qualquer coisa com alguns
  centímetros de altura serve. Blocos de madeira, latas, tijolos.
- O software rodando, com o sensor ligado e **calibrado**

---

## Procedimento

### 1. Nivele e calibre

Alise a areia, tire as mãos da caixa, toque em **Nivelar e calibrar**. Sem isso o relevo
não aparece e os marcadores não se distinguem do fundo.

### 2. Posicione dois marcadores

Coloque os dois objetos sobre a areia, **alinhados no eixo horizontal** da imagem — o
eixo de 640 pixels, que é o lado mais largo do que o sensor enxerga.

Afaste-os o máximo que a caixa permitir, e **meça a distância entre eles com a fita**,
de centro a centro. Anote em milímetros. Quanto maior a distância, menor o efeito do erro
de leitura.

> Exemplo: marcadores a **900 mm** um do outro.

### 3. Abra a projeção e ligue a grade

Toque em **Abrir projeção** e tecle **G**. Aparece uma grade de 10 × 10 divisões sobre a
imagem, com as bordas em vermelho.

Cada divisão vertical vale **10% da largura do quadro do sensor** — ou seja, 64 pixels
dos 640.

### 4. Leia em que divisões os marcadores caem

Olhe onde os dois marcadores aparecem em relação às linhas da grade. Estime em décimos,
interpolando quando cair no meio de uma divisão.

> Exemplo: o marcador da esquerda cai na divisão **1,5**; o da direita, na **7,9**.
> Diferença: **6,4 divisões** = 0,64 da largura do quadro.

### 5. Calcule

```
largura coberta = distância medida entre marcadores ÷ fração da largura
```

> Exemplo: 900 mm ÷ 0,64 = **1406 mm**

### 6. Registre no `config.json`

O arquivo fica ao lado do executável — o caminho aparece no painel, em *Ajustes
técnicos*. Com o programa **fechado**, edite:

```json
"caixa": {
  "larguraCobertaPeloSensorMm": 1406,
  "larguraMedida": true
}
```

Ao reabrir, os volumes perdem o "≈" e o aviso de estimativa some.

### 7. Confira

Uma verificação independente: faça chover uma quantidade conhecida sobre a caixa inteira
e compare. Com chuva de 10 mm/s por 4 s sobre uma cobertura que quase não infiltra
(Rocha), caem 40 mm de lâmina sobre toda a área. O volume esperado é:

```
volume ≈ 40 mm × largura × altura coberta × 1e-6 litros
```

Se o número na tela ficar na mesma ordem de grandeza, a medição está coerente.

---

## Anote também, para o registro

| Grandeza | Valor | Como obter |
|---|---|---|
| Distância do sensor à areia nivelada | | fita, do sensor à superfície |
| Largura física útil da caixa | | fita, borda a borda |
| Distância entre os marcadores | | fita, centro a centro |
| Fração da largura do quadro | | leitura na grade |
| **Largura coberta pelo sensor** | | cálculo do passo 5 |
| Margem de erro estimada | | ver abaixo |

### Estimando a margem de erro

O erro dominante é a leitura na grade. Errar meia divisão em cada marcador dá 0,1 da
largura do quadro — sobre 0,64, são ~16%. Duas formas de reduzir:

- **Afastar os marcadores.** Quanto maior a fração, menor o peso do erro de leitura.
- **Repetir com posições diferentes** e usar a média.

Com marcadores bem afastados e leitura cuidadosa, espere algo entre **5% e 10%** de
incerteza na largura — o que vira 10% a 20% no volume, porque a área é o quadrado.

**Registre a incerteza junto com o valor.** Um número sem margem de erro convida a
tratar como exato o que não é.

---

## Limitações que a calibração não resolve

### As células não são exatamente quadradas

O código assume célula quadrada: `área = tamanho × tamanho`, usando o tamanho derivado
do eixo horizontal para os dois lados.

Isso não é exato. O Kinect v1 tem 57° na horizontal e 43° na vertical, o que dá uma
proporção de campo de **1,378**; a grade da simulação é 320×240, proporção **1,333**.
As células ficam cerca de **3,4% mais largas que altas**, e a área calculada supera a
real na mesma proporção.

**Por que não foi corrigido:** enquanto a largura não estiver medida, o erro dominante é
o dela — cerca de **24% na área**, se a suposição de 1250 mm estiver mesmo errada.
Corrigir 3,4% antes de resolver 24% seria falsa precisão.

**Correção proposta**, para depois da medição: introduzir
`alturaCobertaPeloSensorMm` ao lado da largura, derivar dois tamanhos de célula, e
trocar `tamanho × tamanho` por `tamanhoX × tamanhoY`. Isso muda `VolumeLitros` e
`InfiltradoLitros` em ~3,4% e precisa de decisão consciente, não de mudança silenciosa.

### A região de interesse ainda é o quadro inteiro

O software desenha e simula sobre **tudo o que o sensor enxerga**, incluindo chão, bordas
da caixa e quem estiver por perto. A configuração `projection.roi*` existe mas não tem
controle na interface.

Consequência para esta medição: a largura que você mede é a do **campo de visão**, não a
da caixa. Está certo assim — é isso que o cálculo do tamanho da célula precisa. Mas
significa que a água considera a borda do campo de visão como borda do mundo, não a
borda física da caixa.

Registrado como pendência P4.

---

## Uma nota sobre onde o programa lê a configuração

O `config.json` fica **ao lado do executável**, e o caminho depende de como o projeto foi
compilado:

| Como você compilou | Onde o executável fica |
|---|---|
| `dotnet build CaixaInterativa.sln` | `src/CaixaInterativa/bin/x64/Release/net8.0-windows/` |
| `dotnet build src/CaixaInterativa/CaixaInterativa.csproj` | `src/CaixaInterativa/bin/Release/net8.0-windows/` |

São **duas pastas diferentes**, porque a solução fixa a plataforma `x64` e o MSBuild
insere isso no caminho. Editar o `config.json` de uma e rodar o executável da outra não
tem efeito — e rodar a pasta errada faz você testar um binário antigo.

O caminho em uso aparece no painel, em *Ajustes técnicos*. **Confie nele**, não na
memória.
