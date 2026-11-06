---
name: entregar
description: Fluxo de entrega antes de push e PR. Use SEMPRE que o dev for dar push, abrir PR ou disser que terminou uma tarefa. Revisa o diff com o dev de forma interativa, explica o que foi feito, só faz push e abre o PR depois da confirmação explícita do dev.
---

Objetivo: o dev é responsável pelo que entrega. Nunca faça push nem abra PR sem passar por este fluxo e sem um "confirmo" explícito do dev. Tudo em pt-BR, tom de colega mais experiente, sem jargão desnecessário (muitos devs aqui estão começando).

## 1. Conferir o básico

- Branch segue `<fix|feat|hotfix>/<id-issue>-slug` (id opcional, sem colchetes). Se não seguir, proponha o nome correto.
- Commits atômicos, em pt-BR, sem menção a IA. Se não, proponha reorganizar (use `commit-atomico`).
- Há diff contra `main`: `git diff main...HEAD` e `git log main..HEAD --oneline`.
- Identifique a issue de origem (`#id` na branch ou nos commits) e leia o corpo com `gh issue view`.

## 2. Revisão guiada (interativa)

Percorra o diff **por área** (arquivo ou grupo de arquivos relacionados), uma de cada vez. Para cada área:

1. **O que faz**: 1 a 3 frases em linguagem simples.
2. **Por quê**: ligue à issue ou à spec.
3. **Veredito**, um destes:
   - ✅ Está de acordo com o projeto (cite o `GLOSSARY.md`, um ADR ou o `CLAUDE.md` quando aplicável).
   - 💡 Recomendação: o que mudaria e por quê, com trecho de código curto se ajudar.
   - ⚠️ Risco ou dúvida: o que pode quebrar ou não ficou claro.
4. **Pausa curta**: pergunte só se o dev quer ajustar algo nessa área ou pode seguir. Não faça perguntas para testar o dev.

Se o dev tiver dúvida, explique com calma. Se discordar de uma recomendação, a decisão é dele; registre e siga.


## 3. Resumo e confirmação

Mostre:
- Lista de pendências 💡 e ⚠️ que o dev decidiu não resolver.
- O rascunho do PR (passo 4).

Pergunte: "Posso fazer o push e abrir o PR?" Só continue com resposta afirmativa clara.

## 4. PR

Escreva o corpo em pt-BR, no formato da skill `pr`, com as seções traduzidas:

- **Resumo**: menor visual que mostre a mudança (árvore, pseudocódigo ou diagrama).
- **Evidência**: antes e depois (saída, print, teste).
- **Risco de merge**: porta de mão única ou de duas mãos, e raio de impacto.
- Linha final `Closes #<id>` quando houver issue (o GitHub só fecha a issue sozinho com palavras-chave em inglês; "Fecha" não funciona).
- Linguagem do `GLOSSARY.md`.

Proibido: qualquer menção a IA, "Generated with", `Co-Authored-By` ou equivalentes, no corpo do PR ou nos commits.

## 5. Publicar

Depois da confirmação: `git push -u origin <branch>` e `gh pr create --title "<pt-BR>" --body "<corpo>"`. Mostre a URL do PR ao dev.
