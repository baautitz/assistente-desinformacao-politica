using AssistenteDesinformacao.Application.Coleta;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AssistenteDesinformacao.Infrastructure.Coleta;

/// <summary>Lê a lista de feeds de um arquivo versionado (fontes.yaml).</summary>
public sealed class FontesDeFeedsEmYaml(string caminho) : IFontesDeFeeds
{
    public IReadOnlyList<FeedConfigurado> Listar() => Interpretar(File.ReadAllText(caminho));

    public static IReadOnlyList<FeedConfigurado> Interpretar(string yaml)
    {
        var deserializador = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var arquivo = deserializador.Deserialize<ArquivoDeFontes?>(yaml);

        return (arquivo?.Feeds ?? [])
            .Select(f => new FeedConfigurado(f.Veiculo, new Uri(f.Url, UriKind.Absolute)))
            .ToList();
    }

    private sealed class ArquivoDeFontes
    {
        public List<ItemDeFeed> Feeds { get; set; } = [];
    }

    private sealed class ItemDeFeed
    {
        public string Veiculo { get; set; } = "";
        public string Url { get; set; } = "";
    }
}
