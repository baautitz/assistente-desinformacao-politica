using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AssistenteDesinformacao.Infrastructure.Ia;

/// <summary>Registra quanto cada chamada de IA demora e gasta de tokens. Nunca registra o conteúdo das mensagens.</summary>
public sealed class ClienteComLog(IChatClient interno, ILogger<ClienteComLog> logger) : DelegatingChatClient(interno)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> mensagens, ChatOptions? opcoes = null, CancellationToken ct = default)
    {
        var cronometro = Stopwatch.StartNew();
        try
        {
            var resposta = await base.GetResponseAsync(mensagens, opcoes, ct);
            logger.LogInformation("Chamada à IA em {Segundos:F1}s ({TokensDeEntrada} tokens de entrada, {TokensDeSaida} de saída)",
                cronometro.Elapsed.TotalSeconds, resposta.Usage?.InputTokenCount, resposta.Usage?.OutputTokenCount);
            return resposta;
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogWarning("Chamada à IA falhou após {Segundos:F1}s: {Tipo}", cronometro.Elapsed.TotalSeconds, erro.GetType().Name);
            throw;
        }
    }
}
