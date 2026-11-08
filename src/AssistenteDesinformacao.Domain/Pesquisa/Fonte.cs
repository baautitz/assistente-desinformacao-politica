namespace AssistenteDesinformacao.Domain.Pesquisa;

/// <summary>Matéria jornalística ou checagem publicada, identificada por link e data de publicação.</summary>
public record Fonte(string Titulo, Uri Link, DateOnly? DataPublicacao, string Veiculo)
{
    /// <summary>Trecho do que a fonte diz (resumo do RSS ou afirmação checada). Só apoia o resumo e a triagem; nunca é exibido.</summary>
    public string? Trecho { get; init; }
}
