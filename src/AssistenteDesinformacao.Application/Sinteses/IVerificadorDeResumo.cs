using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Sinteses;

/// <summary>
/// Confere o resumo escrito pela IA contra as fontes antes de enviar: sem fato que as fontes não dizem, sem palavra inventada
/// ou estrangeira e sem veredito por conta própria. Deve falhar para o lado seguro: na dúvida, devolve <c>false</c>.
/// </summary>
public interface IVerificadorDeResumo
{
    Task<bool> EstaFundamentadoAsync(string resumo, IReadOnlyList<Fonte> fontes, CancellationToken ct);
}
