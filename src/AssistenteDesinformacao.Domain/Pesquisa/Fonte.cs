namespace AssistenteDesinformacao.Domain.Pesquisa;

/// <summary>Matéria jornalística ou checagem publicada, identificada por link e data de publicação.</summary>
public record Fonte(string Titulo, Uri Link, DateOnly? DataPublicacao, string Veiculo);
