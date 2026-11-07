using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using Microsoft.Extensions.AI;

namespace AssistenteDesinformacao.Infrastructure.Pesquisa;

public sealed class GeradorDeTermosComIa(IChatClient ia) : IGeradorDeTermosDeBusca
{
    public const int MaximoDeTermos = 4;

    private const string Instrucoes =
        """
        Você recebe uma afirmação. Gere até 4 termos de busca curtos (2 a 4 palavras cada) em português,
        para achar matérias jornalísticas sobre o assunto. Um termo por linha, só com palavras-chave
        (nomes, lugares, temas, números). Não responda à afirmação, não comente e não numere as linhas.
        """;

    public async Task<IReadOnlyList<string>> GerarAsync(Alegacao alegacao, CancellationToken ct)
    {
        var resposta = await ia.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Instrucoes), new ChatMessage(ChatRole.User, alegacao.Texto)],
            new ChatOptions { Temperature = 0 },
            ct);

        return Interpretar(resposta.Text);
    }

    internal static List<string> Interpretar(string resposta) =>
        resposta
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(linha => linha.TrimStart('-', '•', '*', ' ').Trim())
            .Where(linha => linha.Length > 0)
            .Take(MaximoDeTermos)
            .ToList();
}
