using System.Globalization;
using System.Net.Http.Json;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Infrastructure.Pesquisa;

public sealed record OpcoesDaFactCheck(string Chave, string Idioma, int ResultadosPorBusca = 10);

/// <summary>Busca checagens na Google Fact Check Tools API (claims:search).</summary>
public sealed class BuscadorNaFactCheck(HttpClient http, OpcoesDaFactCheck opcoes) : IBuscadorDeFontes
{
    public static readonly Uri Endereco = new("https://factchecktools.googleapis.com/v1alpha1/");

    /// <summary>A chave vai no cabeçalho, não na URL, para não aparecer em logs nem em mensagens de erro.</summary>
    public const string CabecalhoDaChave = "X-Goog-Api-Key";

    public async Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct)
    {
        var url = "./claims:search" // "./" evita que "claims:" seja lido como esquema de URL
            + $"?query={Uri.EscapeDataString(alegacao.Texto)}"
            + $"&languageCode={Uri.EscapeDataString(opcoes.Idioma)}"
            + $"&pageSize={opcoes.ResultadosPorBusca}";

        var resposta = await http.GetFromJsonAsync<RespostaDaBusca>(url, ct);

        return (resposta?.Claims ?? [])
            .SelectMany(item => (item.ClaimReview ?? []).Select(revisao => ParaChecagem(item, revisao)))
            .OfType<Fonte>()
            .ToList();
    }

    /// <summary>Descarta revisões sem link válido ou sem classificação: sem isso não há o que mostrar.</summary>
    private static Checagem? ParaChecagem(AlegacaoDaApi item, RevisaoDaApi revisao)
    {
        if (!Uri.TryCreate(revisao.Url, UriKind.Absolute, out var link)
            || link.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(revisao.TextualRating))
            return null;

        var agencia = PrimeiroPreenchido(revisao.Publisher?.Name, revisao.Publisher?.Site) ?? link.Host;
        var titulo = PrimeiroPreenchido(revisao.Title, item.Text) ?? link.AbsoluteUri;
        DateOnly? data = DateTimeOffset.TryParse(revisao.ReviewDate, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var quando)
            ? DateOnly.FromDateTime(quando.UtcDateTime)
            : null;

        return new Checagem(titulo, link, data, agencia, revisao.TextualRating.Trim()) { Trecho = item.Text };
    }

    private static string? PrimeiroPreenchido(params string?[] valores) =>
        valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private sealed record RespostaDaBusca(List<AlegacaoDaApi>? Claims);

    private sealed record AlegacaoDaApi(string? Text, List<RevisaoDaApi>? ClaimReview);

    private sealed record RevisaoDaApi(
        EditoraDaApi? Publisher, string? Url, string? Title, string? ReviewDate, string? TextualRating);

    private sealed record EditoraDaApi(string? Name, string? Site);
}
