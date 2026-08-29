# Validação com o Kinect — Urbanização e Enchentes

> **Para executar, não para investigar.** O software, os testes, a interface, a projeção e
> a documentação estão prontos e validados **no simulador**. O que falta é medir como a
> atividade se comporta com o sensor lendo areia de verdade.
>
> **15 a 30 minutos**, se nenhum defeito aparecer.

**A distinção que não pode ser perdida:** o simulador valida o *software*; o Kinect valida
o comportamento com a *montagem real*. Nenhum número deste checklist pode ser substituído
por um número de simulador.

---

## 1 · Hardware — 5 min

| | Como conferir | Anotar |
|---|---|---|
| Kinect conectado | Gerenciador de dispositivos, ou o PowerShell abaixo | |
| Sensor enumerado | O painel diz "1 sensor(es) Kinect detectado(s)" | |
| Near mode | Rodapé diz `Kinect v1 (near mode)` | |
| FPS | Rodapé, canto inferior direito | ____ fps |
| Projetor ligado e alinhado | Abrir projeção; a borda bate na moldura | |
| Calibração | Alise a areia → *Nivelar e calibrar* | ____ % de cobertura |

```powershell
Get-CimInstance Win32_PnPEntity | Where-Object { $_.DeviceID -match "VID_045E" } | Select-Object Name, Status
```

Se aparecer vazio, o sensor não está no sistema — **pare aqui**, é cabo ou energia.

> Cobertura abaixo de ~95% costuma indicar areia brilhante, sol na sala, ou o sensor
> enxergando além da moldura.

---

## 2 · Clique — 3 min

Cobertura **Mata**, simulação **Queimada**. Clicar na prévia em três pontos e ver de que
lado o fogo nasce **na areia**.

| Clique na prévia | Fogo na areia | ✓ |
|---|---|---|
| esquerda | esperado: esquerda | |
| centro | esperado: centro | |
| direita | esperado: direita | |

Validando **orientação**, não precisão. Alguns centímetros de desvio em relevo alto são o
**paralaxe do projetor**, já documentado no [roadmap](../ROADMAP.md) — **não é este teste**
e não se corrige aqui.

Depois de cada fogo, **troque a cobertura e volte** para limpar a cicatriz do solo, senão
o próximo clique cai em terreno queimado e não pega.

---

## 3 · Controle Mata → Mata — 5 min

**É a medição mais importante da validação.** Ela responde: quando nada deveria mudar, o
resultado se repete o suficiente para que uma troca de cobertura seja distinguível?

Molde um relevo e **não encoste mais nele**.

1. **Urbanização e Enchentes** → *Fazer chover* → anote o pico de A
2. **Encerrar atividade**
3. **Urbanização e Enchentes** → *Fazer chover* → anote o pico de A de novo
4. Repita uma terceira vez

| | Pico | fps |
|---|---|---|
| Mata #1 | ____ % | ____ |
| Mata #2 | ____ % | ____ |
| Mata #3 | ____ % | ____ |
| **Maior diferença** | **____ pontos** | |

*Referência de laboratório: com terreno constante a variação é 0,000 pontos. Na areia real
haverá alguma — o ruído do sensor é a variável que o simulador não reproduz.*

---

## 4 · Experimento Mata → Área urbana — 4 min

**Sem tocar no relevo**, do começo:

1. **Urbanização e Enchentes** → *Fazer chover* → pico A
2. **Passo B · Área urbana** → *Fazer chover* → pico B

| | Pico |
|---|---|
| Mata (A) | ____ % |
| Área urbana (B) | ____ % |
| **Diferença** | **____ pontos** |

Se o passo B for **recusado**, anote o motivo — relevo mudou ou sensor reiniciou. É a
invariante trabalhando, não defeito.

**Não persiga os números históricos** (~47–48% e ~67–68%). São referência, não meta. Se
der diferente, investigue; não ajuste nada.

---

## 5 · A conta que decide

```
variação do controle  = maior diferença entre as três execuções de Mata
efeito do experimento = |Mata − Área urbana|
```

- **Efeito claramente maior que a variação** → a comparação se sustenta na caixa real.
- **Efeito da mesma ordem da variação** → **não está pronta**. Não ajuste coeficientes;
  registre e pare.

---

## 6 · Invariantes — 5 min

Cada uma deve **bloquear** ou **invalidar com mensagem clara**. Nunca sobrar comparação de
aparência válida.

| Ação | Esperado | ✓ |
|---|---|---|
| Mexer na areia entre A e B | *"O relevo mudou entre as execuções…"* | |
| Reiniciar a fonte no meio | *"O sensor foi reiniciado…"* | |
| Trocar cobertura durante | controle desabilitado | |
| Trocar intensidade | controle desabilitado | |
| Trocar duração | controle desabilitado | |
| Executar A duas vezes | botão indisponível; pico de A não muda | |
| Executar B duas vezes | idem | |
| Encerrar e recomeçar | começa limpa, sem herdar A | |

---

## 7 · UX e projeção — 3 min

| | Esperado | ✓ |
|---|---|---|
| Cliques de ponta a ponta | 4 | |
| Rolagens | 0 | |
| Projeção em A | `A · MATA` · `MESMA CHUVA · MANTENHA O RELEVO` | |
| Projeção em B | `B · ÁREA URBANA` · `MESMA CHUVA · MESMO RELEVO` | |
| Comparação projetada | dois números grandes, legíveis do fundo | |
| *Voltar ao mapa na projeção* | devolve o relevo sem encerrar | |

---

## 8 · Evidências

Guarde antes de fechar o programa:

- **`registro.txt`**, ao lado do executável. Cada atividade grava uma linha com sessão,
  fonte, fps, near mode, menor profundidade e resumo do relevo, mais o pico de cada passo
  e o motivo de qualquer invalidação. É o que permite reconstruir a sessão depois.
- **Fotos da areia** com a projeção em A, em B e na comparação.
- **Capturas do painel** do professor nos mesmos três momentos.
- Os **números** das seções 3 e 4.

---

## 9 · Decisão

| Veredito | Quando |
|---|---|
| **PRONTO PARA PR** | Clique coerente nos três eixos · controle medido · efeito claramente maior que a variação · invariantes funcionando · projeção legível |
| **PRECISA AJUSTE** | Algo pequeno e identificado falhou |
| **NÃO ESTÁ PRONTO** | Controle varia tanto quanto o efeito, ou invariante não segura |

**Se o veredito for PRONTO PARA PR**, o PR pode ser aberto — a branch já está pronta em
todo o resto. Se não, o que falhou vira o próximo trabalho, **sem mexer em coeficientes**.
