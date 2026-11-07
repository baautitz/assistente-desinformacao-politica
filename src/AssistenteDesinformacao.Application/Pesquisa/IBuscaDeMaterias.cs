using AssistenteDesinformacao.Domain.Coleta;

namespace AssistenteDesinformacao.Application.Pesquisa;

public interface IBuscaDeMaterias
{
    /// <summary>Busca textual nas matérias coletadas. Cada termo é uma busca; devolve a união, sem repetir links, só de matérias publicadas a partir de <paramref name="desde"/>.</summary>
    Task<IReadOnlyList<Materia>> BuscarAsync(IReadOnlyList<string> termos, int limite, DateTimeOffset desde, CancellationToken ct);
}
