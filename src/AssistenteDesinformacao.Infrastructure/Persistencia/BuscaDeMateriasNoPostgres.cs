using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Coleta;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Infrastructure.Persistencia;

/// <summary>Busca de texto completo com o dicionário de português do Postgres (índice GIN criado na migration).</summary>
public sealed class BuscaDeMateriasNoPostgres(BancoDeDados banco) : IBuscaDeMaterias
{
    public const string Dicionario = "portuguese";

    public async Task<IReadOnlyList<Materia>> BuscarAsync(
        IReadOnlyList<string> termos, int limite, CancellationToken ct)
    {
        var achadas = new Dictionary<Uri, Materia>();
        foreach (var termo in termos)
        {
            // websearch_to_tsquery aceita texto livre sem lançar erro de sintaxe.
            var resultado = await banco.Materias
                .Where(m => EF.Functions.ToTsVector(Dicionario, m.Titulo + " " + m.Resumo).Matches(EF.Functions.WebSearchToTsQuery(Dicionario, termo)))
                .OrderByDescending(m => EF.Functions.ToTsVector(Dicionario, m.Titulo + " " + m.Resumo).Rank(EF.Functions.WebSearchToTsQuery(Dicionario, termo)))
                .ThenByDescending(m => m.DataPublicacao)
                .Take(limite)
                .ToListAsync(ct);
            foreach (var materia in resultado)
                achadas.TryAdd(materia.Link, materia);
        }

        return achadas.Values.OrderByDescending(m => m.DataPublicacao).Take(limite).ToList();
    }
}
