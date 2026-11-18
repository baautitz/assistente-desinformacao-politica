using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Pesquisa;

/// <summary>
/// Lê o começo do texto de cada matéria escolhida para que o resumo e o verificador não dependam só do título.
/// Quando não consegue ler uma fonte (site bloqueia, demora, paywall), devolve essa fonte como veio.
/// </summary>
public interface IEnriquecedorDeFontes
{
    Task<IReadOnlyList<Fonte>> EnriquecerAsync(IReadOnlyList<Fonte> fontes, CancellationToken ct);
}
