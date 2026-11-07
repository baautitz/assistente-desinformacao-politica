using AssistenteDesinformacao.Application.Mensageria;

namespace AssistenteDesinformacao.Tests.Mensageria;

public sealed class ClassificadorDeNeutralidadeFalso(Func<string, bool> induz) : IClassificadorDeNeutralidade
{
    public static readonly ClassificadorDeNeutralidadeFalso Liberando = new(_ => false);

    public Task<bool> InduzVotoAsync(string texto, CancellationToken ct) => Task.FromResult(induz(texto));
}
