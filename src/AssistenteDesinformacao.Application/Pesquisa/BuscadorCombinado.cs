using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;
using Microsoft.Extensions.Logging;

namespace AssistenteDesinformacao.Application.Pesquisa;

/// <summary>
/// Junta os resultados de vários buscadores (Fact Check e matérias). Se um falhar, os outros ainda respondem;
/// só falha quando todos falham.
/// </summary>
public sealed class BuscadorCombinado(IReadOnlyList<IBuscadorDeFontes> buscadores, ILogger<BuscadorCombinado> logger)
    : IBuscadorDeFontes
{
    public async Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct)
    {
        var fontes = new List<Fonte>();
        var falhas = new List<Exception>();

        foreach (var buscador in buscadores)
        {
            try
            {
                fontes.AddRange(await buscador.BuscarAsync(alegacao, ct));
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                logger.LogWarning(erro, "Falha em {Buscador}", buscador.GetType().Name);
                falhas.Add(erro);
            }
        }

        if (falhas.Count > 0 && falhas.Count == buscadores.Count)
            throw new AggregateException(falhas);
        return fontes;
    }
}
