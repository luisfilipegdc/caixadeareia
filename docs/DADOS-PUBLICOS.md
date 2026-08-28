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
  --fonte https://dataserver-coids.inpe.br/queimadas/queimadas/focos/csv/diario/Brasil/focos_diario_br_20260827.csv \
  --saida src/CaixaInterativa/Dados/contexto-queimadas.json
```

O comando também fica gravado **dentro do próprio pacote**, no campo
`proveniencia.comandoParaRegenerar` — quem abrir o JSON descobre como refazê-lo sem
procurar documentação.

`--fonte` aceita URL ou caminho local. Sem `--saida`, o JSON sai na saída padrão.
`--ajuda` mostra o uso.

Para trocar o dia, mude a data no nome do arquivo. Os diários do Brasil ficam em
`.../focos/csv/diario/Brasil/` e pesam alguns MB; o mensal existe, mas tem ~85 MB e não é
necessário para o que a aula usa.

---

## O que o pacote contém

```json
{
  "schemaVersion": 1,
  "proveniencia": {
    "fonte": "INPE — Programa Queimadas",
    "recurso": "focos_diario_br_20260827.csv",
    "url": "https://dataserver-coids.inpe.br/...",
    "periodoObservado": "2026-08",
    "dataDeAcesso": "2026-08-28",
    "comandoParaRegenerar": "dotnet run --project tools/...",
    "filtros": [...],
    "metodoDeAgregacao": "...",
    "metodoDeClassificacao": "...",
    "observacoes": [...]
  },
  "contextos": [
    {
      "bioma": "Cerrado", "uf": "BAHIA", "periodo": "2026-08",
      "observado": {
        "focos": 975,
        "riscoFogoMediano": 1.0, "riscoFogoP25": 0.9, "riscoFogoP75": 1.0,
        "diasSemChuvaMediano": 79.0, "frpMedianoMw": 76.4,
        "amostras": { "riscoFogo": 975, "diasSemChuva": 975, ... }
      },
      "classesDidaticas": {
        "risco": "Sem variação suficiente",
        "secura": "Muito seco",
        "classificacao": "relativa_ao_recorte"
      }
    }
  ]
}
```

Estado atual: **29 recortes, 22 KB**, gerado do dia 27/08/2026 (28.519 focos).

---

## As decisões estatísticas, e por quê

### Mediana e quartis, nunca média

Medido na amostra real: a média de FRP é **43,7 MW** e a mediana **11,5 MW**. A cauda de
incêndios enormes puxa a média para longe do que é típico do conjunto.

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

Isso acontece de verdade com o risco de fogo: no pacote atual, **29 de 29 recortes**
caem nesse caso, porque nos focos *detectados* o risco satura perto de 1 — o fogo acontece
justamente onde o risco é alto. Uma escala forçada colocaria todo bioma em "crítico", o
que é tecnicamente defensável e pedagogicamente inútil.

**Para comparar territórios, use a secura**, que distribui bem: no pacote atual vai de 0 a
79 dias sem chuva, e as quatro classes aparecem.

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

## Limites conhecidos

- **Foco não é incêndio.** É detecção por satélite, sujeita a nuvem, horário de passagem e
  resolução do sensor.
- **Precipitação é quase sempre zero** — é o acumulado do dia da detecção, e o fogo
  acontece onde não choveu.
- **Uma fonte, um período.** Nenhuma generalização para "qualquer dado aberto" foi feita,
  de propósito.
- **Nunca usado em sala.** A capacidade é experimental no sentido literal.

---

## Segurança

**Nenhuma credencial em lugar nenhum.** A API do dados.gov.br exige chave em todos os
endpoints, e por isso **não é usada** — nem pela ferramenta. Os dados vêm direto do
servidor público do INPE, que responde sem autenticação.

Se algum dia uma fonte exigir chave, ela vai para a ferramenta de preparação (variável de
ambiente ou Windows Credential Manager), **nunca para o aplicativo distribuído**: um
executável não consegue proteger um segredo embutido.
