namespace AssistenteDesinformacao.Domain.Mensageria;

/// <summary>Print enviado pelo usuário. Fica só em memória, nunca é persistido.</summary>
public sealed record Imagem(byte[] Conteudo, string TipoDeMidia);
