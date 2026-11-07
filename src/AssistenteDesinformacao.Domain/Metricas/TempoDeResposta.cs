namespace AssistenteDesinformacao.Domain.Metricas;

/// <summary>
/// Quanto tempo uma consulta levou para ser respondida. Guarda só a duração e a data:
/// nunca texto, imagem nem identificação do usuário.
/// </summary>
public sealed record TempoDeResposta(long Milissegundos, DateTimeOffset Data);
