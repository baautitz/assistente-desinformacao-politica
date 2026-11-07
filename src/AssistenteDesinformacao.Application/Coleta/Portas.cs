using AssistenteDesinformacao.Domain.Coleta;

namespace AssistenteDesinformacao.Application.Coleta;

public sealed record FeedConfigurado(string Veiculo, Uri Url);

public interface ILeitorDeFeed
{
    Task<IReadOnlyList<Materia>> LerAsync(FeedConfigurado feed, CancellationToken ct);
}

public interface IRepositorioDeMaterias
{
    /// <summary>Grava só as matérias cujo link ainda não existe. Devolve quantas foram gravadas.</summary>
    Task<int> AdicionarNovasAsync(IReadOnlyCollection<Materia> materias, CancellationToken ct);
}

public interface IFontesDeFeeds
{
    IReadOnlyList<FeedConfigurado> Listar();
}
