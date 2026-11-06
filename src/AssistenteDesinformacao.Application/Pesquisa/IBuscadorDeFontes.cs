using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Pesquisa;

public interface IBuscadorDeFontes
{
    Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct);
}
