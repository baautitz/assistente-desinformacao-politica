namespace AssistenteDesinformacao.Application.Mensageria;

public static class Eco
{
    public static string? Responder(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto;
}
