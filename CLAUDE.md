## Convenções

- Todo texto de PRs, issues e commits deve ser escrito em **português (pt-BR)**.
- **NUNCA** adicionar `Co-Authored-By`, "Generated with", "colaboração com" ou qualquer menção a nome de agente de IA, em commit, issue, PR ou qualquer outro lugar.
- Branches: `<tag>/<id-issue>-descricao-em-slug-simples-e-direta`, onde `tag` é `fix`, `feat` ou `hotfix`. O `<id-issue>` é opcional (branch sem issue omite essa parte) e nunca vai entre colchetes. Exemplos: `feat/12-cadastro-de-usuario`, `fix/corrige-calculo-de-media`.
- Commits: usar `/commit-atomico` em pt-BR.

## Agent skills

### Issue tracker

Issues vivem no GitHub Issues (via `gh`). Ver `docs/agents/issue-tracker.md`.

### Triage labels

Vocabulário padrão: needs-triage, needs-info, ready-for-agent, ready-for-human, wontfix. Ver `docs/agents/triage-labels.md`.

### Domain docs

Single-context (um `GLOSSARY.md` + `docs/adr/` na raiz). Ver `docs/agents/domain.md`.

## Fluxo de trabalho (automático)

As skills do projeto estão em `.claude/skills/` e carregam sozinhas. Siga a tabela sem esperar o usuário chamar comandos:

| Situação | Skill |
| --- | --- |
| Ideia nova ou requisito ambíguo | `grill-with-docs` (entrevista antes de implementar; atualiza `GLOSSARY.md` e `docs/adr/`) |
| Entrevista concluída, trabalho de várias sessões | `to-spec`, depois `to-tickets` (peça confirmação antes de criar issues no GitHub) |
| Implementar uma issue | Ler a issue, implementar direto e escrever testes xUnit só do essencial (domínio e regras anti-alucinação) |
| Bug difícil, intermitente ou regressão | `diagnosing-bugs` |
| Termo de domínio confuso ou decisão difícil de reverter | `domain-modeling` |
| Criar commit | `commit-atomico` (pt-BR) |
| Terminou a tarefa, vai dar push ou abrir PR | `entregar` (revisão guiada com o dev, confirmação e depois PR no formato da skill `pr`) |
| Nenhuma se aplica | Pergunte ao usuário antes de assumir |

**Regra de entrega:** nunca rode `git push` nem `gh pr create` sem antes passar pela skill `entregar` e receber a confirmação explícita do dev.
