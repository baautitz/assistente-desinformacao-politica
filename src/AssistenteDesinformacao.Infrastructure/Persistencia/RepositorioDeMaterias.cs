using AssistenteDesinformacao.Application.Coleta;
using AssistenteDesinformacao.Domain.Coleta;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Infrastructure.Persistencia;

public sealed class RepositorioDeMaterias(BancoDeDados banco) : IRepositorioDeMaterias
{
    public async Task<int> AdicionarNovasAsync(IReadOnlyCollection<Materia> materias, CancellationToken ct)
    {
        var unicas = materias.DistinctBy(m => m.Link).ToList();
        var links = unicas.Select(m => m.Link).ToList();

        var existentes = (await banco.Materias
                .Where(m => links.Contains(m.Link))
                .Select(m => m.Link)
                .ToListAsync(ct))
            .ToHashSet();

        var novas = unicas.Where(m => !existentes.Contains(m.Link)).ToList();
        banco.Materias.AddRange(novas);
        await banco.SaveChangesAsync(ct);
        return novas.Count;
    }
}
