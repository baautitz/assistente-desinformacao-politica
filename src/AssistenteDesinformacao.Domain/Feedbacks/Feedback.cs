namespace AssistenteDesinformacao.Domain.Feedbacks;

/// <summary>
/// Voto sobre a utilidade de uma síntese (não sobre a veracidade da alegação).
/// Guarda só o id da resposta, o voto e a data: nunca texto nem identificação do usuário.
/// </summary>
public sealed record Feedback(Guid RespostaId, bool Util, DateTimeOffset Data);
