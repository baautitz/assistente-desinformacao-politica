namespace AssistenteDesinformacao.Domain.Pesquisa;

/// <summary>
/// Fonte produzida por uma agência de verificação. A classificação é sempre atribuída à agência que a publicou.
/// </summary>
public sealed record Checagem(string Titulo, Uri Link, DateOnly? DataPublicacao, string Agencia, string Classificacao)
    : Fonte(Titulo, Link, DataPublicacao, Agencia);
