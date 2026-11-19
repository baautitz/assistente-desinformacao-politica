using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Pesquisa;
using Microsoft.Extensions.Logging;

namespace AssistenteDesinformacao.Infrastructure.Pesquisa;

/// <summary>
/// Baixa a página de cada matéria escolhida e guarda a descrição e os primeiros parágrafos como <see cref="Fonte.Trecho"/>.
/// Só lê veículos da lista confiável (<see cref="VeiculosConfiaveis"/>), então o bot nunca baixa páginas de sites quaisquer.
/// </summary>
public sealed partial class LeitorDeMateria(HttpClient http, ILogger<LeitorDeMateria>? logger = null) : IEnriquecedorDeFontes
{
    public const int MaximoDeCaracteres = 1500;
    private const int MaximoDeBytes = 600_000;
    private static readonly TimeSpan LimiteDeTempo = TimeSpan.FromSeconds(7);

    public async Task<IReadOnlyList<Fonte>> EnriquecerAsync(IReadOnlyList<Fonte> fontes, CancellationToken ct)
    {
        var lidas = await Task.WhenAll(fontes.Select(f => LerSemFalharAsync(f, ct)));
        logger?.LogInformation("Texto lido de {Lidas} de {Total} fontes", lidas.Count(l => l.Lida), fontes.Count);
        return lidas.Select(l => l.Fonte).ToList();
    }

    private async Task<(Fonte Fonte, bool Lida)> LerSemFalharAsync(Fonte fonte, CancellationToken ct)
    {
        // Só veículos e agências da lista confiável são baixados.
        if (!VeiculosConfiaveis.Aceita(fonte.Link))
            return (fonte, false);

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(LimiteDeTempo);
        try
        {
            var html = await BaixarAsync(fonte.Link, limite.Token);
            var texto = ExtrairTexto(html);
            if (texto.Length < 80)
                return (fonte, false);
            // Na checagem, a afirmação checada continua no trecho: o texto da agência traz o porquê da classificação,
            // sem ele a IA só sabe que algo foi classificado e inventa o motivo.
            return fonte is Checagem && !string.IsNullOrWhiteSpace(fonte.Trecho)
                ? (fonte with { Trecho = $"Afirmação checada: {fonte.Trecho} Texto da checagem: {texto}" }, true)
                : (fonte with { Trecho = texto }, true);
        }
        catch (Exception erro) when (!ct.IsCancellationRequested)
        {
            logger?.LogInformation("Não consegui ler {Host}: {Erro}", fonte.Link.Host, erro.Message);
            return (fonte, false);
        }
    }

    private async Task<string> BaixarAsync(Uri link, CancellationToken ct)
    {
        using var resposta = await http.GetAsync(link, HttpCompletionOption.ResponseHeadersRead, ct);
        if (resposta.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException($"HTTP {(int)resposta.StatusCode}");

        await using var fluxo = await resposta.Content.ReadAsStreamAsync(ct);
        using var memoria = new MemoryStream();
        var bloco = new byte[16 * 1024];
        int lidos;
        while (memoria.Length < MaximoDeBytes && (lidos = await fluxo.ReadAsync(bloco, ct)) > 0)
            memoria.Write(bloco, 0, lidos);
        return Encoding.UTF8.GetString(memoria.GetBuffer(), 0, (int)memoria.Length);
    }

    /// <summary>Descrição da matéria (og:description) mais os primeiros parágrafos de texto, até <see cref="MaximoDeCaracteres"/>.</summary>
    public static string ExtrairTexto(string html)
    {
        var partes = new List<string>();
        var descricao = Meta(html, "og:description") ?? Meta(html, "description");
        if (!string.IsNullOrWhiteSpace(descricao))
            partes.Add(descricao.Trim());

        var corpo = BlocosIgnorados().Replace(html, " ");
        foreach (Match paragrafo in Paragrafos().Matches(corpo))
        {
            var texto = Espacos().Replace(WebUtility.HtmlDecode(Tags().Replace(paragrafo.Groups[1].Value, " ")), " ").Trim();
            if (texto.Length < 60 || partes.Any(p => p.StartsWith(texto[..Math.Min(40, texto.Length)], StringComparison.OrdinalIgnoreCase)))
                continue;
            partes.Add(texto);
            if (partes.Sum(p => p.Length) >= MaximoDeCaracteres || partes.Count >= 6)
                break;
        }

        var junto = string.Join(" ", partes);
        return junto.Length > MaximoDeCaracteres ? junto[..MaximoDeCaracteres].TrimEnd() + "…" : junto;
    }

    private static string? Meta(string html, string nome)
    {
        var antes = Regex.Match(html, $"""<meta[^>]+(?:property|name)=["']{Regex.Escape(nome)}["'][^>]*content=["']([^"']+)""", RegexOptions.IgnoreCase);
        var depois = Regex.Match(html, $"""<meta[^>]+content=["']([^"']+)["'][^>]+(?:property|name)=["']{Regex.Escape(nome)}["']""", RegexOptions.IgnoreCase);
        var achado = antes.Success ? antes : depois;
        return achado.Success ? WebUtility.HtmlDecode(achado.Groups[1].Value) : null;
    }

    [GeneratedRegex(@"<(script|style|nav|footer|header|aside|noscript)[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex BlocosIgnorados();

    [GeneratedRegex(@"<p[^>]*>([\s\S]*?)</p>", RegexOptions.IgnoreCase)]
    private static partial Regex Paragrafos();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacos();
}
