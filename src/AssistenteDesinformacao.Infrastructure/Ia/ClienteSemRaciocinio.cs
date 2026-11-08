using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace AssistenteDesinformacao.Infrastructure.Ia;

/// <summary>
/// Modelos que "pensam em voz alta" (ex.: MiniMax) abrem a resposta com um bloco think. Esse raciocínio nunca pode
/// chegar ao usuário nem ser lido como resposta, então é removido de toda resposta do cliente de IA.
/// </summary>
public sealed partial class ClienteSemRaciocinio(IChatClient interno) : DelegatingChatClient(interno)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> mensagens, ChatOptions? opcoes = null, CancellationToken ct = default)
    {
        var resposta = await base.GetResponseAsync(mensagens, opcoes, ct);
        foreach (var texto in resposta.Messages.SelectMany(m => m.Contents).OfType<TextContent>())
            texto.Text = Remover(texto.Text);
        return resposta;
    }

    /// <summary>Remove blocos think; um bloco aberto e nunca fechado (resposta cortada) leva o resto do texto junto.</summary>
    public static string Remover(string? texto) =>
        texto is null ? "" : Raciocinio().Replace(texto, "").Trim();

    [GeneratedRegex("<think>.*?(</think>|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Raciocinio();
}
