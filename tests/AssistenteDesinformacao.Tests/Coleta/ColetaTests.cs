using System.Text;
using AssistenteDesinformacao.Application.Coleta;
using AssistenteDesinformacao.Domain.Coleta;
using AssistenteDesinformacao.Infrastructure.Coleta;
using AssistenteDesinformacao.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Tests.Coleta;

public class ColetaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    private static Materia Nova(string slug, int diasAtras, string veiculo = "Veículo") =>
        new($"Título {slug}", "Resumo", new Uri($"https://exemplo.test/{slug}"), Agora.AddDays(-diasAtras), veiculo);

    [Fact]
    public async Task Descarta_materias_fora_da_janela_configurada()
    {
        var repo = new RepositorioFalso();
        var feed = new FeedConfigurado("A", new Uri("https://a.test/rss"));
        var leitor = new LeitorFalso(new() { [feed.Url] = [Nova("recente", 10), Nova("antiga", 100)] });
        var coleta = Criar([feed], leitor, repo, janelaEmDias: 90);

        await coleta.ExecutarAsync(CancellationToken.None);

        Assert.Equal(["recente"], repo.Gravadas.Select(m => m.Link.Segments[^1]));

        var estreita = Criar([feed], leitor, repo = new RepositorioFalso(), janelaEmDias: 5);
        await estreita.ExecutarAsync(CancellationToken.None);
        Assert.Empty(repo.Gravadas);
    }

    [Fact]
    public async Task Falha_em_um_feed_nao_impede_a_coleta_dos_demais()
    {
        var ruim = new FeedConfigurado("Ruim", new Uri("https://ruim.test/rss"));
        var bom = new FeedConfigurado("Bom", new Uri("https://bom.test/rss"));
        var leitor = new LeitorFalso(new() { [bom.Url] = [Nova("x", 1, "Bom")] }, falham: [ruim.Url]);
        var repo = new RepositorioFalso();

        var resultado = await Criar([ruim, bom], leitor, repo).ExecutarAsync(CancellationToken.None);

        Assert.Equal(1, resultado.Gravadas);
        Assert.Equal(["Ruim"], resultado.FeedsComFalha);
    }

    [Fact]
    public async Task Materia_repetida_nao_e_duplicada_no_banco()
    {
        var opcoes = new DbContextOptionsBuilder<BancoDeDados>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var banco = new BancoDeDados(opcoes);
        var repo = new RepositorioDeMaterias(banco);

        var primeira = await repo.AdicionarNovasAsync([Nova("a", 1), Nova("a", 1), Nova("b", 1)], CancellationToken.None);
        var segunda = await repo.AdicionarNovasAsync([Nova("a", 1), Nova("c", 1)], CancellationToken.None);

        Assert.Equal(2, primeira);
        Assert.Equal(1, segunda);
        Assert.Equal(3, await banco.Materias.CountAsync());
    }

    [Fact]
    public void Le_item_do_rss_com_titulo_resumo_link_data_e_veiculo()
    {
        const string xml = """
            <rss version="2.0"><channel><title>x</title>
              <item><title>Manchete</title><link>https://exemplo.test/m</link>
                <description>&lt;p&gt;Resumo &amp;amp; mais&lt;/p&gt;</description>
                <pubDate>Fri, 06 Nov 2026 10:00:00 GMT</pubDate></item>
              <item><title>Sem link</title><pubDate>Fri, 06 Nov 2026 10:00:00 GMT</pubDate></item>
            </channel></rss>
            """;

        var materias = LeitorDeFeedRss.Ler(new MemoryStream(Encoding.UTF8.GetBytes(xml)), "Jornal");

        var materia = Assert.Single(materias);
        Assert.Equal("Manchete", materia.Titulo);
        Assert.Equal("Resumo & mais", materia.Resumo);
        Assert.Equal(new Uri("https://exemplo.test/m"), materia.Link);
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero), materia.DataPublicacao);
        Assert.Equal("Jornal", materia.Veiculo);
    }

    [Fact]
    public void Le_a_lista_de_feeds_do_yaml()
    {
        var feeds = FontesDeFeedsEmYaml.Interpretar("""
            feeds:
              - veiculo: A
                url: https://a.test/rss
              - veiculo: B
                url: https://b.test/rss
            """);

        Assert.Equal(["A", "B"], feeds.Select(f => f.Veiculo));
        Assert.Equal(new Uri("https://b.test/rss"), feeds[1].Url);
    }

    [Fact]
    public void O_fontes_yaml_versionado_e_valido()
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "fontes.yaml");
        var feeds = new FontesDeFeedsEmYaml(caminho).Listar();
        Assert.NotEmpty(feeds);
    }

    private static ColetarFeeds Criar(FeedConfigurado[] feeds, LeitorFalso leitor, RepositorioFalso repo, int janelaEmDias = 90) =>
        new(new FontesFalsas(feeds), leitor, repo, new OpcoesDeColeta(janelaEmDias), new RelogioFalso());

    private sealed class FontesFalsas(FeedConfigurado[] feeds) : IFontesDeFeeds
    {
        public IReadOnlyList<FeedConfigurado> Listar() => feeds;
    }

    private sealed class LeitorFalso(Dictionary<Uri, Materia[]> porFeed, Uri[]? falham = null) : ILeitorDeFeed
    {
        public Task<IReadOnlyList<Materia>> LerAsync(FeedConfigurado feed, CancellationToken ct) =>
            falham?.Contains(feed.Url) == true
                ? throw new HttpRequestException("fora do ar")
                : Task.FromResult<IReadOnlyList<Materia>>(porFeed[feed.Url]);
    }

    private sealed class RepositorioFalso : IRepositorioDeMaterias
    {
        public List<Materia> Gravadas { get; } = [];

        public Task<IReadOnlyList<Materia>> FiltrarNovasAsync(IReadOnlyCollection<Materia> materias, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Materia>>(materias.ToList());

        public Task<int> AdicionarNovasAsync(IReadOnlyCollection<Materia> materias, CancellationToken ct)
        {
            Gravadas.AddRange(materias);
            return Task.FromResult(materias.Count);
        }
    }

    private sealed class RelogioFalso : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
