namespace AssistenteDesinformacao.Domain.Mensageria;

/// <summary>Mensagem de texto enviada pelo usuário ao bot. Nunca é persistida.</summary>
public sealed record Consulta(string Texto);
