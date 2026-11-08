using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Pesquisa;

/// <summary>Confere se as fontes encontradas tratam mesmo da alegação e reescreve a alegação quando a busca não achou nada útil.</summary>
public interface IJuizDeRelevancia
{
    /// <summary>Devolve só as fontes que tratam do mesmo fato, pessoa ou assunto das alegações, na ordem recebida.</summary>
    Task<IReadOnlyList<Fonte>> FiltrarAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct);

    /// <summary>Mesma alegação com outras palavras e nomes próprios em destaque, para uma nova busca.</summary>
    Task<Alegacao> ReformularAsync(Alegacao alegacao, int tentativa, CancellationToken ct);
}
