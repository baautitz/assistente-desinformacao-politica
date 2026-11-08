using System.Text.Json;
using System.Text.Json.Nodes;

namespace AssistenteDesinformacao.Infrastructure.Ia;

/// <summary>
/// Acrescenta campos extras ao JSON de toda requisição de chat. Serve para opções que só alguns provedores aceitam,
/// como desligar o raciocínio do MiniMax (<c>{"thinking":{"type":"disabled"}}</c>), sem mudar o código.
/// </summary>
public sealed class InjetorDeCamposNaRequisicao(JsonObject campos, HttpMessageHandler interno) : DelegatingHandler(interno)
{
    public static JsonObject? Ler(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException("IA_CAMPOS_EXTRAS deve ser um objeto JSON, ex.: {\"thinking\":{\"type\":\"disabled\"}}");
        }
        catch (JsonException erro)
        {
            throw new InvalidOperationException("IA_CAMPOS_EXTRAS não é um JSON válido.", erro);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
    {
        if (pedido.Content is { } conteudo && await conteudo.ReadAsStringAsync(ct) is { Length: > 0 } texto
            && JsonNode.Parse(texto) is JsonObject corpo)
        {
            foreach (var (nome, valor) in campos)
                corpo[nome] = valor?.DeepClone();
            pedido.Content = new StringContent(corpo.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        }

        return await base.SendAsync(pedido, ct);
    }
}
