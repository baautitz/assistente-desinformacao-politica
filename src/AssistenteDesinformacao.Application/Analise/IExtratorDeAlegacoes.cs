using AssistenteDesinformacao.Domain.Analise;

namespace AssistenteDesinformacao.Application.Analise;

public interface IExtratorDeAlegacoes
{
    Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct);
}
