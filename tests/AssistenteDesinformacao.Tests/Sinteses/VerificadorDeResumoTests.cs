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

    [Theory]
    [InlineData("Motivo: D. A data 27/10 não aparece em nenhuma fonte.\nFALHA", "D. A data 27/10 não aparece em nenhuma fonte.")]
    [InlineData("<think>pensando</think>Motivo: A. Número inventado\nFALHA", "A. Número inventado")]
    [InlineData("FALHA", "o resumo traz algo que as fontes não dizem")]
    [InlineData("Motivo: nenhum\nFALHA", "o resumo traz algo que as fontes não dizem")]
    public void Motivo_da_recusa_volta_para_o_redator(string resposta, string esperado) =>
        Assert.Equal(esperado, VerificadorDeResumoComIa.Motivo(resposta));
}
