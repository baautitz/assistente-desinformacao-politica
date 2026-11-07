using AssistenteDesinformacao.Application.Metricas;
using AssistenteDesinformacao.Domain.Metricas;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Infrastructure.Persistencia;

public sealed class RepositorioDeMetricas(BancoDeDados banco) : IRepositorioDeMetricas
{
    public async Task RegistrarTempoAsync(TempoDeResposta tempo, CancellationToken ct)
    {
        banco.TemposDeResposta.Add(tempo);
        await banco.SaveChangesAsync(ct);
    }

    public async Task<ResumoDeMetricas> ResumirAsync(CancellationToken ct)
    {
        var uteis = await banco.Feedbacks.CountAsync(f => f.Util, ct);
        var inuteis = await banco.Feedbacks.CountAsync(f => !f.Util, ct);
        var consultas = await banco.TemposDeResposta.CountAsync(ct);
        double? media = consultas == 0 ? null : await banco.TemposDeResposta.AverageAsync(t => (double)t.Milissegundos, ct);
        return new ResumoDeMetricas(uteis, inuteis, consultas, media);
    }
}
