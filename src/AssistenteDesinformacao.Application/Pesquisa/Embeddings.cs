using AssistenteDesinformacao.Domain.Coleta;

namespace AssistenteDesinformacao.Application.Pesquisa;

/// <summary>Gera vetores semânticos de textos. Opcional: sem modelo configurado, a busca usa só texto.</summary>
public interface IGeradorDeEmbeddings
{
    Task<IReadOnlyList<float[]>> GerarAsync(IReadOnlyList<string> textos, CancellationToken ct);
}

public interface IBuscaVetorialDeMaterias
{
    /// <summary>Matérias mais próximas do vetor, da mais para a menos parecida. Ignora as sem vetor.</summary>
    Task<IReadOnlyList<Materia>> BuscarAsync(float[] embedding, int limite, CancellationToken ct);
}

/// <summary>Reciprocal Rank Fusion: combina rankings de buscas diferentes sem precisar comparar suas pontuações.</summary>
public static class FusaoDeRankings
{
    private const int Constante = 60;

    public static IReadOnlyList<Materia> Combinar(IEnumerable<IReadOnlyList<Materia>> rankings, int limite)
    {
        var pontos = new Dictionary<Uri, (Materia Materia, double Pontos)>();
        foreach (var ranking in rankings)
        {
            for (var posicao = 0; posicao < ranking.Count; posicao++)
            {
                var materia = ranking[posicao];
                var ganho = 1.0 / (Constante + posicao + 1);
                pontos[materia.Link] = pontos.TryGetValue(materia.Link, out var atual)
                    ? (atual.Materia, atual.Pontos + ganho)
                    : (materia, ganho);
            }
        }

        return pontos.Values
            .OrderByDescending(p => p.Pontos)
            .ThenByDescending(p => p.Materia.DataPublicacao)
            .Take(limite)
            .Select(p => p.Materia)
            .ToList();
    }
}
