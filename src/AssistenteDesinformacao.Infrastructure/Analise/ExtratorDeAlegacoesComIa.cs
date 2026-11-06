using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Domain.Analise;
using Microsoft.Extensions.AI;

namespace AssistenteDesinformacao.Infrastructure.Analise;

public sealed class ExtratorDeAlegacoesComIa(IChatClient ia) : IExtratorDeAlegacoes
{
    public const string SemAlegacao = "NENHUMA";

    private const string Instrucoes =
        $"""
        Você recebe uma mensagem que circulou em grupos de mensagem.
        Liste as afirmações de fato verificáveis que ela faz (no máximo 3), uma por linha.
        Escreva cada afirmação como uma frase curta e objetiva em português, boa para buscar em sites de checagem.
        Não julgue se é verdade, não comente e não numere as linhas.
        Ignore opiniões, xingamentos e pedidos de voto.
        Se não houver nenhuma afirmação de fato verificável, responda apenas {SemAlegacao}.
        """;

    public async Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct)
    {
        var resposta = await ia.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Instrucoes), new ChatMessage(ChatRole.User, texto)],
            new ChatOptions { Temperature = 0 },
            ct);

        return Interpretar(resposta.Text);
    }

    private static List<Alegacao> Interpretar(string resposta) =>
        resposta
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(linha => linha.TrimStart('-', '•', '*', ' ').Trim())
            .Where(linha => linha.Length > 0 && !linha.Equals(SemAlegacao, StringComparison.OrdinalIgnoreCase))
            .Select(linha => new Alegacao(linha))
            .ToList();
}
