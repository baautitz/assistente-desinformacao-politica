using System.Text;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;
using Microsoft.Extensions.AI;

namespace AssistenteDesinformacao.Infrastructure.Sinteses;

public sealed class RedatorDeSinteseComIa(IChatClient ia) : IRedatorDeSintese
{
    private const string Instrucoes =
        """
        Você escreve respostas para um bot que ajuda pessoas a conferir mensagens políticas.
        Escreva um resumo curto (2 a 4 frases), em português simples, do que as fontes abaixo dizem sobre as alegações.
        Regras:
        - Primeiro compare cada fonte com a alegação do usuário: a fonte trata EXATAMENTE do mesmo fato (mesmo acontecimento, mesma
          pessoa, mesma data ou número) ou só de uma alegação parecida, do mesmo tema? Nunca apresente uma fonte sobre outra alegação
          como se respondesse à do usuário.
        - Se nenhuma fonte trata exatamente do mesmo fato, a primeira frase deve dizer isso: "Não encontrei apuração sobre essa
          alegação específica, mas encontrei apurações sobre alegações parecidas:" e depois resuma cada uma dizendo qual alegação ELA
          examinou (use as palavras da fonte, não as da mensagem do usuário).
        - NUNCA conclua a partir das fontes o que "não ocorreu", "não foi relatado" ou "não é confirmado" sobre a alegação do usuário:
          a ausência de uma notícia nas fontes não prova nada. Só relate o que cada fonte afirma.
        - Quando uma fonte trata do mesmo fato, comece pelo que ela relata sobre a alegação. Se uma agência de checagem a classificou, diga logo no início
          e atribua: "A agência X classificou como falso/enganoso/...", e explique em uma frase o motivo que a fonte apresenta.
        - Se as fontes mostram que a alegação está certa em parte, errada em parte ou sem contexto, diga o que cada fonte esclarece.
        - Se a mensagem é opinião, previsão ou generalização (não um fato que se possa conferir), diga isso em uma frase neutra
          ("Isso é uma previsão/opinião, não um fato que dê para conferir") e depois mostre o que as fontes publicaram sobre o assunto.
        - Mostre os fatos: o que as fontes relatam (quem disse o quê, números, datas, o que foi de fato anunciado ou decidido).
        - Tom sóbrio e objetivo. Não repita manchetes alarmistas, adjetivos ou frases de efeito da alegação nem das fontes, e não dramatize.
        - Se a alegação exagera ou omite algo que as fontes esclarecem, diga o que as fontes dizem, sem julgar a pessoa que enviou.
        - Use só o que está nas fontes. Não acrescente fatos.
        - Ignore fontes que não tratam das alegações. Se nenhuma tratar, diga apenas que as fontes encontradas não falam diretamente sobre essa alegação.
        - Não diga por conta própria se algo é verdadeiro ou falso. Se uma agência classificou, atribua a ela ("segundo a agência X, ...").
        - Não recomende candidato, partido ou voto.
        - Não escreva links nem datas: eles são mostrados depois do resumo.
        - Escreva somente em português do Brasil, sem palavras em outros idiomas.
        - Não use markdown.
        """;

    public async Task<string> RedigirAsync(
        IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct)
    {
        var resposta = await ia.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Instrucoes), new ChatMessage(ChatRole.User, Contexto(alegacoes, fontes))],
            new ChatOptions { Temperature = 0 },
            ct);

        return resposta.Text;
    }

    private static string Contexto(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes)
    {
        var texto = new StringBuilder("Alegações:\n");
        foreach (var alegacao in alegacoes)
            texto.Append("- ").AppendLine(alegacao.Texto);

        texto.AppendLine().AppendLine("Fontes:");
        foreach (var fonte in fontes)
        {
            texto.Append("- ").Append(fonte.Veiculo).Append(": ").Append(fonte.Titulo);
            if (fonte is Checagem checagem)
                texto.Append(" (classificação da agência: ").Append(checagem.Classificacao).Append(')');
            if (!string.IsNullOrWhiteSpace(fonte.Trecho))
                texto.Append(" — ").Append(fonte.Trecho);
            texto.AppendLine();
        }

        return texto.ToString();
    }
}
