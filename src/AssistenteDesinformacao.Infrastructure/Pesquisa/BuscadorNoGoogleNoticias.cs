using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;
using Microsoft.Extensions.Logging;

namespace AssistenteDesinformacao.Infrastructure.Pesquisa;

/// <summary>
/// Busca matérias na hora pelo feed de busca do Google Notícias, sem depender do que o coletor já guardou.
/// Busca pelos termos que a IA gera (corrigem digitação e usam o nome que a imprensa dá ao assunto). Só se a IA não
/// responder usa as palavras-chave da própria Alegação, inteiras e depois só as primeiras, porque o Google exige todas as palavras.
/// </summary>
public sealed partial class BuscadorNoGoogleNoticias(
    HttpClient http, int janelaEmDias = 90, ILogger<BuscadorNoGoogleNoticias>? logger = null,
    IGeradorDeTermosDeBusca? gerador = null) : IBuscadorDeFontes, IEnriquecedorDeFontes
{
    /// <summary>Poucas chamadas ao Google ao mesmo tempo: dez de uma vez fazem alguns links falharem de forma intermitente.</summary>
    private readonly SemaphoreSlim _vagas = new(4);

    public const int Maximo = 12;

    public const int TermosDaIa = 3;

    /// <summary>Poupa as vagas dos 10 resultados; o filtro de veículos confiáveis em <see cref="Ler"/> é a garantia.</summary>
    private const string SemRedesSociais =
        " -site:instagram.com -site:facebook.com -site:x.com -site:twitter.com -site:tiktok.com -site:youtube.com -site:kwai.com -site:t.me";
    private const int MenorConsulta = 3;

    public async Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct)
    {
        var consultas = (await TermosDaIaAsync(alegacao, ct)).ToList();
        if (consultas.Count == 0)
        {
            var palavras = BuscadorNaFactCheck.PalavrasChave(alegacao.Texto).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (palavras.Length > 0)
                consultas.Add(string.Join(' ', palavras));
            if (palavras.Length > MenorConsulta)
                consultas.Add(string.Join(' ', palavras.Take(MenorConsulta)));
        }
        consultas = consultas.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (consultas.Count == 0)
            return [];

        var tarefas = consultas.Select(c => ConsultarAsync(c, ct)).ToList();
        try
        {
            await Task.WhenAll(tarefas);
        }
        catch when (tarefas.Any(t => t.IsCompletedSuccessfully))
        {
            // Uma consulta falhou, a outra respondeu: segue com o que veio.
        }

        // Intercala os resultados de cada consulta: sem isso, a primeira consulta ocupa todas as vagas e os termos da IA nunca entram.
        var listas = tarefas.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToList();
        var fontes = Enumerable.Range(0, listas.Count == 0 ? 0 : listas.Max(l => l.Count))
            .SelectMany(i => listas.Where(l => i < l.Count).Select(l => l[i]))
            .DistinctBy(f => f.Link)
            .Take(Maximo)
            .ToList();

        // O link direto só é resolvido depois, para as fontes escolhidas (EnriquecerAsync): resolver todas as candidatas faz o Google
        // responder 429 e aí nenhuma matéria é lida.
        return fontes;
    }

    /// <summary>Troca o redirecionamento do Google pelo link direto da matéria, só nas fontes escolhidas; se não der, mantém o do Google.</summary>
    public async Task<IReadOnlyList<Fonte>> EnriquecerAsync(IReadOnlyList<Fonte> fontes, CancellationToken ct)
    {
        var diretos = await Task.WhenAll(fontes.Select(f =>
            f.Link.Host == "news.google.com" ? ResolverSemFalharAsync(f.Link, ct) : Task.FromResult(f.Link)));
        return fontes.Select((f, i) => f with { Link = diretos[i] })
            .DistinctBy(f => f.Link)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> TermosDaIaAsync(Alegacao alegacao, CancellationToken ct)
    {
        if (gerador is null)
            return [];
        try
        {
            return (await gerador.GerarAsync(alegacao, ct)).Take(TermosDaIa).ToList();
        }
        catch (Exception erro) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning("Google Notícias: falha ao gerar termos com a IA; usando as palavras-chave: {Erro}", erro.Message);
            return [];
        }
    }

    private const string EnderecoDosArtigos = "https://news.google.com/rss/articles/";
    private const string EnderecoDoResolvedor = "https://news.google.com/_/DotsSplashUi/data/batchexecute";

    private async Task<Uri> ResolverSemFalharAsync(Uri link, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= 2; tentativa++)
        {
            await _vagas.WaitAsync(ct);
            try
            {
                using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
                limite.CancelAfter(TimeSpan.FromSeconds(8));
                if (await ResolverAsync(link, limite.Token) is { } direto)
                    return direto;
                logger?.LogWarning("Google Notícias: sem endereço direto na tentativa {Tentativa}", tentativa);
            }
            catch (Exception erro) when (!ct.IsCancellationRequested)
            {
                logger?.LogWarning("Google Notícias: falha ao resolver o link na tentativa {Tentativa}: {Erro}", tentativa, erro.Message);
            }
            finally
            {
                _vagas.Release();
            }

            if (tentativa == 1)
                await Task.Delay(400, ct);
        }

        return link; // sem endereço direto, o link do Google ainda leva à matéria
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
        await using var fluxo = await http.GetStreamAsync(EnderecoDaBusca(consulta, janelaEmDias), ct);
        return Ler(fluxo).Take(Maximo).ToList();
    }

    public static string EnderecoDaBusca(string consulta, int janelaEmDias) =>
        "https://news.google.com/rss/search"
        + $"?q={Uri.EscapeDataString($"{consulta} when:{janelaEmDias}d{SemRedesSociais}")}&hl=pt-BR&gl=BR&ceid=BR:pt-419";

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

        // O endereço do veículo vem no próprio feed, então dá para barrar antes de resolver o link direto.
        if (!Uri.TryCreate(item.Element("source")?.Attribute("url")?.Value, UriKind.Absolute, out var site)
            || !VeiculosConfiaveis.Aceita(site))
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
