using AssistenteDesinformacao.Infrastructure.Ia;
using Microsoft.Extensions.AI;

namespace AssistenteDesinformacao.Tests.Ia;

public class ClienteSemRaciocinioTests
{
    [Theory]
    [InlineData("<think>pensando...</think>\n\nResposta final.", "Resposta final.")]
    [InlineData("<think>a</think><think>b</think>Texto", "Texto")]
    [InlineData("Sem raciocínio", "Sem raciocínio")]
    [InlineData("<think>cortado no meio", "")]
    public void Remove_o_raciocinio_do_modelo(string bruto, string esperado) =>
        Assert.Equal(esperado, ClienteSemRaciocinio.Remover(bruto));

    [Fact]
    public async Task Resposta_do_cliente_chega_sem_o_raciocinio()
    {
        var cliente = new ClienteSemRaciocinio(new ClienteFalso("<think>segredo</think>\n\nA agência X classificou como falso."));

        var resposta = await cliente.GetResponseAsync([new ChatMessage(ChatRole.User, "oi")]);

        Assert.Equal("A agência X classificou como falso.", resposta.Text);
    }

    private sealed class ClienteFalso(string texto) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> mensagens, ChatOptions? opcoes = null, CancellationToken ct = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, texto)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> mensagens, ChatOptions? opcoes = null, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public object? GetService(Type tipo, object? chave = null) => null;
        public void Dispose() { }
    }
}
