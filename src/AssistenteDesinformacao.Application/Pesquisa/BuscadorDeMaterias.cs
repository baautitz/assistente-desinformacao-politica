using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Coleta;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Pesquisa;

/// <summary>Busca nas matérias do RSS usando termos gerados a partir da Alegação.</summary>
public sealed class BuscadorDeMaterias(IGeradorDeTermosDeBusca gerador, IBuscaDeMaterias busca) : IBuscadorDeFontes
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

        var materias = await busca.BuscarAsync(termos, MaximoDeMaterias, ct);
        return materias.Select(ParaFonte).ToList();
    }

    private static Fonte ParaFonte(Materia m) =>
        new(m.Titulo, m.Link, DateOnly.FromDateTime(m.DataPublicacao.UtcDateTime), m.Veiculo);
}
