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

## Como subir

Precisa de Docker com Compose e de um servidor (ou sua máquina) com acesso à internet.

1. Crie um bot no Telegram com o [@BotFather](https://t.me/BotFather) e guarde o token.
2. Tenha uma chave de uma API de IA compatível com OpenAI e uma chave da Google Fact Check Tools API.
3. Copie o arquivo de exemplo e preencha:

   ```bash
   cp .env.example .env
   ```

4. Suba:

   ```bash
   docker compose -f docker-compose.yml up -d
   ```

O `.env` fica fora do repositório. Nunca faça commit dele.

Para desenvolver, `docker compose up --build` builda a imagem do código local e expõe o Postgres na porta 5432 (usa o `docker-compose.override.yml`, que o Compose carrega sozinho). Por isso o passo 4 usa `-f docker-compose.yml`: na VPS o arquivo é ignorado e a imagem publicada é baixada.

O provedor de IA é configurado por URL base, chave e modelo, então dá para trocar de provedor sem mexer no código. Os modelos de texto, de imagem e de embeddings são configurados separadamente. Sem modelo de imagem, o bot só lê texto.

## Como rodar os testes

```bash
dotnet test
```

## Estrutura

Aplicação em C# (.NET), organizada em quatro projetos: `Domain`, `Application`, `Infrastructure` e `Bot`. Os contextos (mensageria, análise, pesquisa, síntese e feedback) ficam separados por pasta dentro deles. Banco: Postgres. Termos do domínio no [glossário](GLOSSARY.md).

## Contribuindo

Convenções de branch, commit e PR estão no [CLAUDE.md](CLAUDE.md). Tudo em português.
