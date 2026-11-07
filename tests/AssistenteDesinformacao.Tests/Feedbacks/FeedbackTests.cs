using AssistenteDesinformacao.Application.Feedbacks;
using AssistenteDesinformacao.Domain.Feedbacks;
using AssistenteDesinformacao.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Tests.Feedbacks;

public class FeedbackTests
{
    private static BancoDeDados NovoBanco(string nome) =>
        new(new DbContextOptionsBuilder<BancoDeDados>().UseInMemoryDatabase(nome).Options);

    [Fact]
    public void Dados_do_botao_cabem_no_limite_do_telegram_e_fazem_ida_e_volta()
    {
        var id = Guid.NewGuid();
        var dados = DadosDoBotao.Gerar(id, false);

        Assert.True(System.Text.Encoding.UTF8.GetByteCount(dados) <= 64);
        Assert.True(DadosDoBotao.TentarLer(dados, out var lido, out var util));
        Assert.Equal(id, lido);
        Assert.False(util);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("outro:123")]
    [InlineData("fb:naoeguid:1")]
    [InlineData("fb:00000000000000000000000000000000:2")]
    public void Dados_invalidos_nao_sao_feedback(string? dados) =>
        Assert.False(DadosDoBotao.TentarLer(dados, out _, out _));

    [Fact]
    public async Task Votar_duas_vezes_na_mesma_resposta_nao_duplica_e_mantem_o_ultimo_voto()
    {
        var nome = Guid.NewGuid().ToString();
        var id = Guid.NewGuid();
        var registrar = new RegistrarFeedback(new RepositorioDeFeedbacks(NovoBanco(nome)), TimeProvider.System);

        Assert.True(await registrar.ExecutarAsync(DadosDoBotao.Gerar(id, true), CancellationToken.None));
        Assert.True(await registrar.ExecutarAsync(DadosDoBotao.Gerar(id, false), CancellationToken.None));

        var gravados = await NovoBanco(nome).Feedbacks.ToListAsync();
        var unico = Assert.Single(gravados);
        Assert.Equal(id, unico.RespostaId);
        Assert.False(unico.Util);
    }

    [Fact]
    public void Feedback_guarda_somente_resposta_voto_e_data()
    {
        var propriedades = typeof(Feedback).GetProperties().Select(p => p.Name).Order();

        Assert.Equal(["Data", "RespostaId", "Util"], propriedades);
    }
}
