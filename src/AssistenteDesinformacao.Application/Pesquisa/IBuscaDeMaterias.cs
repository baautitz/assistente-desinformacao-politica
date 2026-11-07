using AssistenteDesinformacao.Domain.Coleta;

namespace AssistenteDesinformacao.Application.Pesquisa;

public interface IBuscaDeMaterias
{
    /// <summary>Busca textual nas matérias coletadas. Cada termo é uma busca; devolve a união, sem repetir links.</summary>
    Task<IReadOnlyList<Materia>> BuscarAsync(IReadOnlyList<string> termos, int limite, CancellationToken ct);
}
