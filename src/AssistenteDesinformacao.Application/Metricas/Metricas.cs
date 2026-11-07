using AssistenteDesinformacao.Domain.Metricas;

namespace AssistenteDesinformacao.Application.Metricas;

/// <summary>Resumo para a equipe acompanhar qualidade e neutralidade. Só números agregados.</summary>
public sealed record ResumoDeMetricas(int VotosUteis, int VotosInuteis, int Consultas, double? TempoMedioEmMilissegundos);

public interface IRepositorioDeMetricas
{
    Task RegistrarTempoAsync(TempoDeResposta tempo, CancellationToken ct);

    Task<ResumoDeMetricas> ResumirAsync(CancellationToken ct);
}

public sealed class RegistrarTempoDeResposta(IRepositorioDeMetricas repositorio, TimeProvider relogio)
{
    public Task ExecutarAsync(TimeSpan duracao, CancellationToken ct) =>
        repositorio.RegistrarTempoAsync(new TempoDeResposta((long)duracao.TotalMilliseconds, relogio.GetUtcNow()), ct);
}

public sealed class ConsultarMetricas(IRepositorioDeMetricas repositorio)
{
    public Task<ResumoDeMetricas> ExecutarAsync(CancellationToken ct = default) => repositorio.ResumirAsync(ct);
}
