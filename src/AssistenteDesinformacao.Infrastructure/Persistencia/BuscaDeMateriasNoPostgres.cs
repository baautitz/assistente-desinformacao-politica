using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Coleta;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Infrastructure.Persistencia;

/// <summary>Busca de texto completo com o dicionário de português do Postgres (índice GIN criado na migration).</summary>
public sealed class BuscaDeMateriasNoPostgres(BancoDeDados banco) : IBuscaDeMaterias
{
    public const string Dicionario = "portuguese";

    public async Task<IReadOnlyList<Materia>> BuscarAsync(
        IReadOnlyList<string> termos, int limite, DateTimeOffset desde, CancellationToken ct)
    {
        var rankings = new List<IReadOnlyList<Materia>>();
        foreach (var termo in termos)
        {
            // websearch_to_tsquery aceita texto livre sem lançar erro de sintaxe.
            var resultado = await banco.Materias
                .Where(m => m.DataPublicacao >= desde && EF.Functions.ToTsVector(Dicionario, m.Titulo + " " + m.Resumo).Matches(EF.Functions.WebSearchToTsQuery(Dicionario, termo)))
                .OrderByDescending(m => EF.Functions.ToTsVector(Dicionario, m.Titulo + " " + m.Resumo).Rank(EF.Functions.WebSearchToTsQuery(Dicionario, termo)))
                .ThenByDescending(m => m.DataPublicacao)
                .Take(limite)
                .ToListAsync(ct);
            rankings.Add(resultado);
        }

        // Relevância primeiro: quem aparece bem em vários termos vence, em vez de ordenar por data.
        return FusaoDeRankings.Combinar(rankings, limite);
    }
}
