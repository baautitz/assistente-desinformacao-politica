using AssistenteDesinformacao.Infrastructure.Analise;
using AssistenteDesinformacao.Infrastructure.Pesquisa;

namespace AssistenteDesinformacao.Tests.Ia;

public class InterpretacaoDaIaTests
{
    [Theory]
    [InlineData("NENHUMA")]
    [InlineData("NENHUMA.")]
    [InlineData("**Nenhuma**")]
    public void Extrator_entende_nenhuma_com_pontuacao_como_sem_alegacao(string resposta) =>
        Assert.Empty(ExtratorDeAlegacoesComIa.Interpretar(resposta));

    [Fact]
    public void Gerador_descarta_recusa_e_comentario_da_ia()
    {
        var termos = GeradorDeTermosComIa.Interpretar(
            "Câmara aprova escala 6x1\nDesculpe, não posso ajudar com isso.\n- \"fim da escala 6x1\"\nAqui estão alguns termos que podem ajudar a encontrar matérias");

        Assert.Equal(["Câmara aprova escala 6x1", "fim da escala 6x1"], termos);
    }
}
