using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Sinteses;

/// <summary>
/// Confere o resumo escrito pela IA contra as fontes antes de enviar: sem fato que as fontes não dizem, sem palavra inventada
/// ou estrangeira e sem veredito por conta própria. Deve falhar para o lado seguro: na dúvida, devolve <c>false</c>.
/// </summary>
public interface IVerificadorDeResumo
{
    Task<bool> EstaFundamentadoAsync(string resumo, IReadOnlyList<Fonte> fontes, CancellationToken ct);

    /// <summary>Nulo quando o resumo está fundamentado; senão, o problema achado, que volta ao redator para ele corrigir.</summary>
    async Task<string?> ProblemaAsync(string resumo, IReadOnlyList<Fonte> fontes, CancellationToken ct) =>
        await EstaFundamentadoAsync(resumo, fontes, ct) ? null : "o resumo traz algo que as fontes não dizem";
}
