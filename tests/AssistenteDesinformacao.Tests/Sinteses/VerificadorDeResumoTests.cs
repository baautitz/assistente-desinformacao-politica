using AssistenteDesinformacao.Infrastructure.Sinteses;

namespace AssistenteDesinformacao.Tests.Sinteses;

public class VerificadorDeResumoTests
{
    [Theory]
    [InlineData("Motivo: nenhum\nOK", true)]
    [InlineData("Motivo: nenhum\n**OK**", true)]
    [InlineData("ok.", true)]
    [InlineData("<think>talvez OK</think>Motivo: nenhum\nOK", true)]
    [InlineData("Motivo: A. Fato que nenhuma fonte diz.\nFALHA", false)]
    [InlineData("Motivo: nenhum, OK\nFALHA", false)]
    [InlineData("OK, mas há um problema", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void So_um_ok_claro_na_ultima_linha_libera(string? resposta, bool esperado) =>
        Assert.Equal(esperado, VerificadorDeResumoComIa.Interpretar(resposta));
}
