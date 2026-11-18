using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Infrastructure.Pesquisa;

/// <summary>
/// Busca matérias na hora pelo feed de busca do Google Notícias, sem depender do que o coletor já guardou.
/// Tenta as palavras-chave inteiras e depois só as primeiras, porque o Google exige todas as palavras.
/// </summary>
public sealed partial class BuscadorNoGoogleNoticias(HttpClient http, int janelaEmDias = 90) : IBuscadorDeFontes
{
    public const int Maximo = 10;
    private const int MenorConsulta = 3;

    public async Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct)
    {
        var palavras = BuscadorNaFactCheck.PalavrasChave(alegacao.Texto).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palavras.Length == 0)
            return [];

        var consultas = new List<string> { string.Join(' ', palavras) };
        if (palavras.Length > MenorConsulta)
            consultas.Add(string.Join(' ', palavras.Take(MenorConsulta)));

        var tarefas = consultas.Select(c => ConsultarAsync(c, ct)).ToList();
        try
        {
            await Task.WhenAll(tarefas);
        }
        catch when (tarefas.Any(t => t.IsCompletedSuccessfully))
        {
            // Uma consulta falhou, a outra respondeu: segue com o que veio.
        }

        var fontes = tarefas.Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .DistinctBy(f => f.Link)
            .Take(Maximo)
            .ToList();

        // Troca o redirecionamento do Google pelo link direto da matéria; se não der, mantém o do Google.
        var diretos = await Task.WhenAll(fontes.Select(f => ResolverSemFalharAsync(f.Link, ct)));
        return fontes.Select((f, i) => f with { Link = diretos[i] })
            .DistinctBy(f => f.Link)
            .ToList();
    }

    private const string EnderecoDosArtigos = "https://news.google.com/rss/articles/";
    private const string EnderecoDoResolvedor = "https://news.google.com/_/DotsSplashUi/data/batchexecute";

    private async Task<Uri> ResolverSemFalharAsync(Uri link, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(6));
        try
        {
            return await ResolverAsync(link, limite.Token) ?? link;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return link;
        }
    }

    /// <summary>
    /// O link do feed é um redirecionamento do Google. O endereço real só sai pedindo ao próprio Google
    /// (chamada interna, sem garantia de estabilidade). Devolve null se não conseguir.
    /// </summary>
    private async Task<Uri?> ResolverAsync(Uri link, CancellationToken ct)
    {
        var id = link.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (link.Host != "news.google.com" || string.IsNullOrEmpty(id))
            return null;

        var pagina = await http.GetStringAsync(EnderecoDosArtigos + Uri.EscapeDataString(id), ct);
        var assinatura = Assinatura().Match(pagina);
        var instante = Instante().Match(pagina);
        if (!assinatura.Success || !instante.Success)
            return null;

        var interno = JsonSerializer.Serialize(new object?[]
        {
            "garturlreq",
            new object?[] { new object?[] { "X", "X", new[] { "X", "X" }, null, null, 1, 1, "US:en", null, 1, null, null, null, null, null, 0, 1 },
                "X", "X", 1, new[] { 1, 1, 1 }, 1, 1, null, 0, 0, null, 0 },
            id, long.Parse(instante.Groups[1].Value, CultureInfo.InvariantCulture), assinatura.Groups[1].Value,
        });
        var corpo = "f.req=" + Uri.EscapeDataString(JsonSerializer.Serialize(new[] { new[] { new object?[] { "Fbv4je", interno, null, "generic" } } }));

        using var pedido = new HttpRequestMessage(HttpMethod.Post, EnderecoDoResolvedor)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        using var resposta = await http.SendAsync(pedido, ct);
        resposta.EnsureSuccessStatusCode();
        return LerEnderecoDireto(await resposta.Content.ReadAsStringAsync(ct));
    }

    /// <summary>A resposta vem como ")]}'" seguido de JSON; o endereço está num JSON dentro de uma string.</summary>
    public static Uri? LerEnderecoDireto(string resposta)
    {
        var inicio = resposta.IndexOf('[');
        if (inicio < 0)
            return null;
        try
        {
            using var externo = JsonDocument.Parse(resposta[inicio..], new JsonDocumentOptions { AllowTrailingCommas = true });
            foreach (var linha in externo.RootElement.EnumerateArray())
            {
                if (linha.ValueKind != JsonValueKind.Array || linha.GetArrayLength() < 3
                    || linha[2].ValueKind != JsonValueKind.String)
                    continue;
                using var interno = JsonDocument.Parse(linha[2].GetString()!);
                if (interno.RootElement.ValueKind == JsonValueKind.Array && interno.RootElement.GetArrayLength() > 1
                    && interno.RootElement[1].ValueKind == JsonValueKind.String
                    && Uri.TryCreate(interno.RootElement[1].GetString(), UriKind.Absolute, out var direto)
                    && direto.Scheme is "http" or "https")
                    return direto;
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    [GeneratedRegex("data-n-a-sg=\"([^\"]+)\"")]
    private static partial Regex Assinatura();

    [GeneratedRegex("data-n-a-ts=\"([^\"]+)\"")]
    private static partial Regex Instante();

    private async Task<IReadOnlyList<Fonte>> ConsultarAsync(string consulta, CancellationToken ct)
    {
        var url = "https://news.google.com/rss/search"
            + $"?q={Uri.EscapeDataString($"{consulta} when:{janelaEmDias}d")}&hl=pt-BR&gl=BR&ceid=BR:pt-419";
        await using var fluxo = await http.GetStreamAsync(url, ct);
        return Ler(fluxo).Take(Maximo).ToList();
    }

    public static IReadOnlyList<Fonte> Ler(Stream xml)
    {
        var documento = XDocument.Load(
            XmlReader.Create(xml, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }));

        return documento.Descendants("item").Select(ParaFonte).OfType<Fonte>().ToList();
    }

    private static Fonte? ParaFonte(XElement item)
    {
        var titulo = item.Element("title")?.Value.Trim();
        var veiculo = item.Element("source")?.Value.Trim();
        if (!Uri.TryCreate(item.Element("link")?.Value, UriKind.Absolute, out var link)
            || string.IsNullOrEmpty(titulo) || string.IsNullOrEmpty(veiculo))
            return null;

        // O Google acrescenta " - Veículo" ao título.
        var sufixo = " - " + veiculo;
        if (titulo.EndsWith(sufixo, StringComparison.Ordinal))
            titulo = titulo[..^sufixo.Length];

        DateOnly? data = DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var quando)
            ? DateOnly.FromDateTime(quando.UtcDateTime)
            : null;

        return new Fonte(titulo, link, data, veiculo) { Trecho = titulo };
    }
}
