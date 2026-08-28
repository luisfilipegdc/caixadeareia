# Como publicar uma release

Onze passos, na ordem. Os comandos são os que foram realmente usados na `v1.4.0` — não
um exemplo genérico.

> **Regra que vale mais que o resto:** se qualquer passo de 1 a 4 falhar, **não publique**.
> Corrija ou pare. Uma release que sai com teste vermelho vira suporte, não entrega.

Substitua `1.4.0` pela versão nova em todos os comandos.

---

## 1. Confirmar o estado

```bash
git rev-parse --abbrev-ref HEAD && git rev-parse HEAD && git status --porcelain
```

A árvore precisa estar limpa. Se `main` tiver commits que a branch não tem, traga-os
**antes** — publicar sem eles regride funcionalidade em silêncio:

```bash
git fetch origin && git log --oneline HEAD..origin/main
git merge origin/main
```

## 2. Build e testes

```bash
dotnet build CaixaInterativa.sln -c Release
dotnet test tests/CaixaInterativa.Tests/CaixaInterativa.Tests.csproj -c Release
```

Anote a contagem de testes e confirme **zero avisos**. O aviso de hoje é o erro de amanhã.

## 3. Versão

Uma linha, um arquivo. Não há número de versão escrito à mão em nenhum outro lugar do
código — `AppInfo` lê do assembly, e título da janela, tela de suporte e propriedades do
executável leem de `AppInfo`.

```
src/CaixaInterativa/CaixaInterativa.csproj  →  <Version>1.4.0</Version>
```

Confira depois de compilar:

```powershell
(Get-Item .\release-v1.4.0\CaixaInterativa-v1.4.0-win-x64.exe).VersionInfo.FileVersion
```

## 4. Changelog

Atualize [`CHANGELOG.md`](../CHANGELOG.md) com uma seção da versão nova, em
*Adicionado · Alterado · Corrigido · Limitações conhecidas*.

**Escreva a partir do histórico, não da memória:**

```bash
git log --oneline v1.3..HEAD
```

Não invente correção. Se não está no histórico ou no código, não entra.

## 5. README

- a linha de versão estável no topo;
- o link de download apontando para `releases/latest` — **nunca** para um arquivo com o
  número da versão no caminho, que quebra na release seguinte.

## 6. Build da release

```bash
dotnet publish src/CaixaInterativa/CaixaInterativa.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -p:DebugSymbols=false \
  -o ./release-1.4.0
```

`DebugType=none` tira o `.pdb`; `EnableCompressionInSingleFile` mantém o executável em
~68 MB em vez de ~154 MB.

Renomeie para `CaixaInterativa-v1.4.0-win-x64.exe` e monte o `.zip` com o executável, a
pasta `Dados/` e o `config.default.json`.

**O que nunca pode entrar:** `config.json` pessoal, `calibracao.dat`, `registro.txt`,
`.pdb`, backups, capturas de tela, builds antigas, qualquer credencial.

> `registro.txt` aparece sozinho ao lado do executável no primeiro teste. Apague antes de
> montar os anexos.

## 7. Checksums

```powershell
Get-FileHash .\release-1.4.0\CaixaInterativa-v1.4.0-win-x64.exe -Algorithm SHA256
```

Grave um `SHA256SUMS.txt` no formato `hash<dois espaços>nome`, em UTF-8 sem BOM.

## 8. Segurança

```bash
git grep -nIE "ghp_[A-Za-z0-9]{20,}|github_pat_|eyJ[A-Za-z0-9_-]{20,}\.|Bearer |Authorization:"
git ls-files | grep -iE "\.env$|secret|token|credential|\.pem$|calibracao\.dat|config\.json"
```

Se aparecer alguma coisa: **pare**, e informe apenas o arquivo — nunca o conteúdo.

## 9. Pull request

```bash
git push origin <branch>
gh pr create --base main --title "release: v1.4.0" --body-file <arquivo>
```

Sem `--force`. Sem reescrever histórico já publicado.

## 10. Tag

**Só depois do merge em `main`**, e apontando para o commit que foi mergeado:

```bash
git checkout main && git pull
git tag -a v1.4.0 -m "Caixa de Areia Interativa v1.4.0"
git push origin v1.4.0
```

Taguear a branch antes do merge deixa a tag apontando para um commit que não é o publicado.

## 11. GitHub Release e validação

```bash
gh release create v1.4.0 \
  ./release-1.4.0/CaixaInterativa-v1.4.0-win-x64.exe \
  ./release-1.4.0/CaixaInterativa-v1.4.0-win-x64.zip \
  ./release-1.4.0/SHA256SUMS.txt \
  --title "Caixa de Areia Interativa v1.4.0" --notes-file <arquivo>
```

Nas notas, separe o que é **estável** do que é **experimental**. Uma versão estável pode
conter capacidades experimentais; esconder isso é que não pode.

Depois de publicar, **baixe o próprio anexo** e confira:

```powershell
gh release download v1.4.0 -D .\conferencia
Get-FileHash .\conferencia\CaixaInterativa-v1.4.0-win-x64.exe -Algorithm SHA256
```

O hash tem de bater com o `SHA256SUMS.txt`, e o executável baixado tem de abrir mostrando
a versão certa na barra de título. É o que garante que o arquivo que o professor baixa é
o mesmo que foi testado.
