using AssistenteDesinformacao.Application.Feedbacks;
using AssistenteDesinformacao.Application.Metricas;
using AssistenteDesinformacao.Domain.Metricas;
using AssistenteDesinformacao.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Tests.Metricas;

public class MetricasTests
{
    private static BancoDeDados NovoBanco(string nome) =>
        new(new DbContextOptionsBuilder<BancoDeDados>().UseInMemoryDatabase(nome).Options);

    [Fact]
    public async Task Resume_votos_e_tempo_medio()
    {
        var nome = Guid.NewGuid().ToString();
        var repositorio = new RepositorioDeMetricas(NovoBanco(nome));
        var tempo = new RegistrarTempoDeResposta(repositorio, TimeProvider.System);
        await tempo.ExecutarAsync(TimeSpan.FromMilliseconds(1000), CancellationToken.None);
        await tempo.ExecutarAsync(TimeSpan.FromMilliseconds(3000), CancellationToken.None);

        var votos = new RegistrarFeedback(new RepositorioDeFeedbacks(NovoBanco(nome)), TimeProvider.System);
        await votos.ExecutarAsync(DadosDoBotao.Gerar(Guid.NewGuid(), true), CancellationToken.None);
        await votos.ExecutarAsync(DadosDoBotao.Gerar(Guid.NewGuid(), true), CancellationToken.None);
        await votos.ExecutarAsync(DadosDoBotao.Gerar(Guid.NewGuid(), false), CancellationToken.None);

        var resumo = await new ConsultarMetricas(new RepositorioDeMetricas(NovoBanco(nome))).ExecutarAsync();

        Assert.Equal(new ResumoDeMetricas(2, 1, 2, 2000), resumo);
    }

    [Fact]
    public async Task Sem_consultas_nao_ha_tempo_medio()
    {
        var resumo = await new RepositorioDeMetricas(NovoBanco(Guid.NewGuid().ToString())).ResumirAsync(CancellationToken.None);

        Assert.Equal(new ResumoDeMetricas(0, 0, 0, null), resumo);
    }

    [Fact]
    public void Tempo_de_resposta_guarda_somente_duracao_e_data()
    {
        var propriedades = typeof(TempoDeResposta).GetProperties().Select(p => p.Name).Order();

        Assert.Equal(["Data", "Milissegundos"], propriedades);
    }
}
