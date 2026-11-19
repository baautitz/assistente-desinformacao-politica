using System.Collections.Concurrent;
using AssistenteDesinformacao.Domain.Analise;

namespace AssistenteDesinformacao.Application.Pesquisa;

/// <summary>
/// Guarda os termos gerados por Alegação: os três buscadores (Fact Check, Google Notícias e matérias) pedem os termos da mesma
/// Alegação ao mesmo tempo, e sem isso seriam três chamadas à IA com a mesma pergunta. Falha não fica guardada.
/// </summary>
public sealed class GeradorDeTermosComCache(IGeradorDeTermosDeBusca gerador, int capacidade = 200) : IGeradorDeTermosDeBusca
{
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyList<string>>>> _termos = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<string>> GerarAsync(Alegacao alegacao, CancellationToken ct)
    {
        if (_termos.Count >= capacidade)
            _termos.Clear();

        var chave = alegacao.Texto.Trim();
        var pedido = _termos.GetOrAdd(chave, _ => new Lazy<Task<IReadOnlyList<string>>>(() => gerador.GerarAsync(alegacao, ct)));
        try
        {
            return await pedido.Value;
        }
        catch
        {
            _termos.TryRemove(new KeyValuePair<string, Lazy<Task<IReadOnlyList<string>>>>(chave, pedido));
            throw;
        }
    }
}
