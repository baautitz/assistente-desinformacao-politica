using AssistenteDesinformacao.Domain.Analise;

namespace AssistenteDesinformacao.Application.Pesquisa;

public interface IGeradorDeTermosDeBusca
{
    Task<IReadOnlyList<string>> GerarAsync(Alegacao alegacao, CancellationToken ct);
}
