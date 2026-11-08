using AssistenteDesinformacao.Application.Mensageria;

namespace AssistenteDesinformacao.Tests.Mensageria;

public class ComandosDoBotTests
{
    [Theory]
    [InlineData("/metricas")]
    [InlineData("/metrics")]
    [InlineData("/METRICAS")]
    [InlineData("  /metrics  ")]
    [InlineData("/metricas@meu_bot")]
    [InlineData("/metrics qualquer coisa")]
    public void Reconhece_o_comando_de_metricas(string texto) => Assert.True(ComandosDoBot.EhMetricas(texto));

    [Theory]
    [InlineData("metricas")]
    [InlineData("/metricas2")]
    [InlineData("quero ver /metricas")]
    [InlineData("")]
    [InlineData(null)]
    public void Texto_comum_nao_e_o_comando(string? texto) => Assert.False(ComandosDoBot.EhMetricas(texto));
}
