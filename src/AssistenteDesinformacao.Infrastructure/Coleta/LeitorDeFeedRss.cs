using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
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
        var documento = XDocument.Load(
            XmlReader.Create(xml, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreWhitespace = true }));

        // O leitor do .NET só aceita RSS 2.0, mas o 0.91 (usado pela Folha) tem os mesmos campos que lemos.
        if (documento.Root is { Name.LocalName: "rss" } raiz && raiz.Attribute("version")?.Value is "0.91" or "0.92")
            raiz.SetAttributeValue("version", "2.0");

        return SyndicationFeed.Load(documento.CreateReader()).Items
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
