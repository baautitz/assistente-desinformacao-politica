# Assistente de Combate à Desinformação Política

Bot do Telegram que confere se um texto ou print político tem contexto antes de você repassar.

Você manda a mensagem. O bot identifica a alegação, procura o que matérias e agências de checagem já publicaram sobre ela e responde com um resumo curto e os links das fontes, cada um com a data de publicação.

Projeto da disciplina de Inteligência Artificial, curso de Sistemas de Informação, Centro Universitário Dinâmica das Cataratas.

> Primeira entrega completa. Melhorias e pendências ficam nas [issues](../../issues).

## Sumário

- [O que faz](#o-que-faz) e [o que não faz](#o-que-não-faz)
- [Como funciona](#como-funciona)
- [Começando em 5 minutos (desenvolvimento)](#começando-em-5-minutos-desenvolvimento)
- [Variáveis de ambiente](#variáveis-de-ambiente)
- [Desenvolvimento](#desenvolvimento)
- [Testes](#testes)
- [Deploy na VPS](#deploy-na-vps)
- [Problemas comuns](#problemas-comuns)
- [Estrutura do código](#estrutura-do-código)
- [Contribuindo](#contribuindo)

## O que faz

- Recebe texto ou print de tela pelo Telegram, sem cadastro.
- Extrai a alegação central da mensagem (até 3 por consulta).
- Busca checagens na Google Fact Check Tools API e matérias de uma lista de feeds RSS.
- Responde com um resumo neutro, os links e as datas das fontes.
- Aceita 👍 ou 👎 em cada resposta, para avaliarmos se ela ajudou.
- Limita as consultas por usuário (padrão: 5 por minuto).
- Comandos: `/start` explica o que o bot faz e não faz; `/metricas` ou `/metrics` (só para o `ADMIN_TELEGRAM_ID`) mostra consultas, votos e tempo médio.

## O que não faz

- Não diz se algo é verdadeiro ou falso. Quando uma agência classificou a alegação, o bot cita a classificação e o nome da agência.
- Não recomenda candidato, partido ou voto, nem quando o texto recebido pede isso (veja o filtro de neutralidade abaixo).
- Não guarda mensagens nem identifica quem perguntou. Só ficam salvos o voto do 👍/👎 (sem texto), os tempos de resposta e as matérias coletadas dos feeds. O ID do usuário existe apenas na memória do limitador de consultas.
- Se não achar nenhuma fonte, diz que não encontrou. Não inventa resposta.

## Como funciona

```
Telegram ──► Worker ──► ResponderConsulta
                          │
                          ├─ 1. Extrai alegações (texto, ou print via modelo de visão)
                          ├─ 2. Busca fontes (em paralelo)
                          │      ├─ Google Fact Check Tools API   (Checagens)
                          │      └─ Postgres: texto + pgvector    (Matérias dos RSS, por relevância)
                          ├─ 3. Juiz de relevância (IA): descarta fonte que não trata da alegação
                          │      └─ nenhuma serviu? reescreve a alegação e busca de novo (até 3 tentativas)
                          ├─ 4. Redige a Síntese com a IA, só com o que as fontes dizem
                          └─ 5. Valida antes de enviar (links, veredito, neutralidade)

Enquanto isso o bot mostra "digitando..." no Telegram até a resposta ficar pronta.

ColetorWorker ──► lê os feeds de fontes.yaml de tempos em tempos ──► Postgres
```

Regras que o código garante antes de enviar qualquer resposta:

- Toda Síntese traz pelo menos um link de uma fonte **relevante**. Sem fonte relevante (mesmo depois de reformular a busca), a resposta é "não encontrei apuração": o bot não lista link só para parecer que achou algo.
- Veredito só aparece quando uma agência de checagem o publicou, e sempre com o nome dela.
- **Filtro de neutralidade sobre o que o bot escreve**: o usuário pode mandar qualquer texto, inclusive notícia sensacionalista ou sobre candidatos (é justamente o que o bot existe para conferir). Mas o resumo gerado pela IA nunca pode recomendar voto, candidato ou partido. Um atalho barato (regex) pega os casos óbvios ("vote 22", "melhor candidato") e os demais passam por um classificador de IA (temperatura 0, resposta SIM/NAO). Na dúvida ou em caso de erro, **bloqueia**: só uma resposta clara "NAO" libera. Se o resumo for barrado, o bot manda só a lista de fontes.
- Checagens de agências têm prioridade sobre matérias no corte das 5 fontes citadas.

Termos do domínio (Alegação, Fonte, Matéria, Checagem, Síntese, Feedback, Consulta) estão no [GLOSSARY.md](GLOSSARY.md).

## Começando em 5 minutos (desenvolvimento)

Pré-requisitos: [Docker](https://docs.docker.com/get-docker/) com Compose, e as chaves abaixo.

1. Crie um bot com o [@BotFather](https://t.me/BotFather) e guarde o token.
2. Tenha uma chave de um provedor de IA compatível com a API da OpenAI e uma chave da [Google Fact Check Tools API](https://developers.google.com/fact-check/tools/api).
3. Clone e configure:

   ```bash
   git clone https://github.com/baautitz/assistente-desinformacao-politica.git
   cd assistente-desinformacao-politica
   cp .env.example .env
   # edite o .env: TELEGRAM_BOT_TOKEN, IA_CHAVE, GOOGLE_FACT_CHECK_API_KEY (e troque POSTGRES_PASSWORD)
   ```

4. Suba tudo, buildando o código local:

   ```bash
   docker compose up --build
   ```

5. Abra o seu bot no Telegram e mande `/start`.

O `docker-compose.override.yml` é carregado sozinho: builda a imagem do código local (em vez de baixar a publicada) e expõe o Postgres em `localhost:5432`. As migrations rodam na subida do bot.

## Variáveis de ambiente

Ficam no arquivo `.env` (modelo em [`.env.example`](.env.example)). O `.env` e o `.env.example` precisam andar juntos: ao criar uma variável nova, adicione nos dois lugares e nesta tabela.

| Variável | Obrigatória | Padrão | Para que serve |
| --- | --- | --- | --- |
| `TELEGRAM_BOT_TOKEN` | sim | - | Token do bot, gerado pelo [@BotFather](https://t.me/BotFather). |
| `IA_URL_BASE` | sim | `https://api.openai.com/v1` | URL base do provedor de IA compatível com a API da OpenAI (OpenAI, OpenRouter, Groq, Ollama...). |
| `IA_CHAVE` | sim | - | Chave de API do provedor de IA. |
| `IA_MODELO_TEXTO` | sim | `gpt-4o-mini` | Modelo usado para extrair a alegação, redigir a síntese e classificar a neutralidade. |
| `IA_CAMPOS_EXTRAS` | não | vazio | Objeto JSON com campos extras enviados em toda chamada de chat, para opções que só alguns provedores aceitam. No MiniMax, `{"thinking":{"type":"disabled"}}` desliga o raciocínio e a resposta cai de ~60s para ~2s. Outros provedores podem recusar campos desconhecidos. |
| `IA_MODELO_VISAO` | não | vazio | Modelo de visão para ler prints. Sem ele, o bot responde que só lê texto. |
| `IA_MODELO_EMBEDDINGS` | não | vazio | Modelo de embeddings (ex.: `text-embedding-3-small`) do mesmo provedor. Com ele, a coleta gera o vetor de cada matéria nova e a consulta gera o da Alegação, e a busca combina sentido (pgvector) e texto. Sem ele, só texto. |
| `GOOGLE_FACT_CHECK_API_KEY` | sim | - | Chave da Google Fact Check Tools API. |
| `FACT_CHECK_IDIOMA` | não | `pt-BR` | Idioma das checagens buscadas. |
| `ADMIN_TELEGRAM_ID` | não | vazio | ID numérico do Telegram de quem pode usar `/metricas` (só agregados, sem texto). Sem ele, o comando é ignorado. |
| `LIMITE_CONSULTAS_POR_MINUTO` | não | `5` | Consultas por minuto permitidas a cada usuário. |
| `COLETA_JANELA_DIAS` | não | `90` | Matérias mais antigas que isso são descartadas na coleta de RSS e ignoradas na busca. |
| `COLETA_INTERVALO_MINUTOS` | não | `60` | Intervalo entre rodadas de coleta. |
| `FONTES_ARQUIVO` | não | `fontes.yaml` ao lado do executável | Caminho de outro arquivo de feeds, se quiser trocar a lista sem rebuildar. |
| `ConnectionStrings__Postgres` | sim | montada pelo compose | String de conexão do Postgres. No `docker compose` você não precisa definir: ela é montada a partir das três variáveis `POSTGRES_*`. Defina só ao rodar o bot fora do Docker. |
| `POSTGRES_USER` | sim | `bot` | Usuário do Postgres (lido pelo `docker compose`). |
| `POSTGRES_PASSWORD` | sim | `troque-esta-senha` | Senha do Postgres. Troque antes de subir. |
| `POSTGRES_DB` | sim | `desinformacao` | Nome do banco. |
| `TESTE_POSTGRES` | não | vazio | Só para testes. Veja [Testes](#testes). |

O provedor de IA é configurado por URL base, chave e modelo, então dá para trocar de provedor sem mexer no código. Se trocar o modelo de embeddings, as matérias já coletadas deixam de entrar na busca por sentido (continuam acháveis por texto).

## Desenvolvimento

### Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download) (o projeto usa `net10.0`)
- Docker com Compose (para o Postgres)

### Rodando o bot fora do Docker

Útil para depurar. O Postgres sobe no Docker e o bot roda com `dotnet run`:

```bash
docker compose up -d db                  # Postgres com pgvector em localhost:5432

set -a; source .env; set +a              # o dotnet run não lê o .env sozinho
export ConnectionStrings__Postgres="Host=localhost;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD;Database=$POSTGRES_DB"

dotnet run --project src/AssistenteDesinformacao.Bot
```

Alternativa sem exportar variáveis: guarde as chaves com `dotnet user-secrets set "TELEGRAM_BOT_TOKEN" "..." --project src/AssistenteDesinformacao.Bot` (o projeto já tem um `UserSecretsId`; vale no ambiente `Development`, que o `dotnet run` usa por padrão).

### Banco de dados e migrations

As migrations (EF Core) são aplicadas automaticamente quando o bot sobe. Para criar uma nova depois de mudar uma entidade:

```bash
dotnet tool install --global dotnet-ef   # uma vez só
dotnet ef migrations add NomeDaMudanca \
  --project src/AssistenteDesinformacao.Infrastructure \
  --startup-project src/AssistenteDesinformacao.Bot \
  --output-dir Persistencia/Migrations
```

O Postgres do compose usa a imagem `pgvector/pgvector:pg18`, que já traz a extensão `pgvector`; a primeira migration a ativa sozinha.

### Feeds RSS

A lista de veículos fica em [`src/AssistenteDesinformacao.Bot/fontes.yaml`](src/AssistenteDesinformacao.Bot/fontes.yaml). Para incluir um veículo, adicione um item com `veiculo` e `url`. O `ColetorWorker` lê todos a cada `COLETA_INTERVALO_MINUTOS`; um feed que falha não derruba os outros, e o motivo aparece nos logs.

### Dicas

- Para ver só a IA e a busca sem Telegram, rode os testes (sem rede, com doubles) ou o roteiro do classificador (veja [Testes](#testes)).
- Sem `IA_MODELO_VISAO`, prints recebem a mensagem de que o bot só lê texto. Sem `IA_MODELO_EMBEDDINGS`, a busca é só textual (Postgres full-text em português).

## Testes

```bash
dotnet test
```

Roda sem rede e sem chaves: IA e fontes são substituídas por doubles. Cobrem as regras de domínio e anti-alucinação (link obrigatório, filtro eleitoral, veredito só com agência, "não encontrei apuração"), o limitador, o feedback, as métricas e a coleta.

**Testes com Postgres real.** A busca textual e a vetorial só rodam com `TESTE_POSTGRES` apontando para um Postgres **descartável** com pgvector (os testes recriam o banco):

```bash
docker run -d -p 55437:5432 -e POSTGRES_PASSWORD=x pgvector/pgvector:pg18
export TESTE_POSTGRES="Host=localhost;Port=55437;Username=postgres;Password=x;Database=teste"
dotnet test
```

Sem a variável, esses testes passam sem executar nada.

**Filtro de neutralidade contra o modelo real.** Exporte `IA_URL_BASE`, `IA_CHAVE` e `IA_MODELO_TEXTO` (os mesmos do `.env`) e rode:

```bash
dotnet test --filter ClassificadorDeNeutralidadeRealTests
```

Sem essas variáveis, esses testes passam sem medir nada. Rode antes de apresentar o projeto ou ao trocar de modelo.

A CI ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) builda, roda os testes e valida os arquivos do Compose em todo PR.

## Deploy na VPS

Qualquer VPS Linux com acesso à internet serve (os exemplos usam Ubuntu/Debian). O bot usa long polling, então não precisa abrir porta nem ter domínio.

A imagem é publicada pela CI em `ghcr.io/baautitz/assistente-desinformacao-politica` **a cada merge na `main`** (tags `latest` e o SHA do commit). Antes do primeiro merge, não há imagem para baixar.

### 1. Preparar o servidor

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER   # saia e entre de novo na sessão SSH
docker compose version          # confirma que o Compose está instalado
```

### 2. Clonar o repositório

```bash
git clone https://github.com/baautitz/assistente-desinformacao-politica.git
cd assistente-desinformacao-politica
```

### 3. Configurar o `.env`

```bash
cp .env.example .env
nano .env        # preencha as variáveis obrigatórias (tabela acima)
chmod 600 .env
```

O `.env` fica fora do repositório. Nunca faça commit dele. Troque a `POSTGRES_PASSWORD`.

### 4. Subir

```bash
docker compose -f docker-compose.yml up -d
```

O `-f docker-compose.yml` faz o Compose ignorar o `docker-compose.override.yml` (que é só para desenvolvimento) e baixar a imagem publicada. Se o pacote estiver privado, faça `docker login ghcr.io` antes, com um token do GitHub com permissão `read:packages`.

Confira com `docker compose -f docker-compose.yml ps`: `bot` e `db` devem estar `running`. Depois mande `/start` ao bot no Telegram.

### 5. Atualizar

```bash
git pull
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
```

O volume `dados` do Postgres é preservado. Se o `.env.example` ganhou variáveis novas, adicione-as ao seu `.env` antes do `up`. Para fixar uma versão, troque `latest` no `docker-compose.yml` pela tag do SHA.

### Logs e reinício

```bash
docker compose -f docker-compose.yml logs -f bot          # acompanha os logs (Ctrl+C sai)
docker compose -f docker-compose.yml logs --tail 100 bot  # últimas 100 linhas
docker compose -f docker-compose.yml restart bot          # reinicia só o bot
docker compose -f docker-compose.yml down                 # para tudo (os dados ficam no volume)
```

O log mostra cada etapa de uma consulta, sem o texto da mensagem nem quem enviou (só contagens e decisões):

```
Consulta recebida (texto)
Alegações extraídas: 1
Chamada à IA em 9.8s (412 tokens de entrada, 530 de saída)
BuscadorNaFactCheck: 3 fontes em 640 ms
BuscadorDeMaterias: 8 fontes em 11200 ms
Busca 1/3: 11 candidatas (3 checagens), 1 relevantes
Resumo aprovado pelos guardrails; 1 fontes (1 checagens)
Consulta respondida em 31.2s (1 fontes)
```

Também são registrados os votos (`Feedback registrado: 👍 útil` ou `👎 não ajudou`), o uso do `/metricas` (`Métricas enviadas ao administrador` ou o motivo de ter sido ignorado) e o limite de consultas por minuto. O ID de quem votou ou pediu nunca vai para o log.

Quando nenhuma fonte serve, aparece `reformulando a alegação para a tentativa 2`. Se um guardrail barrar o resumo, aparece `Resumo descartado (motivo)`. As chamadas lentas de IA aparecem na linha `Chamada à IA`.

Os containers têm `restart: unless-stopped`, então voltam sozinhos após reboot da VPS. Depois de mudar o `.env`, rode `up -d` de novo: um `restart` não relê o arquivo.

## Problemas comuns

| Sintoma | Causa provável |
| --- | --- |
| O bot cai na subida com "Defina TELEGRAM_BOT_TOKEN" ou "variável obrigatória" | Falta uma variável obrigatória no `.env`. Veja a tabela. |
| O bot não responde no Telegram | Token errado, ou outra instância do mesmo bot rodando (o Telegram só entrega as mensagens a uma). Veja os logs. |
| "Só leio texto" ao mandar um print | `IA_MODELO_VISAO` está vazio. |
| Respostas sempre "não encontrei apuração" | A coleta ainda não rodou (espere a primeira rodada), os feeds mudaram de endereço ou a chave da Fact Check está inválida. Veja os logs do `ColetorWorker`. |
| `pull access denied` no `compose up` da VPS | A imagem ainda não foi publicada (falta merge na `main`) ou o pacote está privado: `docker login ghcr.io`. |
| Erro de extensão `vector` no Postgres | O volume foi criado com outra imagem. Use `pgvector/pgvector:pg18` (mesma major do `postgres:18`, sem perder dados). |
| Mudei o `.env` e nada mudou | Rode `docker compose up -d` de novo; `restart` não relê o arquivo. |

## Estrutura do código

Aplicação em C# (.NET 10), monolito modular em quatro projetos. Os contextos (Mensageria, Análise, Pesquisa, Síntese, Feedback) ficam separados por pasta e namespace dentro deles.

```
src/
  AssistenteDesinformacao.Domain/          # tipos puros: Alegação, Matéria, Checagem, Síntese, Feedback
  AssistenteDesinformacao.Application/     # casos de uso e portas (interfaces): ResponderConsulta, ColetarFeeds, limites, métricas
  AssistenteDesinformacao.Infrastructure/  # IA, Fact Check, RSS, EF Core/Postgres (migrations), injeção de dependências
  AssistenteDesinformacao.Bot/             # Worker do Telegram, ColetorWorker, Program, fontes.yaml
tests/
  AssistenteDesinformacao.Tests/           # xUnit: regras de domínio e anti-alucinação
```

A dependência vai sempre de fora para dentro: `Bot` → `Infrastructure` → `Application` → `Domain`. O caso de uso central é o `ResponderConsulta`, testado com doubles para a IA e as fontes.

Principais tecnologias: [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) (long polling), `Microsoft.Extensions.AI` com cliente OpenAI-compatível, EF Core + Npgsql, Postgres 18 com full-text search e [pgvector](https://github.com/pgvector/pgvector) (busca híbrida combinada por RRF), YamlDotNet e `System.ServiceModel.Syndication` (RSS).

## Contribuindo

Convenções de branch, commit e PR estão no [CLAUDE.md](CLAUDE.md). Resumo:

- Tudo em português (pt-BR): código de domínio, commits, issues e PRs.
- Branch: `<fix|feat|hotfix>/<id-da-issue>-descricao-em-slug` (ex.: `feat/12-cadastro-de-usuario`).
- Commits atômicos, em uma linha, no formato Conventional Commits (`feat(escopo): descrição`).
- Testes xUnit para o essencial: domínio e regras anti-alucinação.
- Dependências sempre na última versão estável; imagens Docker com a major fixada (ex.: `postgres:18`), nunca `latest`.
- Todo termo novo do domínio entra no [GLOSSARY.md](GLOSSARY.md).
