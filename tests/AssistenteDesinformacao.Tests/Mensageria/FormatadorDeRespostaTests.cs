using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Domain.Sinteses;

namespace AssistenteDesinformacao.Tests.Mensageria;

public class FormatadorDeRespostaTests
{
    [Fact]
    public void Mostra_link_data_e_classificacao_atribuida_a_agencia()
    {
        var sintese = new Sintese("Resumo.",
        [
            new Checagem("Vídeo antigo", new Uri("https://agencia.example/checagem"), new DateOnly(2024, 8, 5), "Agência A", "Falso"),
            new Fonte("Matéria", new Uri("https://jornal.example/materia"), null, "Jornal B"),
        ]);

        var texto = FormatadorDeResposta.Formatar(sintese);

        Assert.StartsWith("Resumo.", texto);
        Assert.Contains("Agência A (05/08/2024): classificou como \"Falso\"", texto);
        Assert.Contains("https://agencia.example/checagem", texto);
        Assert.Contains("Jornal B (sem data): Matéria", texto);
        Assert.Contains("https://jornal.example/materia", texto);
    }

    [Fact]
    public void Sem_fontes_mostra_so_o_resumo() =>
        Assert.Equal(MensagensDoBot.SemApuracao,
            FormatadorDeResposta.Formatar(new Sintese(MensagensDoBot.SemApuracao, [])));
}
