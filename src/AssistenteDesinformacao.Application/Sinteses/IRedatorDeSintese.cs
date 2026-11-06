using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Sinteses;

/// <summary>Redige o resumo da síntese. Links e datas não passam por aqui: são montados a partir das fontes.</summary>
public interface IRedatorDeSintese
{
    Task<string> RedigirAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct);
}
