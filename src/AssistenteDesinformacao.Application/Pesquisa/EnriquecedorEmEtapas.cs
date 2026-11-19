using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Pesquisa;

/// <summary>Roda os enriquecedores em ordem (ex.: resolver o link do Google e depois ler a matéria). Se uma etapa falha, as fontes seguem como estavam.</summary>
public sealed class EnriquecedorEmEtapas(IReadOnlyList<IEnriquecedorDeFontes> etapas) : IEnriquecedorDeFontes
{
    public async Task<IReadOnlyList<Fonte>> EnriquecerAsync(IReadOnlyList<Fonte> fontes, CancellationToken ct)
    {
        foreach (var etapa in etapas)
        {
            try
            {
                fontes = await etapa.EnriquecerAsync(fontes, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
            }
        }
        return fontes;
    }
}
