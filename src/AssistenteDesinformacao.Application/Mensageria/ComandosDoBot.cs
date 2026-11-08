namespace AssistenteDesinformacao.Application.Mensageria;

public static class ComandosDoBot
{
    /// <summary>Aceita /metricas, /metrics e a forma com o nome do bot (/metricas@meu_bot), que o Telegram usa em grupos.</summary>
    public static bool EhMetricas(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return false;
        var comando = texto.Trim().Split(' ', 2)[0].Split('@')[0];
        return comando.Equals("/metricas", StringComparison.OrdinalIgnoreCase)
            || comando.Equals("/metrics", StringComparison.OrdinalIgnoreCase);
    }
}
