# Dados públicos offline — capacidade experimental

> **⚠️ EXPERIMENTAL.** Esta capacidade traz **contexto** para a aula. Ela **não** alimenta
> nenhum parâmetro das simulações, e não foi validada como recurso pedagógico em sala.

---

## A distinção que sustenta tudo

**DADO PÚBLICO REAL ≠ SIMULAÇÃO FÍSICA REAL.**

O pacote traz números observados por satélite, publicados pelo INPE. A propagação do fogo
e o escoamento da água continuam sendo **modelos didáticos**, calibrados para o fenômeno
aparecer numa aula de meia hora. Uma coisa não empresta credibilidade à outra.

A interface mantém as duas separadas, e a atividade conceitual diz isso em texto:

| Origem | O que é |
|---|---|
| **Dado externo observado** | Focos de calor detectados por satélite — INPE |
| **Medição da caixa** | O relevo que os estudantes moldaram, lido pelo sensor |
| **Modelo didático** | A propagação do fogo. Não é previsão, e não reproduz os focos observados |

---

## O fluxo

```
INPE (CSV público)
   ↓   uma vez, na mesa de quem desenvolve
CaixaInterativa.DataPrep
   ↓   JSON pequeno, versionado no Git
src/CaixaInterativa/Dados/contexto-queimadas.json
   ↓   copiado para a saída do build
Aplicação (lê arquivo local, nunca a rede)
   ↓
Contexto pedagógico na tela, com procedência
```

**A ferramenta é a única parte do projeto que acessa a rede, e ela não entra no
executável distribuído.** O aplicativo WPF não tem `HttpClient`, `WebClient`,
`WebRequest`, `Socket` nem qualquer `System.Net`, e não referencia projeto nenhum.

Por quê: uma escola pode ficar sem internet, e a aula não pode depender de o INPE estar no
ar naquele momento. Há um segundo motivo, menos óbvio — **dado que muda sozinho quebraria
a comparação entre duas aulas**, que é justamente o que a `AssinaturaDoRelevo` passou a
proteger.

---

## Como regenerar o pacote

```bash
dotnet run --project tools/CaixaInterativa.DataPrep -- \
  --fonte https://dataserver-coids.inpe.br/queimadas/queimadas/focos/csv/mensal/Brasil/focos_mensal_br_202606.csv \
  --fonte https://dataserver-coids.inpe.br/queimadas/queimadas/focos/csv/mensal/Brasil/focos_mensal_br_202607.csv \
  --saida src/CaixaInterativa/Dados/contexto-queimadas.json
```

O comando também fica gravado **dentro do próprio pacote**, no campo
`proveniencia.comandoParaRegenerar` — quem abrir o JSON descobre como refazê-lo sem
procurar documentação.

`--fonte` aceita URL ou caminho local e **pode repetir**: cada arquivo entra como uma
fonte a mais. `--ajuda` mostra o uso; sem `--saida`, o JSON sai na saída padrão.

**O período não vem do nome do arquivo, vem da data de cada linha.** Isso importa: um
arquivo diário promovido a "mês" produziria um rótulo verdadeiro no formato e falso no
conteúdo. O pacote grava, por período, quantos dias distintos foram observados, e o
aplicativo marca como amostra parcial qualquer período com menos de 20.

### Os dois períodos do pacote atual, e por que estes

| Período | Arquivo | Dias | Focos lidos |
|---|---|---|---|
| 2026-06 | `focos_mensal_br_202606.csv` | 30 | 132.772 |
| 2026-07 | `focos_mensal_br_202607.csv` | 31 | 159.934 |

**Critério: os dois meses completos mais recentes, em sequência.** Junho e julho de 2026
são os últimos dois meses fechados na data de acesso. Agosto foi recusado porque está em
curso — comparar 31 dias com 28 dias faria a contagem de focos parecer menor por um
motivo que não tem nada a ver com fogo.

Nenhum dos dois foi escolhido por ser extremo. O critério é posicional (os dois últimos
fechados), não estatístico, exatamente para que a diferença encontrada seja o que os dados
trouxerem, e não o que a escolha plantou.

---

## O que o pacote contém

```json
{
  "schemaVersion": 2,
  "proveniencia": {
    "fonte": "INPE — Programa Queimadas",
    "dataDeAcesso": "2026-08-28",
    "periodos": [
      { "periodo": "2026-06", "recurso": "focos_mensal_br_202606.csv",
        "url": "https://dataserver-coids.inpe.br/...",
        "diasObservados": 30, "focosLidos": 132772 },
      { "periodo": "2026-07", "recurso": "focos_mensal_br_202607.csv",
        "url": "https://dataserver-coids.inpe.br/...",
        "diasObservados": 31, "focosLidos": 159934 }
    ],
    "comandoParaRegenerar": "dotnet run --project tools/...",
    "filtros": [...],
    "metodoDeAgregacao": "...",
    "metodoDeClassificacao": "...",
    "observacoes": [...]
  },
  "contextos": [
    {
      "bioma": "Cerrado", "uf": "GOIÁS", "periodo": "2026-06",
      "observado": {
        "focos": 3483,
        "riscoFogoMediano": 1.0, "riscoFogoP25": 0.9, "riscoFogoP75": 1.0,
        "diasSemChuvaMediano": 14.0, "frpMedianoMw": 5.9,
        "amostras": { "riscoFogo": 3483, "diasSemChuva": 3483, ... }
      },
      "classesDidaticas": {
        "risco": "Alto",
        "secura": "Seco",
        "classificacao": "relativa_ao_recorte"
      }
    }
  ]
}
```

**O período é a chave.** Cada recorte é `bioma × UF × período`, então o mesmo território
aparece uma vez por período, e a procedência de cada período fica em `proveniencia.periodos`.
Foi a menor mudança que comporta mais de um período sem virar banco de dados: uma lista a
mais no cabeçalho, mais linhas na mesma tabela, e o diff continua legível.

Estado atual: **84 recortes, 58,4 KB**, gerado de junho e julho de 2026 (292.706 focos
lidos, 41 territórios com os dois períodos).

Os quartis são calculados **sobre os dois períodos juntos**, e é isso que torna as classes
comparáveis entre eles: se cada período tivesse os seus próprios cortes, "Alto" em junho e
"Alto" em julho não significariam a mesma coisa.

---

## As decisões estatísticas, e por quê

### Mediana e quartis, nunca média

Medido nos focos de 27/08/2026, a amostra que motivou a decisão: a média de FRP era
**43,7 MW** e a mediana **11,5 MW**. A cauda de incêndios enormes puxa a média para longe
do que é típico do conjunto.

### A sentinela −999 é descartada na entrada

O INPE usa **−999 para dado inválido** — confirmado no FAQ do Programa Queimadas:
acontece em área urbana e corpo d'água, "onde não faz sentido calcular o Risco de Fogo".

Sem filtrar, a média de `risco_fogo` sai **−2,06**: um campo que vai de 0 a 1 aparecendo
negativo. O parser converte a sentinela em `null` antes de qualquer conta.

### Recortes com menos de 30 focos são descartados

Uma mediana sobre cinco focos não descreve um território.

### Classificação relativa, e declarada como tal

Os cortes vêm dos **quartis dos próprios recortes do pacote**, e cada contexto carrega
`"classificacao": "relativa_ao_recorte"`.

**Por que não a escala nomeada do INPE:** o instituto publica classes nomeadas para o
Risco de Fogo, mas não foi possível confirmar os valores de corte numa fonte primária
legível — o FAQ informa apenas que o risco varia de 0 a 1. Codificar cortes não
verificados seria inventar ciência.

### Quando não há variação, o pacote diz isso

Se os quartis empatam, não existe fronteira que separe quatro níveis, e a classe vira
**"Sem variação suficiente"**.

#### Correção de uma conclusão anterior sobre o risco de fogo

A versão anterior deste documento afirmava que o risco de fogo satura e por isso **não
serve** para separar recortes: 29 de 29 caíam em "Sem variação suficiente".

**Isso descrevia a amostra, não o campo.** Aquele pacote vinha de um único dia. Com os
dois meses atuais, o mesmo campo distribui: **Alto 42, Moderado 21, Baixo 21**, e nenhum
recorte cai em "Sem variação suficiente". O que saturava era um dia de detecções, não a
variável.

A regra de saturação continua no código, e continua disparando — só que agora onde de
fato há saturação. São dois gatilhos independentes:

1. **A classe do pacote é "Sem variação suficiente"** — os quartis empataram sobre todos
   os recortes.
2. **Os dois períodos estão no teto da escala.** O risco de fogo do INPE vai de 0 a 1; no
   teto não existe aumento possível. Este caso apareceu na validação em tela: Cerrado ·
   GOIÁS deu 1,00 em junho e 1,00 em julho, com classe "Alto" nos dois — o gatilho 1 não
   dispara, e a comparação respondia *"semelhante"*. É verdade e é enganoso: sugere uma
   medida que encontrou igualdade, quando o campo não tinha como encontrar diferença.

**Para comparar territórios, a secura continua sendo a mais legível**, porque distribui
num intervalo largo e as quatro classes aparecem.

---

## Comparar dois períodos do mesmo território

O pacote guarda mais de um período, e a interface deixa escolher um segundo para o mesmo
bioma e a mesma UF. O resultado é uma lista de campos com o formato `A → B (veredito)`.

### O exemplo que a ferramenta produz hoje

**Cerrado · GOIÁS — 2026-06 vs 2026-07**

| Campo | 2026-06 | 2026-07 | Veredito |
|---|---|---|---|
| Focos de calor | 3.483 | 5.384 | aumentou |
| Dias sem chuva (valor típico) | 14 dias | 30 dias | aumentou |
| Chuva registrada (valor típico) | 0 mm | 0 mm | parecido nos dois |
| Calor liberado pelos focos (valor típico) | 5,9 MW | 8,1 MW | aumentou |
| Risco de fogo (índice de 0 a 1) | 1,00 | 1,00 | **os dois ficaram no mesmo patamar; este dado não separa os períodos** |

Os rótulos mudaram na auditoria pedagógica; as contas, não. Onde a tela diz "valor
típico", o número continua sendo a mediana, e a procedência continua dizendo isso.

### A distinção de quatro pontas

**DADO OBSERVADO EM A vs DADO OBSERVADO EM B ≠ CAUSA ≠ PREVISÃO ≠ CALIBRAÇÃO AUTOMÁTICA
DA SIMULAÇÃO.**

Lendo a tabela acima, a frase que se forma sozinha na cabeça é *"secou mais, então
queimou mais"*. Ela pode até estar certa — mas **não é isto que está escrito ali**, e a
diferença não é preciosismo:

| | O que é | O que a tabela sustenta |
|---|---|---|
| **Observação em A e em B** | Dois números medidos, cada um com sua procedência | **Sim.** É tudo o que ela faz. |
| **Causa** | "Os 16 dias a mais sem chuva produziram os 1.901 focos a mais" | **Não.** Nada aqui separa a seca de safra, de política de fiscalização, de cobertura de nuvem, ou de qualquer outra coisa que também mudou entre junho e julho. |
| **Previsão** | "Agosto vai ter mais ainda" | **Não.** Dois pontos não são uma tendência, e o pacote não modela nada. |
| **Calibração da simulação** | Usar 30 dias sem chuva para ajustar a propagação do fogo na caixa | **Não.** Ver a seção seguinte: nenhum valor do pacote toca um solver. |

Por isso o texto que vai à tela é sempre da forma **"no período B houve mais X e também
mais Y"** — nunca "X provocou Y". A ressalva *"São duas medições postas lado a lado. Não
estabelece causa."* acompanha toda comparação, sem exceção, e há um teste que falha se o
vocabulário causal (`causou`, `provocou`, `porque`, `devido`, `resultou`, `por isso`)
aparecer nos textos desta capacidade.

### O critério de "semelhante", e sua honestidade

Dois valores são semelhantes quando a diferença não passa do maior entre:

- **10% da escala** — convenção **declarada, não derivada**. Não há base para afirmar qual
  variação é estatisticamente significativa numa contagem de detecções por satélite
  sujeita a nuvem e a horário de passagem; estimar esse ruído exigiria um estudo que este
  projeto não fez. O que os 10% fazem é evitar os dois erros grosseiros: chamar 2% de
  "aumentou" e chamar 40% de "semelhante".
- **um piso absoluto por campo** — 10 focos, 1 dia, 0,5 mm, 2 MW. Sem ele, "2 focos → 3
  focos" viraria "aumentou 50%", que é precisão falsa sobre ruído.

A atividade conceitual que acompanha a comparação faz uma segunda pergunta
**hipotética** — "e se mudássemos apenas uma condição na caixa?". Ela é hipotética de
propósito: a caixa não reproduz nenhum dos dois períodos, e **nenhuma condição didática é
ajustada automaticamente pelo dado externo. Quem decide é quem dá a aula.**

---

## O que ficou deliberadamente desconectado

Nenhum valor do pacote toca uma simulação. Em particular:

- `risco_fogo` **não** vira probabilidade de propagação
- `precipitacao` **não** vira `ChuvaMmPorSegundo`
- a classe de secura **não** escolhe cobertura nem força de vento

Há uma razão técnica além da pedagógica. `WaterSimulation.IniciarChuva` recebe
**milímetros por segundo**, e os presets são 3, 8 e 18. Um dado real de "150 mm em 24 h" é
**0,0017 mm/s** — jogar 150 no solver seria oitenta mil vezes a tempestade mais forte que
o sistema tem, sem travar e sem avisar.

A conversão correta também enganaria: o solver roda numa caixa de ~1 m² com a infiltração
ajustada para o contraste aparecer em meia hora. **A escala temporal do modelo não é a do
mundo.**

---

## A auditoria pedagógica, e o que ela mudou

A capacidade foi percorrida como quem nunca viu o código: abrir, achar os dados, escolher
território e períodos, ler a comparação, ler a atividade, rodar uma simulação. O que a
travessia encontrou:

| Achado | Antes | Agora |
|---|---|---|
| O rótulo do risco enganava | `Risco de fogo (mediana): 1,00 → Alto` | `Risco de fogo: Alto (índice 1,00, numa escala de 0 a 1)` |
| Identificador cru na tela | `Classificação: relativa_ao_recorte` | "Seco" e "Alto" comparam este território com os outros deste pacote — não são categorias oficiais do INPE |
| Jargão na leitura principal | "mediana", "poder discriminante", "recorte" | "valor típico", "os dois ficaram no mesmo patamar"; os termos exatos ficaram na procedência |
| A ressalva era abstrata | "Não estabelece causa." | "…duas coisas terem mudado juntas não quer dizer que uma tenha mudado a outra." |
| A areia parecia ser o território | nada dizia o contrário | aviso próprio, em amarelo, no quadro de dados |
| A hipótese vinha grudada no dado | uma frase só, com as duas perguntas | quatro blocos: PERGUNTA · OBSERVAÇÃO · HIPÓTESE · EXPERIMENTO |
| "Contexto real" | real em oposição a quê? | "Dados públicos do INPE (experimental)" |
| Dois "comparar" na mesma tela | COMPARAÇÃO (simulações) e comparar períodos | "COMPARAR SIMULAÇÕES" e "Comparar com outro período do mesmo território" |

O roteiro de aula que saiu dela está em [ROTEIRO-DE-AULA.md](ROTEIRO-DE-AULA.md).

**O que ficou como está, de propósito:** a seleção continua num combo único de 84 itens
(`bioma · UF · período`) em vez de três seletores encadeados, e a projeção continua sem
mostrar contexto externo nenhum. As duas coisas são mudanças de estrutura, não de texto, e
ficaram registradas como decisão pendente em vez de resolvidas de passagem.

---

## Limites conhecidos

- **Foco não é incêndio.** É detecção por satélite, sujeita a nuvem, horário de passagem e
  resolução do sensor.
- **Precipitação é quase sempre zero** — é o acumulado do dia da detecção, e o fogo
  acontece onde não choveu.
- **Uma fonte, dois períodos.** Nenhuma generalização para "qualquer dado aberto" nem
  para série temporal foi feita, de propósito. O formato guarda uma lista de períodos, não
  um banco histórico.
- **Dois pontos não são uma série.** A comparação diz o que mudou entre A e B. Ela não
  distingue variação sazonal de tendência, e não deveria ser lida como se distinguisse.
- **A comparação exige o mesmo bioma e a mesma UF.** Dos 84 recortes do pacote, 41
  territórios têm os dois períodos; os demais não oferecem comparação, e a interface
  simplesmente não mostra o seletor.
- **Nunca usado em sala.** A capacidade é experimental no sentido literal.

---

## Segurança

**Nenhuma credencial em lugar nenhum.** A API do dados.gov.br exige chave em todos os
endpoints, e por isso **não é usada** — nem pela ferramenta. Os dados vêm direto do
servidor público do INPE, que responde sem autenticação.

Se algum dia uma fonte exigir chave, ela vai para a ferramenta de preparação (variável de
ambiente ou Windows Credential Manager), **nunca para o aplicativo distribuído**: um
executável não consegue proteger um segredo embutido.
