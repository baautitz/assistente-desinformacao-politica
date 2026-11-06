using AssistenteDesinformacao.Application.Mensageria;

namespace AssistenteDesinformacao.Tests;

public class RepetirMensagemTests
{
    [Fact]
    public void Repete_o_texto_recebido() =>
        Assert.Equal("olá", RepetirMensagem.Responder("olá"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ignora_mensagem_sem_texto(string? texto) =>
        Assert.Null(RepetirMensagem.Responder(texto));
}
