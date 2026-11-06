---
name: commit-atomico
description: Cria commits atomicos em Conventional Commits, uma linha, no idioma pedido. Use SEMPRE que for criar um commit neste projeto (padrao ptbr), ou quando o usuario pedir /commit-atomico ptbr, /commit-atomico en ou "commita atomico".
argument-hint: "[ptbr|en]"
---

Idioma da mensagem: `$ARGUMENTS` (`ptbr` = portugues do Brasil, `en` = ingles). Se vazio, use `ptbr`.

1. Rode `git status` e `git diff` para ver tudo que mudou. Veja `git log --oneline -5` para seguir o estilo do repo (scopes).
2. Agrupe as mudancas em commits atomicos: uma unica intencao logica por commit, mesmo que duas intencoes estejam no mesmo arquivo.
3. Se um arquivo mistura intencoes, separe com staging parcial (`git add -p` nao e interativo aqui: use `git apply --cached` com um patch, ou remova temporariamente o trecho, faca o commit e restaure). Confira com `git diff --cached` antes de cada commit.
4. Mensagem: Conventional Commits em **uma linha**, `tipo(escopo): descricao`, no imperativo/presente, minuscula, sem ponto final, sem acentos se o repo nao usa. Tipos: feat, fix, refactor, docs, style, test, chore, perf, build, ci.
5. NUNCA inclua `Co-Authored-By` nem qualquer atribuicao no commit.
6. Nao faca push. Nao inclua arquivos nao relacionados ou nao versionados de proposito (ex.: `.claude/`).
7. Ao final, mostre `git log --oneline` dos commits criados e confirme que a arvore ficou limpa.
