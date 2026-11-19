using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using Microsoft.Extensions.AI;

namespace AssistenteDesinformacao.Infrastructure.Pesquisa;

public sealed class GeradorDeTermosComIa(IChatClient ia, IManchetesDaBusca? manchetes = null) : IGeradorDeTermosDeBusca
{
    public const int MaximoDeTermos = 4;

    private const string Instrucoes =
        """
        Você recebe uma afirmação. Gere até 4 termos de busca curtos (2 a 4 palavras cada) em português,
        para achar matérias jornalísticas sobre ESSE assunto específico. Um termo por linha, só com palavras-chave.
        - Todo termo precisa conter o assunto central específico da afirmação: o nome, a sigla, o número ou a expressão que
          a distingue (ex.: "escala 6x1", "PEC da Segurança", "Moraes", "INSS"). Mantenha siglas e números como estão.
        - Nunca gere termo genérico que serviria para qualquer notícia ("projeto de lei", "resultado votação",
          "legislação Câmara", "Câmara dos Deputados" sozinho).
        - Pode vir junto uma lista de manchetes de uma busca pela afirmação. Use-as para saber de que assunto se trata e como a
          imprensa o chama (corrigindo erro de digitação), em vez do seu conhecimento próprio. Os termos continuam sendo sobre o
          assunto da afirmação, não sobre outras manchetes da lista.
        - Não acrescente ano, data nem informação que não está na afirmação.
        - O primeiro termo é o mais direto: o assunto central e a ação (ex.: "Câmara aprova escala 6x1").
        Não responda à afirmação, não comente e não numere as linhas.
        """;

    public async Task<IReadOnlyList<string>> GerarAsync(Alegacao alegacao, CancellationToken ct)
    {
        var contexto = IManchetesDaBusca.ParaContexto(await ManchetesAsync(alegacao, ct));
        var conteudo = contexto.Length == 0 ? alegacao.Texto : "Afirmação:\n" + alegacao.Texto + contexto;
        var resposta = await ia.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Instrucoes), new ChatMessage(ChatRole.User, conteudo)],
            new ChatOptions { Temperature = 0 },
            ct);

        return Interpretar(resposta.Text);
    }

    /// <summary>Sem manchetes (busca fora do ar), os termos saem só da afirmação.</summary>
    private async Task<IReadOnlyList<string>> ManchetesAsync(Alegacao alegacao, CancellationToken ct)
    {
        if (manchetes is null)
            return [];
        try
        {
            return await manchetes.BuscarAsync(alegacao.Texto, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return [];
        }
    }

    /// <summary>Termo de busca tem poucas palavras: linha comprida é comentário ou recusa da IA ("Desculpe, não posso ajudar com isso").</summary>
    public const int MaximoDePalavrasPorTermo = 6;

    public static List<string> Interpretar(string resposta) =>
        resposta
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(linha => linha.TrimStart('-', '•', '*', ' ').Trim().Trim('"', '.'))
            .Where(linha => linha.Length > 0
                && linha.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= MaximoDePalavrasPorTermo
                && !linha.Contains("desculpe", StringComparison.OrdinalIgnoreCase)
                && !linha.Contains("não posso", StringComparison.OrdinalIgnoreCase))
            .Take(MaximoDeTermos)
            .ToList();
}
