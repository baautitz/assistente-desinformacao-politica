using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Sinteses;

/// <summary>Redige o resumo da síntese. Links e datas não passam por aqui: são montados a partir das fontes.</summary>
public interface IRedatorDeSintese
{
    Task<string> RedigirAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct);

    /// <summary>
    /// Nova tentativa depois de um resumo recusado: <paramref name="correcao"/> diz o que estava errado na versão anterior,
    /// para a IA corrigir em vez de tentar às cegas. Quem não sabe usar a correção só redige de novo.
    /// </summary>
    Task<string> RedigirAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, string? correcao, CancellationToken ct) =>
        RedigirAsync(alegacoes, fontes, ct);
}
