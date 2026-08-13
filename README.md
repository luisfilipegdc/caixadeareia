# Caixa de Areia Interativa

Caixa de areia com realidade aumentada para o ensino de cartografia: um projetor
desenha sobre a areia real o mapa de altitudes, as curvas de nível e a água
escorrendo pelo relevo que os alunos modelam com as mãos.

Diferente dos projetos que serviram de base — o
[AR Sandbox](https://arsandbox.ucdavis.edu) do KeckCAVES/UC Davis e o
[Caixa e Água](https://github.com/lifefurb/caixaeagua) da FURB — **todo o
sistema roda no navegador**. Não há Vrui, não há SARndbox, não há compilação.

## O que muda em relação ao projeto original

| | AR Sandbox / Caixa e Água | Este projeto |
|---|---|---|
| Instalação | Linux + 4 scripts de compilação | abrir uma URL, ou um `.exe` |
| Sistema operacional | só Linux | qualquer um |
| Criar um cenário novo | editar C++ e recompilar | uma pasta com JSON + shader |
| Atualizar as escolas | visita presencial | publicar o site |
| Sem sensor | não roda | modo demonstração completo |

## Como testar agora (sem sensor nenhum)

```bash
npm run web
```

Abra <http://localhost:8080>. O modo **Demonstração** gera um relevo sintético:

- arraste com o **botão esquerdo** para levantar areia, com o **direito** para cavar
- segure **Shift** e arraste para fazer chover
- **1–5** trocam de cenário, **F** entra em tela cheia, **espaço** pausa, **C** seca a água

## Publicar na Vercel

O projeto é estático, sem etapa de build. Na Vercel: importe o repositório,
framework **Other**, deixe o comando de build vazio e o diretório de saída
como a raiz. O `vercel.json` já cuida do cache (cenários e código sempre
revalidados, para o professor receber atualização sem limpar nada).

Com o site em HTTPS, vale saber: o navegador bloqueia `ws://` vindo de página
segura, **exceto para localhost**. Ou seja, a Vercel funciona bem para demonstrar
os modos e para a versão sem sensor; com o Kinect, prefira o executável ou o
`npm run web` na própria máquina da caixa. Se tentar um endereço que o
navegador vai barrar, a interface avisa em vez de falhar em silêncio.

## Com o Kinect

O navegador não consegue falar com o Kinect diretamente: o Chrome não
implementa transferências USB isócronas, que é como o sensor transmite
profundidade. Por isso existe uma **ponte** — o único pedaço nativo do projeto,
que só lê o sensor e reenvia por WebSocket.

```bash
npm install
npm run ponte              # sensor real
npm run ponte:simulada     # relevo falso, para testar a cadeia inteira
```

No painel, escolha a fonte **Kinect (via ponte)** e clique em *Conectar*.

### Se o sensor não conectar

```bash
npm run diagnostico
```

Ele confere versão do Node, drivers instalados, presença do SDK, se o sistema
operacional está enxergando o sensor no USB e se a porta está livre — e
termina dizendo o próximo passo. A causa mais comum é simples: **o Kinect não
se alimenta pelo USB**, ele precisa da fonte de energia do adaptador oficial.

Sensores suportados pela ponte:

| Sensor | Driver | Observação |
|---|---|---|
| Kinect for Xbox One / Windows v2 | `kinect2` | melhor resolução, exige USB 3.0 |
| Kinect v1: Xbox 360 (1414/1473) e for Windows (1517) | `freenect` ou `kinect` | o mais barato de achar usado |
| Qualquer um, por programa externo | entrada padrão | ver abaixo |
| Nenhum | simulado | desenvolvimento e demonstração |

### Kinect v1 no Windows — caminho recomendado

Use a **ponte em C#** (`ponte-windows-v1/`), que fala com o sensor pelo Kinect
for Windows SDK 1.8, o driver oficial do modelo 1517. Ela substitui a ponte em
Node: é um `.exe` só, sem Node, sem npm, sem compilar módulo nativo.

1. Instale o **Kinect for Windows SDK 1.8** e o **Runtime 1.8** da Microsoft
2. Baixe o `PonteKinect.exe` (artifacts do GitHub Actions ou a aba Releases)
3. Rode:

```
PonteKinect.exe
PonteKinect.exe --perto           40cm a 3m, aproveita o modo perto do 1517
PonteKinect.exe --angulo -15      inclina o motor para a caixa
PonteKinect.exe --simulado        testa sem sensor
```

4. Abra a caixa em `http://localhost:8080` (`npm run web`) ou o executável
   principal, escolha **Kinect (via ponte)** e conecte em `ws://localhost:8787`

Ela também sabe alimentar a ponte em Node, se você preferir:

```
PonteKinect.exe --saida-padrao | node ponte/ponte.js
```

Um detalhe que economiza uma tarde: o runtime do Kinect 1.8 é de 32 bits, por
isso o projeto compila em **x86**. Se aparecer erro de arquitetura ao carregar
`Microsoft.Kinect.dll`, é isso.

### Kinect v1 pelos drivers em Node

O driver do v1 é a libfreenect, e os pacotes npm que a empacotam são antigos:
no Linux compilam sem drama, no Windows costumam falhar. Em ordem de esforço:

1. **Tente direto** — `npm install freenect`, e se falhar, `npm install kinect`.
   Leva um minuto e às vezes resolve.
2. **Linux na máquina da caixa** — é onde o v1 é bem suportado, e é o que o AR
   Sandbox original usa. `sudo apt install libfreenect-dev` antes do npm install.
3. **Programa externo alimentando a ponte** — se você já tem qualquer programa
   que fale com o sensor nessa máquina, ele pode alimentar a ponte pela entrada
   padrão, despejando quadros crus de 640×480 em uint16:

   ```bash
   meu-programa-do-kinect | PONTE_STDIN=1 node ponte/ponte.js
   ```

   A ponte não precisa saber como os quadros foram obtidos.

O v1 entrega profundidade em 640×480, exatamente a resolução que o projeto
assume — a perda em relação ao v2 é pequena para uso em caixa de areia.

### Calibração

1. **Base (cm)** — distância do sensor até o fundo da caixa vazia
2. **Altura útil (cm)** — espessura da camada de areia
3. **Ajustar 4 cantos** — clique nos cantos da areia na ordem indicada; isso
   alinha a projeção ao que o sensor enxerga

A mão pairando acima da areia é detectada automaticamente e vira chuva — o
mesmo gesto do projeto original.

## Executável para a escola

O `.github/workflows/build.yml` compila o `.exe` no Windows a cada push; o
arquivo sai nos *artifacts* da execução. Um push de tag `v*` publica o
executável direto na aba Releases:

```bash
git tag v0.1.0 && git push origin v0.1.0
```

Para gerar localmente, em uma máquina Windows:

```bash
npm install
npm run empacotar
```

O executável sobe o servidor local, sobe a ponte e abre em tela cheia,
preferindo o segundo monitor (o projetor) quando existe.

### Sobre o driver do Kinect no executável público

O `kinect2` é um módulo nativo: para compilar, a máquina precisa do Kinect for
Windows SDK v2 instalado, e o runner do GitHub não tem. Por isso o `.exe`
publicado sai **sem** o driver embutido — ele traz os 16 modos e o modo
demonstração completos, e serve para avaliar o sistema em qualquer PC.

Para usar com o sensor, há dois caminhos:

1. **Compilar na máquina da caixa** — instale o Kinect SDK v2 e rode
   `npm install && npm run empacotar`. O driver entra no executável.
2. **Ponte por fora** — rode `npm run ponte` antes de abrir o `.exe`. A ponte
   interna percebe que a porta já está ocupada, sai de cena em silêncio, e o
   aplicativo conversa com a sua ponte.

## Criar um cenário

Um cenário é uma pasta. Nenhum código do motor precisa mudar.

```
cenarios/meu-cenario/
  cenario.json      paleta por altitude, curvas de nível, água, quiz
  overlay.frag      opcional: efeitos próprios em GLSL
```

O `overlay.frag` implementa uma função que recebe a cor já calculada e devolve
a cor final:

```glsl
vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  return cor;
}
```

Dentro dela estão disponíveis `alturaEm(uv)`, `aguaEm(uv)`, `marcadorEm(uv)`,
`ruido(p)` e `u_texel`. Depois, acrescente o id em `cenarios/index.json`. Um
shader com erro é rejeitado e o cenário cai na exibição padrão — a aula não
para.

## Modos incluídos

| Modo | Categoria | O que faz |
|---|---|---|
| Topografia | educação | cores por altitude e curvas de nível |
| Nascente de água | relaxamento | correnteza com espuma, cheias, barragens |
| Bacias hidrográficas | educação | destaca os divisores de água |
| Ecossistemas | educação | biomas que respondem à umidade |
| Ilhas e arquipélagos | educação | linha de costa, praia e rebentação |
| Cidades e áreas de proteção | educação | ocupação regular e irregular junto da água |
| Enchente | educação | cheia cíclica revelando as áreas de risco |
| Safári | educação | savana que vira deserto rachado quando seca |
| Inverno | relaxamento | neve que só gruda em encosta suave, água que congela |
| Marte | educação | regolito, crateras e tempestade de poeira |
| Vulcão | jogos | lava incandescente com crosta esfriando |
| Dinossauros | jogos | pântano, samambaias e pegadas no barro |
| Jardineiro | jogos | plantar sementes, regar, ver a planta viver ou murchar |
| Formas e cores | criatividade | camadas do arco-íris e formas geométricas guia |
| Pintura na areia | criatividade | modo livre, a altura vira cor |
| Quiz do ciclo da água | educação | perguntas conduzidas pelo professor |

O modo **Cidades e áreas de proteção** reproduz a atividade descrita no
material da FURB: os alunos fundam cidades, e o sistema marca com círculo
branco as que respeitam a mata ciliar e com vermelho pulsante as que invadem a
área protegida.

Cada modo declara sua categoria, e o painel filtra por ela. Modos com
`"marcadores"` habilitam o gesto **Alt + clique**, que planta sementes, abre
nascentes ou posiciona rebanhos conforme o modo.

## Como funciona por dentro

```
Kinect ──ponte──► profundidade (mm)
                        │
                        ▼
              src/sim/terreno.js      recorte, buraco preenchido, mão detectada
                        │  textura de altura
                        ▼
              src/sim/agua.js         tubos virtuais (Mei et al.) em shaders
                        │  lâmina d'água
                        ▼
              src/render/vista.js     paleta + curvas de nível + cenário
                        │
                        ▼
                    projetor
```

A simulação de água usa o mesmo método do SARndbox: cada célula troca vazão
com as quatro vizinhas conforme a diferença de altura total, com limitador
para nunca escoar mais água do que existe na célula.

## Hardware

- **PC** com placa de vídeo dedicada (a simulação de água é o gargalo)
- **Projetor** de curta distância, conectado por HDMI/DVI/DisplayPort — VGA
  degrada a imagem e desalinha a projeção
- **Kinect** montado ao lado do projetor, apontado para a areia
- **Caixa** com areia clara; areia de sílica rende mais contraste na projeção

## Licença

GPL-2.0-or-later, herdada dos projetos que serviram de base.
