using System.Text.Json;

namespace AssistenteDesinformacao.WhatsAppBot;

/// <summary>Mensagem de um chat individual, já separada do JSON da Evolution API. Nunca é persistida.</summary>
/// <param name="Chat">JID do chat (ex.: 5511999999999@s.whatsapp.net): é para onde a resposta vai.</param>
/// <param name="Texto">Texto digitado, legenda da imagem ou emoji de uma reação; vazio se não houver.</param>
/// <param name="IdDoBotao">Só quando a pessoa tocou num botão de resposta: o id que o bot mandou no botão.</param>
/// <param name="ChaveEMensagem">Só nas imagens: o par key/message original, que a Evolution pede para baixar a mídia.</param>
public sealed record MensagemRecebida(string Id, string Chat, string Texto, bool EhReacao, JsonElement? ChaveEMensagem, string? IdDoBotao = null)
{
    public bool TemImagem => ChaveEMensagem is not null;
}

/// <summary>Lê o webhook <c>messages.upsert</c> da Evolution API (v2) e descarta tudo o que o bot não atende.</summary>
public static class LeitorDeWebhook
{
    private const string SufixoDeGrupo = "@g.us";

    public static MensagemRecebida? Ler(string json)
    {
        try
        {
            using var documento = JsonDocument.Parse(json);
            return Ler(documento.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static MensagemRecebida? Ler(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object
            || Texto(raiz, "event") is not { } evento
            || !evento.Replace('_', '.').Equals("messages.upsert", StringComparison.OrdinalIgnoreCase)
            || !raiz.TryGetProperty("data", out var dados))
            return null;

        // Em algumas versões o campo vem como lista de mensagens; o bot atende só a primeira.
        if (dados.ValueKind == JsonValueKind.Array)
            dados = dados.GetArrayLength() > 0 ? dados[0] : default;
        if (dados.ValueKind != JsonValueKind.Object
            || !dados.TryGetProperty("key", out var chave) || chave.ValueKind != JsonValueKind.Object)
            return null;

        var id = Texto(chave, "id");
        var chat = Texto(chave, "remoteJid");
        var minha = chave.TryGetProperty("fromMe", out var deMim) && deMim.ValueKind == JsonValueKind.True;

        // Só conversa individual, de outra pessoa: nada de grupo, status/broadcast nem eco das próprias respostas.
        if (minha || string.IsNullOrEmpty(id) || string.IsNullOrEmpty(chat)
            || chat.EndsWith(SufixoDeGrupo, StringComparison.Ordinal)
            || chat.Contains("broadcast", StringComparison.Ordinal)
            || !dados.TryGetProperty("message", out var mensagem) || mensagem.ValueKind != JsonValueKind.Object)
            return null;

        if (Texto(mensagem, "conversation") is { Length: > 0 } simples)
            return new MensagemRecebida(id, chat, simples, false, null);

        if (mensagem.TryGetProperty("extendedTextMessage", out var estendida)
            && Texto(estendida, "text") is { Length: > 0 } estendido)
            return new MensagemRecebida(id, chat, estendido, false, null);

        if (IdDoBotao(mensagem) is { Length: > 0 } botao)
            return new MensagemRecebida(id, chat, "", false, null, botao);

        if (mensagem.TryGetProperty("reactionMessage", out var reacao)
            && Texto(reacao, "text") is { Length: > 0 } emoji)
            return new MensagemRecebida(id, chat, emoji, true, null);

        if (mensagem.TryGetProperty("imageMessage", out var imagem) && imagem.ValueKind == JsonValueKind.Object)
        {
            // Serializa de novo para o elemento não depender do JsonDocument, que é descartado ao sair.
            var paraBaixar = JsonSerializer.SerializeToElement(new { key = chave, message = mensagem });
            return new MensagemRecebida(id, chat, Texto(imagem, "caption") ?? "", false, paraBaixar);
        }

        return null;
    }

    // O clique chega de três formas, conforme a versão do WhatsApp/Baileys: botão nativo (id dentro de um JSON),
    // botão simples antigo e botão de template.
    private static string? IdDoBotao(JsonElement mensagem)
    {
        if (mensagem.TryGetProperty("interactiveResponseMessage", out var interativa)
            && interativa.TryGetProperty("nativeFlowResponseMessage", out var fluxo)
            && Texto(fluxo, "paramsJson") is { Length: > 0 } parametros)
        {
            try
            {
                using var json = JsonDocument.Parse(parametros);
                if (json.RootElement.ValueKind == JsonValueKind.Object && Texto(json.RootElement, "id") is { } idNativo)
                    return idNativo;
            }
            catch (JsonException)
            {
            }
        }

        if (mensagem.TryGetProperty("buttonsResponseMessage", out var simples) && Texto(simples, "selectedButtonId") is { } idSimples)
            return idSimples;

        return mensagem.TryGetProperty("templateButtonReplyMessage", out var modelo) ? Texto(modelo, "selectedId") : null;
    }

    private static string? Texto(JsonElement objeto, string campo) =>
        objeto.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
