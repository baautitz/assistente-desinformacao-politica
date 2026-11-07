using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Coleta;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace AssistenteDesinformacao.Infrastructure.Persistencia;

/// <summary>Busca por distância de cosseno com a extensão pgvector.</summary>
public sealed class BuscaVetorialNoPostgres(BancoDeDados banco) : IBuscaVetorialDeMaterias
{
    public async Task<IReadOnlyList<Materia>> BuscarAsync(float[] embedding, int limite, DateTimeOffset desde, CancellationToken ct)
    {
        var alvo = new Vector(embedding);
        var dimensoes = embedding.Length;
        // O filtro de dimensões evita erro se o modelo de embeddings mudou e há vetores antigos de outro tamanho.
        return await banco.Materias
            .FromSql($"""
                SELECT * FROM materias
                WHERE "Embedding" IS NOT NULL AND "DataPublicacao" >= {desde} AND vector_dims("Embedding") = {dimensoes}
                ORDER BY "Embedding" <=> {alvo}
                LIMIT {limite}
                """)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
