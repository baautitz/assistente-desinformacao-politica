using System.Net.Http.Json;
using System.Text.Json;
using AssistenteDesinformacao.Domain.Mensageria;

namespace AssistenteDesinformacao.WhatsAppBot;

/// <summary>Fala com a Evolution API (REST). A chave vai no cabeçalho <c>apikey</c>, configurado no HttpClient.</summary>
public sealed class ClienteEvolution(HttpClient http, string instancia)
{
    public async Task EnviarTextoAsync(string chat, string texto, CancellationToken ct)
    {
        using var resposta = await http.PostAsJsonAsync(
            $"message/sendText/{instancia}", new { number = chat, text = texto, linkPreview = false }, ct);
        resposta.EnsureSuccessStatusCode();
    }

    /// <summary>Manda uma mensagem com botões de resposta (no máximo 3). O clique volta pelo webhook com o <c>id</c> do botão.</summary>
    public async Task EnviarBotoesAsync(string chat, string texto, IReadOnlyList<(string Id, string Texto)> botoes, CancellationToken ct)
    {
        using var resposta = await http.PostAsJsonAsync(
            $"message/sendButtons/{instancia}",
            new
            {
                number = chat,
                title = texto,
                description = "Toque em um botão",
                footer = "",
                buttons = botoes.Select(b => new { type = "reply", displayText = b.Texto, id = b.Id }),
            }, ct);
        resposta.EnsureSuccessStatusCode();
    }

    /// <summary>Mostra "digitando..." por <paramref name="duracao"/>; a Evolution só devolve depois desse tempo.</summary>
    public async Task DigitandoAsync(string chat, TimeSpan duracao, CancellationToken ct)
    {
        using var resposta = await http.PostAsJsonAsync(
            $"chat/sendPresence/{instancia}",
            new { number = chat, presence = "composing", delay = (int)duracao.TotalMilliseconds }, ct);
        resposta.EnsureSuccessStatusCode();
    }

    /// <summary>Baixa a imagem da mensagem. Devolve null se a Evolution não entregar a mídia.</summary>
    public async Task<Imagem?> BaixarImagemAsync(JsonElement chaveEMensagem, CancellationToken ct)
    {
        using var resposta = await http.PostAsJsonAsync(
            $"chat/getBase64FromMediaMessage/{instancia}", new { message = chaveEMensagem }, ct);
        resposta.EnsureSuccessStatusCode();

        var dados = await resposta.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (!dados.TryGetProperty("base64", out var base64) || base64.GetString() is not { Length: > 0 } conteudo)
            return null;

        // Alguns retornos vêm como data URI ("data:image/jpeg;base64,...").
        var virgula = conteudo.IndexOf(',');
        if (conteudo.StartsWith("data:", StringComparison.Ordinal) && virgula >= 0)
            conteudo = conteudo[(virgula + 1)..];

        var tipo = dados.TryGetProperty("mimetype", out var mime) && mime.GetString() is { Length: > 0 } m ? m : "image/jpeg";
        return new Imagem(Convert.FromBase64String(conteudo), tipo);
    }
}
