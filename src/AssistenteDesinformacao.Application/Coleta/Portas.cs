using AssistenteDesinformacao.Domain.Coleta;

namespace AssistenteDesinformacao.Application.Coleta;

public sealed record FeedConfigurado(string Veiculo, Uri Url);

public interface ILeitorDeFeed
{
    Task<IReadOnlyList<Materia>> LerAsync(FeedConfigurado feed, CancellationToken ct);
}

public interface IRepositorioDeMaterias
{
    /// <summary>Descarta as matérias cujo link já existe, sem gravar nada.</summary>
    Task<IReadOnlyList<Materia>> FiltrarNovasAsync(IReadOnlyCollection<Materia> materias, CancellationToken ct);

    /// <summary>Grava só as matérias cujo link ainda não existe. Devolve quantas foram gravadas.</summary>
    Task<int> AdicionarNovasAsync(IReadOnlyCollection<Materia> materias, CancellationToken ct);
}

public interface IFontesDeFeeds
{
    IReadOnlyList<FeedConfigurado> Listar();
}
