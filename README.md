# Assistente de Combate à Desinformação Política

Bot do Telegram que confere se um texto ou print político tem contexto antes de você repassar.

Você manda a mensagem. O bot identifica a alegação, procura o que matérias e agências de checagem já publicaram sobre ela e responde com um resumo curto e os links das fontes, cada um com a data de publicação.

Projeto da disciplina de Inteligência Artificial, curso de Sistemas de Informação, Centro Universitário Dinâmica das Cataratas.

> Em desenvolvimento. O que está pronto e o que falta está nas [issues](../../issues).

## O que faz

- Recebe texto ou print de tela pelo Telegram, sem cadastro.
- Extrai a alegação central da mensagem.
- Busca checagens na Google Fact Check Tools API e matérias de uma lista de feeds RSS.
- Responde com um resumo neutro, os links e as datas das fontes.
- Aceita 👍 ou 👎 em cada resposta, para avaliarmos se ela ajudou.

## O que não faz

- Não diz se algo é verdadeiro ou falso. Quando uma agência classificou a alegação, o bot cita a classificação e o nome da agência.
- Não recomenda candidato, partido ou voto.
- Não guarda mensagens nem identifica quem perguntou. Só ficam salvos o voto do 👍/👎 (sem texto) e as matérias coletadas dos feeds.
- Se não achar nenhuma fonte, diz que não encontrou. Não inventa resposta.

## Variáveis de ambiente

Ficam no arquivo `.env` (modelo em [`.env.example`](.env.example)). As duas listas precisam andar juntas: ao criar uma variável nova, adicione nos dois lugares.

| Variável | Obrigatória | Padrão | Para que serve |
| --- | --- | --- | --- |
| `TELEGRAM_BOT_TOKEN` | sim | - | Token do bot, gerado pelo [@BotFather](https://t.me/BotFather). |
| `IA_URL_BASE` | sim | `https://api.openai.com/v1` | URL base do provedor de IA compatível com a API da OpenAI (OpenAI, OpenRouter, Groq, Ollama...). |
| `IA_CHAVE` | sim | - | Chave de API do provedor de IA. |
| `IA_MODELO_TEXTO` | sim | `gpt-4o-mini` | Modelo usado para extrair a alegação e redigir a síntese. |
| `GOOGLE_FACT_CHECK_API_KEY` | sim | - | Chave da Google Fact Check Tools API. |
| `FACT_CHECK_IDIOMA` | não | `pt-BR` | Idioma das checagens buscadas. |
| `LIMITE_CONSULTAS_POR_MINUTO` | não | `5` | Consultas por minuto permitidas a cada usuário. |
| `COLETA_JANELA_DIAS` | não | `90` | Matérias mais antigas que isso são descartadas na coleta de RSS. |
| `COLETA_INTERVALO_MINUTOS` | não | `60` | Intervalo entre rodadas de coleta. Os feeds ficam em `src/AssistenteDesinformacao.Bot/fontes.yaml`. |
| `POSTGRES_USER` | sim | `bot` | Usuário do Postgres (lido pelo `docker compose`). |
| `POSTGRES_PASSWORD` | sim | `troque-esta-senha` | Senha do Postgres. Troque antes de subir. |
| `POSTGRES_DB` | sim | `desinformacao` | Nome do banco. |

O provedor de IA é configurado por URL base, chave e modelo, então dá para trocar de provedor sem mexer no código.

## Deploy na VPS

Qualquer VPS Linux com acesso à internet serve (os exemplos usam Ubuntu/Debian). O bot usa long polling, então não precisa abrir porta nem ter domínio.

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

1. Crie o bot com o [@BotFather](https://t.me/BotFather) e guarde o token.
2. Tenha a chave do provedor de IA e a chave da Google Fact Check Tools API.
3. Copie o exemplo e preencha todas as variáveis obrigatórias (tabela acima):

   ```bash
   cp .env.example .env
   nano .env
   chmod 600 .env
   ```

O `.env` fica fora do repositório. Nunca faça commit dele.

### 4. Subir

```bash
docker compose -f docker-compose.yml up -d
```

O `-f docker-compose.yml` faz o Compose ignorar o `docker-compose.override.yml` (que é só para desenvolvimento) e baixar a imagem publicada em `ghcr.io/baautitz/assistente-desinformacao-politica`. Se o pacote estiver privado, faça `docker login ghcr.io` antes, com um token do GitHub com permissão `read:packages`.

Confira com `docker compose -f docker-compose.yml ps`: `bot` e `db` devem estar `running`.

### 5. Atualizar

```bash
git pull
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
```

O volume `dados` do Postgres é preservado. Se o `.env.example` ganhou variáveis novas, adicione-as ao seu `.env` antes do `up`.

### Logs e reinício

```bash
docker compose -f docker-compose.yml logs -f bot          # acompanha os logs do bot (Ctrl+C sai)
docker compose -f docker-compose.yml logs --tail 100 bot  # últimas 100 linhas
docker compose -f docker-compose.yml restart bot          # reinicia só o bot
docker compose -f docker-compose.yml down                 # para tudo (os dados ficam no volume)
```

Os containers têm `restart: unless-stopped`, então voltam sozinhos após reboot da VPS. Depois de mudar o `.env`, rode `up -d` de novo: um `restart` não relê o arquivo.

## Desenvolvimento local

`docker compose up --build` builda a imagem do código local e expõe o Postgres na porta 5432 (usa o `docker-compose.override.yml`, que o Compose carrega sozinho). Sem modelo de imagem configurado, o bot só lê texto.

## Como rodar os testes

```bash
dotnet test
```

## Estrutura

Aplicação em C# (.NET), organizada em quatro projetos: `Domain`, `Application`, `Infrastructure` e `Bot`. Os contextos (mensageria, análise, pesquisa, síntese e feedback) ficam separados por pasta dentro deles. Banco: Postgres. Termos do domínio no [glossário](GLOSSARY.md).

## Contribuindo

Convenções de branch, commit e PR estão no [CLAUDE.md](CLAUDE.md). Tudo em português.
