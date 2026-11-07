namespace AssistenteDesinformacao.Domain.Mensageria;

/// <summary>Mensagem de texto ou print enviada pelo usuário ao bot. Nunca é persistida.</summary>
public sealed record Consulta(string Texto, Imagem? Imagem = null)
{
    public static Consulta DePrint(Imagem imagem) => new("", imagem);
}
