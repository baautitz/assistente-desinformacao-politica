using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using AssistenteDesinformacao.Application.Coleta;
using AssistenteDesinformacao.Domain.Coleta;

namespace AssistenteDesinformacao.Infrastructure.Coleta;

public sealed partial class LeitorDeFeedRss(HttpClient http) : ILeitorDeFeed
{
    public async Task<IReadOnlyList<Materia>> LerAsync(FeedConfigurado feed, CancellationToken ct)
    {
        await using var fluxo = await http.GetStreamAsync(feed.Url, ct);
        return Ler(fluxo, feed.Veiculo);
    }

    public static IReadOnlyList<Materia> Ler(Stream xml, string veiculo)
    {
        using var leitor = XmlReader.Create(xml, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });

        return SyndicationFeed.Load(leitor).Items
            .Select(item => ParaMateria(item, veiculo))
            .OfType<Materia>()
            .ToList();
    }

    private static Materia? ParaMateria(SyndicationItem item, string veiculo)
    {
        var link = item.Links.FirstOrDefault(l => l.RelationshipType is null or "alternate")?.Uri;
        var titulo = item.Title?.Text?.Trim();
        var data = item.PublishDate != default ? item.PublishDate : item.LastUpdatedTime;

        if (link is not { IsAbsoluteUri: true } || string.IsNullOrEmpty(titulo) || data == default)
            return null;

        return new Materia(titulo, LimparHtml(item.Summary?.Text ?? ""), link, data.ToUniversalTime(), veiculo);
    }

    private static string LimparHtml(string html) =>
        System.Net.WebUtility.HtmlDecode(TagHtml().Replace(html, " ")).Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagHtml();
}
