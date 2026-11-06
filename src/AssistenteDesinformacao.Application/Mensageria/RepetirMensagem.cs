namespace AssistenteDesinformacao.Application.Mensageria;

public static class RepetirMensagem
{
    public static string? Responder(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto;
}
