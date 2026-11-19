namespace AssistenteDesinformacao.Domain.Analise;

/// <summary>Afirmação de fato, extraída de uma consulta, que pode ser conferida em fontes jornalísticas.</summary>
/// <param name="SemFatoIsolado">
/// A mensagem é opinião ou previsão ("fulano vai transformar o país em X"): não há fato para conferir, então a busca usa o
/// texto inteiro e o resumo relata o que se sabe do assunto, sem tratar a mensagem como afirmação verdadeira ou falsa.
/// </param>
public sealed record Alegacao(string Texto, bool SemFatoIsolado = false);
