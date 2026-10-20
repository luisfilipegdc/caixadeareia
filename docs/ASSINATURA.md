# Assinatura do executável

Por que o Windows avisa, o que resolve e como ligar a assinatura no fluxo de release.

---

## O aviso

Na primeira execução, o Windows mostra **"O Windows protegeu o computador"**, com
*Fornecedor desconhecido*. É o SmartScreen. Ele aparece por dois motivos somados:

1. **O executável não tem assinatura digital.** Sem ela, o Windows não sabe quem publicou
   o arquivo.
2. **O arquivo não tem reputação.** O SmartScreen confia em executáveis que muita gente já
   baixou sem problema. Cada versão nova começa do zero.

A assinatura resolve o primeiro motivo na hora: o aviso passa a mostrar o nome do
publicador. O segundo só se resolve com o tempo. Desde 2024, nem certificado EV dá
reputação imediata; ela vem com os downloads ao longo de algumas semanas.

## O caminho escolhido: SignPath Foundation

A [SignPath Foundation](https://signpath.org) assina gratuitamente projetos de código
aberto. O certificado é da fundação, e o aviso mostra o projeto assinado por ela. Serve a
este projeto porque ele é GPL, público no GitHub e compilado por um fluxo automático — que
é exatamente o que a fundação exige: ela só assina o que foi compilado pelo CI a partir do
código público, nunca um arquivo enviado à mão.

Alternativas, se a inscrição não for aceita:

| Opção | Custo | Observação |
|---|---|---|
| Certum Open Source Code Signing | ~€50–70/ano + leitor de cartão | Em nome de pessoa física, disponível no Brasil |
| Certificado OV comercial (Sectigo, DigiCert…) | US$ 200–500/ano | Exige CNPJ |

## Passo a passo

**Quem faz:** o dono do repositório. A inscrição é em nome do projeto e não pode ser
feita por terceiros.

### 1. Conferir os requisitos

A fundação pede, em resumo: licença de código aberto reconhecida pela OSI (GPL-2.0 é),
repositório público, projeto mantido e com uso real, e build automatizado. Os critérios
atuais estão em <https://signpath.org/terms>.

### 2. Inscrever o projeto

Em <https://signpath.org>, siga *Apply*. Informe:

- **Repositório:** `https://github.com/luisfilipegdc/caixadeareia`
- **Fluxo de build:** `.github/workflows/release.yml`
- **O que é assinado:** um executável Windows (`CaixaInterativa-vX.Y.Z-win-x64.exe`)
- **Para que serve o projeto:** ensino de geografia e ciências com uma caixa de areia e um
  Kinect, usado em escola.

### 3. Configurar no SignPath

Depois de aprovado, no painel do SignPath:

1. Instale o GitHub App do SignPath no repositório, como indicado por eles.
2. Crie (ou confirme) o **projeto** e a **política de assinatura** — por exemplo
   `release-signing`.
3. Gere um **API token** para um usuário de CI com permissão de enviar pedidos de
   assinatura.

Anote o **Organization ID**, o **slug do projeto** e o **slug da política**.

### 4. Colocar os valores no GitHub

Em *Settings → Secrets and variables → Actions* do repositório:

| Tipo | Nome | Valor |
|---|---|---|
| **Secret** | `SIGNPATH_API_TOKEN` | o token do passo 3 |
| Variable | `SIGNPATH_ORGANIZATION_ID` | o Organization ID |
| Variable | `SIGNPATH_PROJECT_SLUG` | o slug do projeto |
| Variable | `SIGNPATH_SIGNING_POLICY_SLUG` | o slug da política |

O token é segredo: nunca entra em arquivo, commit, issue ou print.

### 5. Testar

Em *Actions → Release → Run workflow*, rode o fluxo na `main`. Ele compila, testa, envia o
executável ao SignPath, espera a assinatura e confere com `Get-AuthenticodeSignature`
antes de montar o zip. O resultado fica nos artefatos da execução. Se a assinatura vier
inválida, o fluxo falha e nada é publicado.

Sem o `SIGNPATH_API_TOKEN`, o mesmo fluxo roda e gera o executável **sem** assinatura —
dá para usá-lo antes da aprovação.

## Como uma release passa a sair

1. Atualize `<Version>` no `CaixaInterativa.csproj` e o `CHANGELOG.md` (passos 3 e 4 do
   [RELEASE.md](RELEASE.md)).
2. Depois do merge na `main`, crie e envie a tag:
   ```bash
   git tag -a v1.5.0 -m "Caixa de Areia Interativa v1.5.0"
   git push origin v1.5.0
   ```
3. O fluxo cria uma **release em rascunho** com o `.exe`, o `.zip` e o `SHA256SUMS.txt`.
   Ele recusa a tag se ela não bater com o `<Version>` do csproj.
4. Escreva as notas, baixe o anexo, confira o hash e publique (passo 11 do RELEASE.md).
