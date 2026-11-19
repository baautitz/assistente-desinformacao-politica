using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using Microsoft.Extensions.AI;

namespace AssistenteDesinformacao.Infrastructure.Analise;

public sealed class ExtratorDeAlegacoesComIa(IChatClient ia) : IExtratorDeAlegacoes
{
    public const string SemAlegacao = "NENHUMA";

    private const string Instrucoes =
        $"""
        Você recebe uma mensagem que circulou em grupos de mensagem.
        Leia a mensagem inteira antes de responder e liste as afirmações de fato verificáveis que ela faz (no máximo 3), uma por linha.
        Mensagem curta, manchete ou pergunta (uma frase só) é UMA afirmação: nunca quebre uma frase em várias afirmações.
        Só liste mais de uma quando a mensagem traz fatos realmente independentes.
        Pode vir junto uma lista de manchetes de uma busca pelo texto da mensagem. Use-as SÓ para entender de que assunto a
        mensagem trata e como a imprensa chama esse assunto (ex.: um termo com erro de digitação). Não use seu conhecimento próprio
        para interpretar a mensagem, e não transforme as manchetes em afirmações: as afirmações são as da mensagem.
        {RegrasDeSaida}
        """;

    internal const string RegrasDeSaida =
        $"""
        Escreva cada afirmação como uma frase curta e objetiva em português, boa para buscar em sites de checagem.
        Mantenha exatamente os termos específicos da mensagem: nomes, siglas, números e expressões como "6x1", "PEC", "PL 2630".
        Não reinterprete nem explique esses termos (ex.: um número não vira placar nem resultado de votação).
        Corrija só erros de digitação óbvios ("camara" → "Câmara", "refroma" → "reforma"), sem trocar o sentido.
        Não invente detalhes que a mensagem não tem (local, data, resultado, números, autor).
        Não julgue se é verdade, não comente e não numere as linhas.
        Ignore opiniões, xingamentos e pedidos de voto.
        Se não houver nenhuma afirmação de fato verificável, responda apenas {SemAlegacao}.
        """;

    public Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct) => ExtrairAsync(texto, [], ct);

    public async Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, IReadOnlyList<string> manchetes, CancellationToken ct)
    {
        var conteudo = manchetes.Count == 0 ? texto : "Mensagem:\n" + texto + IManchetesDaBusca.ParaContexto(manchetes);
        var resposta = await ia.GetResponseAsync(
            [new ChatMessage(ChatRole.System, Instrucoes), new ChatMessage(ChatRole.User, conteudo)],
            new ChatOptions { Temperature = 0 },
            ct);

        return Interpretar(resposta.Text);
    }

    public static List<Alegacao> Interpretar(string resposta) =>
        resposta
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(linha => linha.TrimStart('-', '•', '*', ' ').Trim())
            // "NENHUMA." ou "**NENHUMA**" também é a resposta de que não há afirmação, não uma afirmação chamada "NENHUMA".
            .Where(linha => linha.Length > 0 && !linha.Trim('.', '*', '"', '\'', ' ').Equals(SemAlegacao, StringComparison.OrdinalIgnoreCase))
            .Select(linha => new Alegacao(linha))
            .ToList();
}
