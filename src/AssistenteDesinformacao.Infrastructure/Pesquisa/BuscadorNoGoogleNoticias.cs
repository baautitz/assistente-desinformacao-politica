using System.Globalization;
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
public sealed class BuscadorNoGoogleNoticias(HttpClient http, int janelaEmDias = 90) : IBuscadorDeFontes
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

        return tarefas.Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .DistinctBy(f => f.Link)
            .Take(Maximo)
            .ToList();
    }

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
