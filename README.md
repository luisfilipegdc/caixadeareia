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

Sensores suportados pela ponte:

| Sensor | Driver | Observação |
|---|---|---|
| Kinect for Xbox One / Windows v2 | `kinect2` | melhor resolução |
| Kinect for Xbox 360 (1414/1473) | `freenect` | o mais barato de achar usado |
| Nenhum | simulado | desenvolvimento e demonstração |

### Calibração

1. **Base (cm)** — distância do sensor até o fundo da caixa vazia
2. **Altura útil (cm)** — espessura da camada de areia
3. **Ajustar 4 cantos** — clique nos cantos da areia na ordem indicada; isso
   alinha a projeção ao que o sensor enxerga

A mão pairando acima da areia é detectada automaticamente e vira chuva — o
mesmo gesto do projeto original.

## Executável para a escola

O `.github/workflows/build.yml` compila o `.exe` no Windows a cada push; o
arquivo sai nos *artifacts* da execução. Para gerar localmente, em uma máquina
Windows:

```bash
npm install
npm run empacotar
```

O executável sobe o servidor local, sobe a ponte e abre em tela cheia,
preferindo o segundo monitor (o projetor) quando existe.

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
| Safári | educação | savana que vira deserto rachado quando seca |
| Vulcão | jogos | lava incandescente com crosta esfriando |
| Jardineiro | jogos | plantar sementes, regar, ver a planta viver ou murchar |
| Formas e cores | criatividade | camadas do arco-íris e formas geométricas guia |
| Quiz do ciclo da água | educação | perguntas conduzidas pelo professor |

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
