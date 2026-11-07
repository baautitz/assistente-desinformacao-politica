using AssistenteDesinformacao.Application.Pesquisa;
using Microsoft.Extensions.AI;

namespace AssistenteDesinformacao.Infrastructure.Ia;

public sealed class GeradorDeEmbeddingsComIa(IEmbeddingGenerator<string, Embedding<float>> gerador) : IGeradorDeEmbeddings
{
    public async Task<IReadOnlyList<float[]>> GerarAsync(IReadOnlyList<string> textos, CancellationToken ct)
    {
        var resultado = await gerador.GenerateAsync(textos, cancellationToken: ct);
        return resultado.Select(e => e.Vector.ToArray()).ToList();
    }
}
