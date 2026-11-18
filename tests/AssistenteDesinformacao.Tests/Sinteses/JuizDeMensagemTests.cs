using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Infrastructure.Analise;

namespace AssistenteDesinformacao.Tests.Sinteses;

public class JuizDeMensagemTests
{
    [Theory]
    [InlineData("AFIRMACAO", TipoDeMensagem.Afirmacao)]
    [InlineData("PEDIDO_DE_VOTO", TipoDeMensagem.PedidoDeVoto)]
    [InlineData("PEDIDO_DE_OPINIAO", TipoDeMensagem.PedidoDeOpiniao)]
    [InlineData("pedido_de_opinião", TipoDeMensagem.PedidoDeOpiniao)]
    [InlineData("OPINIAO", TipoDeMensagem.Opiniao)]
    [InlineData("FORA_DO_ESCOPO.", TipoDeMensagem.ForaDoEscopo)]
    [InlineData("**PEDIDO_DE_VOTO**", TipoDeMensagem.PedidoDeVoto)]
    [InlineData("<think>talvez OPINIAO</think>PEDIDO_DE_OPINIAO", TipoDeMensagem.PedidoDeOpiniao)]
    public void Le_a_categoria(string resposta, TipoDeMensagem esperado) =>
        Assert.Equal(esperado, JuizDeMensagemComIa.Interpretar(resposta));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("não sei classificar")]
    [InlineData("PEDIDO_DE_VOTO ou OPINIAO")]
    public void Resposta_confusa_segue_como_afirmacao_no_fluxo_normal(string? resposta) =>
        Assert.Equal(TipoDeMensagem.Afirmacao, JuizDeMensagemComIa.Interpretar(resposta));
}
