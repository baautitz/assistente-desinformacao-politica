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
        // Os buscadores usam recursos diferentes (API externa, banco), então rodam ao mesmo tempo.
        var resultados = await Task.WhenAll(buscadores.Select(b => BuscarSemFalharAsync(b, alegacao, ct)));

        var fontes = resultados.Where(r => r.Fontes is not null).SelectMany(r => r.Fontes!).ToList();
        var falhas = resultados.Where(r => r.Erro is not null).Select(r => r.Erro!).ToList();

        if (falhas.Count > 0 && falhas.Count == buscadores.Count)
            throw new AggregateException(falhas);
        return fontes;
    }

    private async Task<(IReadOnlyList<Fonte>? Fontes, Exception? Erro)> BuscarSemFalharAsync(
        IBuscadorDeFontes buscador, Alegacao alegacao, CancellationToken ct)
    {
        try
        {
            return (await buscador.BuscarAsync(alegacao, ct), null);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogWarning(erro, "Falha em {Buscador}", buscador.GetType().Name);
            return (null, erro);
        }
    }
}
