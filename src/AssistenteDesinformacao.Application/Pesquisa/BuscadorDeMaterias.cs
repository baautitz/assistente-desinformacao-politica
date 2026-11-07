using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Coleta;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Pesquisa;

/// <summary>Busca nas matérias do RSS usando termos gerados a partir da Alegação.</summary>
public sealed class BuscadorDeMaterias(
    IGeradorDeTermosDeBusca gerador,
    IBuscaDeMaterias busca,
    IGeradorDeEmbeddings? embeddings = null,
    IBuscaVetorialDeMaterias? buscaVetorial = null) : IBuscadorDeFontes
{
    public const int MaximoDeMaterias = 10;

    public async Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct)
    {
        var termos = (await gerador.GerarAsync(alegacao, ct))
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        // Se a IA não devolver termos, a própria Alegação serve de busca.
        if (termos.Count == 0)
            termos.Add(alegacao.Texto);

        var porTexto = await busca.BuscarAsync(termos, MaximoDeMaterias, ct);
        var porSentido = await BuscarPorSentidoAsync(alegacao, ct);
        var materias = porSentido.Count == 0
            ? porTexto
            : FusaoDeRankings.Combinar([porTexto, porSentido], MaximoDeMaterias);
        return materias.Select(ParaFonte).ToList();
    }

    /// <summary>Sem modelo de embeddings, ou se ele falhar, devolve vazio e a busca segue só com texto.</summary>
    private async Task<IReadOnlyList<Materia>> BuscarPorSentidoAsync(Alegacao alegacao, CancellationToken ct)
    {
        if (embeddings is null || buscaVetorial is null)
            return [];
        try
        {
            var vetor = (await embeddings.GerarAsync([alegacao.Texto], ct)).FirstOrDefault();
            return vetor is null ? [] : await buscaVetorial.BuscarAsync(vetor, MaximoDeMaterias, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return [];
        }
    }

    private static Fonte ParaFonte(Materia m) =>
        new(m.Titulo, m.Link, DateOnly.FromDateTime(m.DataPublicacao.UtcDateTime), m.Veiculo);
}
