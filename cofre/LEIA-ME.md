# Cofre

Esta pasta guarda credenciais de produção **na sua máquina**. O `.gitignore` está
configurado para que **nada aqui, exceto este arquivo, chegue ao repositório**.

> ⚠️ O repositório é público. Um segredo commitado aqui fica visível para qualquer
> pessoa e permanece no histórico do git mesmo depois de apagado. Trocar a credencial
> depois não desfaz a exposição — bots varrem o GitHub em minutos.

---

## Como usar

Crie os arquivos que precisar dentro desta pasta. Eles não serão versionados:

```
cofre/
├── LEIA-ME.md          ← único arquivo versionado
├── producao.env        ← ignorado
├── vercel.token        ← ignorado
└── kinect-licenca.txt  ← ignorado
```

Um arquivo `.env` tem uma variável por linha:

```
VERCEL_TOKEN=valor_aqui
GITHUB_TOKEN=valor_aqui
```

---

## Onde cada tipo de segredo deve morar

Guardar tudo num arquivo local funciona para você sozinho. Quando o projeto for
para produção de verdade, cada tipo tem um lugar melhor:

| Segredo | Lugar certo | Por quê |
|---|---|---|
| Token do GitHub | `gh auth login -w` | Fica no Windows Credential Manager, cifrado pelo sistema |
| Token de deploy (Vercel etc.) | Secrets do próprio serviço | O serviço injeta na hora do build; nunca toca sua máquina |
| Chaves usadas pelo CI | GitHub → Settings → Secrets | Ficam cifradas e mascaradas nos logs |
| Senhas pessoais | Gerenciador de senhas | Bitwarden, 1Password, KeePass |
| Config local do app | `config.json` (já ignorado) | Não é segredo, mas é específico da máquina |

---

## Proteção automática

O repositório tem um hook que **bloqueia commits contendo padrões de segredo** —
tokens do GitHub, chaves da AWS, chaves privadas, chaves de API. Ele roda sozinho
antes de cada commit.

Para ativar numa cópia nova do repositório:

```bash
git config core.hooksPath .githooks
```

Se o hook bloquear um commit legítimo (um exemplo na documentação, digamos), revise
com cuidado antes de contornar com `git commit --no-verify`. O hook errar é bem mais
barato que um vazamento.

---

## Se um segredo vazar

Ordem importa — **revogue primeiro, limpe depois**:

1. **Revogue a credencial imediatamente** no serviço que a emitiu. Enquanto ela for
   válida, o histórico não importa.
2. Gere uma nova.
3. Só então se preocupe em limpar o histórico do git.

Um segredo que passou por chat, e-mail, print de tela ou repositório público deve ser
considerado comprometido para sempre, mesmo que pareça que ninguém viu.

---

## Rotação

Quando o projeto sair de desenvolvimento, troque todas as credenciais que foram usadas
durante a construção. Anote aqui a data da última rotação:

| Credencial | Serviço | Última rotação |
|---|---|---|
| | | |
