namespace AssistenteDesinformacao.Application.Mensageria;

/// <summary>
/// Segunda camada do filtro eleitoral, atrás da regex: julga o sentido do texto, não só as palavras.
/// Deve falhar para o lado seguro: na dúvida, devolve <c>true</c>.
/// </summary>
public interface IClassificadorDeNeutralidade
{
    /// <summary>Verdadeiro se o texto pede ou dá recomendação de voto, candidato ou partido.</summary>
    Task<bool> InduzVotoAsync(string texto, CancellationToken ct);
}
