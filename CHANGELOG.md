# Registro de mudanças

Todas as mudanças relevantes deste projeto ficam aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/), e o
versionamento segue [SemVer](https://semver.org/lang/pt-BR/) — a regra usada aqui está
descrita no [README](README.md#versionamento).

---

## [1.4.0] — 2026-08-28

Primeira versão em que a caixa foi montada, ligada e usada com o sensor de verdade. Boa
parte do que está aqui foi encontrado com a areia na frente, não lendo código.

### Adicionado

- **Camada visual genérica.** Os módulos de simulação declaram campos escalares e um modo
  de cor; o renderizador desenha sem saber quem produziu o quê. Acrescentar um fenômeno
  deixou de exigir mudança no renderizador.
- **Trava de regressão visual** por SHA-256 do quadro renderizado, feita antes da
  refatoração para provar que a imagem não mudou um byte.
- **Contexto público offline do INPE (experimental).** Ferramenta de preparação
  (`tools/CaixaInterativa.DataPrep`) que baixa focos de calor do Programa Queimadas, agrega
  por bioma × UF × período e grava um JSON versionado. O aplicativo só lê o arquivo — não
  há acesso de rede no WPF, nem credencial em lugar nenhum.
- **Comparação temporal entre dois períodos** do mesmo território, com limiar declarado e
  pisos absolutos por campo, e recusa explícita de veredito onde o dado não discrimina.
- **Painel de procedência** do contexto externo: fonte, arquivo, dias observados por
  período, método de agregação e de classificação.
- **Atividade conceitual em quatro blocos** — pergunta, observação, hipótese e experimento —
  separando dado observado, medição da caixa e modelo didático.
- **Queimada na interface**, com força do vento e direção sorteada por incêndio.
- **Ponto de ignição escolhido**: clicar na prévia ateia fogo ali. O foco sorteado continua
  disponível no botão.
- **O mar barra o fogo.** A cota da linha d'água vem da mesma fração que o renderizador usa
  para pintar o azul, de modo que a chama pare exatamente onde o aluno vê o mar começar.
- **Cicatriz de queimada visível**, que fica no mapa depois que a última chama apaga.
- **Assinatura do relevo** nas execuções comparadas, para avisar quando o terreno mudou
  entre duas medições.
- **Aviso quando o near mode pode não ter sido aplicado** ao sensor.
- **Registro de operação em arquivo** (`registro.txt`), em texto simples, com rotação em
  512 KB — para diagnosticar depois o que deu errado durante uma aula.
- **Roteiro de aula** de 5 a 10 minutos e documentação de desenvolvedor.
- **Suíte de testes automatizados: 252 testes.** Não havia nenhum na v1.3 — o projeto de
  testes nasceu junto com a trava de regressão visual. Nenhum deles toca a rede.

### Alterado

- **Renderizador desacoplado** dos fenômenos: não conhece água, terremoto nem fogo.
- **Ciclo de quadro polimórfico** sobre os módulos de simulação.
- **Painel de controle usável em 1366×768**, a resolução dos projetores de escola.
- **Linguagem da interface revista para quem dá aula**: "mediana" virou "valor típico",
  "potência radiativa" virou "calor liberado", e o risco de fogo passou a mostrar a escala
  junto do número — sozinho, `1,00` era lido como "100% de chance de incêndio".
- **Limites do modelo científico ditos na tela**, não só na documentação.
- **Mensagens de erro do Kinect** reescritas para dizer o que fazer.
- **Procedimento de calibração da largura física** documentado.

### Corrigido

- **Tabela de códigos de erro do NUI, errada em quatro das cinco entradas.** O código supunha
  que os valores eram sequenciais a partir de `0x83010001`; não são, e quatro deles o SDK
  deriva de erros do Windows, em outra *facility*. Conferido linha a linha contra o
  `NuiApi.h` do SDK 1.8.
- **`E_NUI_DEVICE_IN_USE` (`0x83010009`) caía no ramo genérico** e a tela mandava conferir
  cabo e fonte de um sensor que estava a 24 fps. Encontrado com o Kinect ligado, com duas
  cópias do aplicativo abertas.
- **"Kinect não encontrado" era dito para um sensor que o driver tinha acabado de enumerar.**
- **A cobertura exibida divergia da aplicada.** Cada início de fonte cria um mapa de solo
  novo, preenchido com areia, enquanto o combo continuava exibindo a escolha do professor.
  Corrigido nos caminhos da interface e, nesta versão, também no da reconexão automática do
  sensor, que inicia a fonte de dentro do motor.
- **Botões da abertura ficavam habilitados sem fonte iniciada**, porque o evento de estado
  só dispara quando o estado muda.
- **Água e fogo:** células molhadas ficavam permanentemente incombustíveis; agora a poça que
  seca devolve a passagem. E o fogo passou a ler o buffer de água vivo, não uma referência
  capturada uma única vez.
- **Recuperação quando o Kinect para de entregar quadros** sem sinalizar falha.
- **Comparação A/B sem assinatura de relevo** comparava execuções feitas sobre terrenos
  diferentes como se fossem a mesma medição.
- **Rótulo de período falso:** um arquivo diário do INPE era promovido a mês inteiro. O
  período passou a vir da data de cada linha, e o pacote grava quantos dias observou.
- **Classificação fabricada:** quando os quartis empatam não existe fronteira que separe
  quatro níveis, e a classe passou a dizer "sem variação suficiente" em vez de inventar uma.

### Limitações conhecidas

- **A largura física coberta pelo sensor ainda precisa ser medida por instalação.** O padrão
  é 1250 mm; sem medir, os valores absolutos ficam proporcionais mas não calibrados.
- **A ROI depende de calibração e configuração** — não é detectada sozinha.
- **Métricas em litros são aproximadas** até a medição física de cada montagem.
- **Os dados do INPE são contexto externo, não calibração.** Nenhum valor do pacote alimenta
  parâmetro de simulação, de propósito. Risco de fogo não vira probabilidade de propagação,
  precipitação não vira chuva no solver.
- **Comparar dois períodos não é uma série temporal.** Dois pontos não distinguem
  sazonalidade de tendência.
- **As capacidades pedagógicas são experimentais e nunca foram usadas em sala.**
- **A projeção não mostra o contexto externo** — ele fica só no painel do professor.

---

## [1.3] — 2026-08-22

Primeira versão distribuída como executável único. Sensor Kinect v1 por P/Invoke,
calibração persistente, chuva e enchente, terremoto, cobertura do solo, projeção alinhável.

## [1.0.1] — 2026-08

Correções da primeira versão pública.

## [1.0.0] — 2026-08

Primeira versão pública.

[1.4.0]: https://github.com/luisfilipegdc/caixadeareia/releases/tag/v1.4.0
[1.3]: https://github.com/luisfilipegdc/caixadeareia/releases/tag/v1.3
[1.0.1]: https://github.com/luisfilipegdc/caixadeareia/releases/tag/v1.0.1
[1.0.0]: https://github.com/luisfilipegdc/caixadeareia/releases/tag/v1.0.0
