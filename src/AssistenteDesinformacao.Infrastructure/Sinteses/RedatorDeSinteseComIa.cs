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
        - Comece pelo que as fontes relatam sobre a alegação. Se uma agência de checagem a classificou, diga logo no início
          e atribua: "A agência X classificou como falso/enganoso/...", e explique em uma frase o motivo que a fonte apresenta.
        - Se as fontes mostram que a alegação está certa em parte, errada em parte ou sem contexto, diga o que cada fonte esclarece.
        - Mostre os fatos: o que as fontes relatam (quem disse o quê, números, datas, o que foi de fato anunciado ou decidido).
        - Tom sóbrio e objetivo. Não repita manchetes alarmistas, adjetivos ou frases de efeito da alegação nem das fontes, e não dramatize.
        - Se a alegação exagera ou omite algo que as fontes esclarecem, diga o que as fontes dizem, sem julgar a pessoa que enviou.
        - Use só o que está nas fontes. Não acrescente fatos.
        - Ignore fontes que não tratam das alegações. Se nenhuma tratar, diga apenas que as fontes encontradas não falam diretamente sobre essa alegação.
        - Não diga por conta própria se algo é verdadeiro ou falso. Se uma agência classificou, atribua a ela ("segundo a agência X, ...").
        - Não recomende candidato, partido ou voto.
        - Não escreva links nem datas: eles são mostrados depois do resumo.
        - Não use markdown.
        """;

    public async Task<string> RedigirAsync(
        IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct)
    {
        var resposta = await ia.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Instrucoes), new ChatMessage(ChatRole.User, Contexto(alegacoes, fontes))],
            new ChatOptions { Temperature = 0.2f },
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
