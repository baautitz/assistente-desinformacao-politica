using AssistenteDesinformacao.Application.Coleta;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Coleta;
using AssistenteDesinformacao.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Tests.Pesquisa;

[Collection("Postgres")]
public class BuscaHibridaTests
{
    private static readonly DateTimeOffset Data = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static Materia Nova(string slug, float[]? embedding = null) =>
        new($"Titulo {slug}", $"Resumo {slug}", new Uri($"https://jornal.example/{slug}"), Data, "Jornal") { Embedding = embedding };

    [Fact]
    public void Fusao_prioriza_materia_que_aparece_nas_duas_buscas()
    {
        var a = Nova("a");
        var b = Nova("b");
        var c = Nova("c");

        var resultado = FusaoDeRankings.Combinar([[a, b], [c, b]], 10);

        Assert.Equal(b.Link, resultado[0].Link);
        Assert.Equal(3, resultado.Count);
        Assert.Single(FusaoDeRankings.Combinar([[a, b], [c, b]], 1));
    }

    [Fact]
    public async Task Combina_resultado_textual_e_vetorial_sem_repetir_links()
    {
        var texto = Nova("texto");
        var sentido = Nova("sentido");
        var buscador = new BuscadorDeMaterias(
            new GeradorDeTermosFalso(), new BuscaTextoFalsa([texto, sentido]),
            new EmbeddingsFalso(), new BuscaVetorialFalsa([sentido]));

        var fontes = await buscador.BuscarAsync(new Alegacao("a"), default);

        Assert.Equal([sentido.Link, texto.Link], fontes.Select(f => f.Link));
    }

    [Fact]
    public async Task Sem_modelo_de_embeddings_busca_so_texto()
    {
        var texto = Nova("texto");
        var vetorial = new BuscaVetorialFalsa([Nova("outra")]);
        var buscador = new BuscadorDeMaterias(new GeradorDeTermosFalso(), new BuscaTextoFalsa([texto]), null, vetorial);

        var fontes = await buscador.BuscarAsync(new Alegacao("a"), default);

        Assert.Equal([texto.Link], fontes.Select(f => f.Link));
        Assert.False(vetorial.Chamada);
    }

    [Fact]
    public async Task Falha_no_modelo_de_embeddings_nao_derruba_a_busca_textual()
    {
        var texto = Nova("texto");
        var buscador = new BuscadorDeMaterias(
            new GeradorDeTermosFalso(), new BuscaTextoFalsa([texto]),
            new EmbeddingsFalso(lanca: true), new BuscaVetorialFalsa([]));

        var fontes = await buscador.BuscarAsync(new Alegacao("a"), default);

        Assert.Equal([texto.Link], fontes.Select(f => f.Link));
    }

    [Fact]
    public async Task Coleta_gera_embedding_das_materias_novas_e_grava_sem_ele_se_o_modelo_falhar()
    {
        var feed = new FeedConfigurado("Jornal", new Uri("https://jornal.example/feed"));
        var materia = new Materia("T", "R", new Uri("https://jornal.example/x"), DateTimeOffset.UtcNow, "Jornal");

        var comModelo = new RepositorioQueGuarda();
        await Coletar(feed, materia, comModelo, new EmbeddingsFalso());
        Assert.Equal([1f, 0f], Assert.Single(comModelo.Gravadas).Embedding);

        var modeloFora = new RepositorioQueGuarda();
        await Coletar(feed, materia, modeloFora, new EmbeddingsFalso(lanca: true));
        Assert.Null(Assert.Single(modeloFora.Gravadas).Embedding);

        var semModelo = new RepositorioQueGuarda();
        await Coletar(feed, materia, semModelo, null);
        Assert.Null(Assert.Single(semModelo.Gravadas).Embedding);
    }

    private static Task Coletar(FeedConfigurado feed, Materia materia, RepositorioQueGuarda repo, IGeradorDeEmbeddings? embeddings) =>
        new ColetarFeeds(new FontesFalsas(feed), new LeitorFalso(materia), repo, new OpcoesDeColeta(), TimeProvider.System, embeddings)
            .ExecutarAsync(default);

    /// <summary>Roda só com TESTE_POSTGRES apontando para um Postgres com pgvector (imagem pgvector/pgvector).</summary>
    [Fact]
    public async Task Busca_vetorial_no_postgres_ordena_por_proximidade_e_ignora_materias_sem_vetor()
    {
        if (Environment.GetEnvironmentVariable("TESTE_POSTGRES") is not { Length: > 0 } conexao)
            return;

        var opcoes = new DbContextOptionsBuilder<BancoDeDados>().UseNpgsql(conexao, n => n.UseVector()).Options;
        await using var banco = new BancoDeDados(opcoes);
        await banco.Database.EnsureDeletedAsync();
        await banco.Database.MigrateAsync();
        var perto = Nova("perto", [1f, 0.1f, 0f]);
        var longe = Nova("longe", [0f, 1f, 0f]);
        banco.Materias.AddRange(longe, perto, Nova("sem-vetor"), Nova("outro-modelo", [1f, 0f]));
        await banco.SaveChangesAsync();

        var achadas = await new BuscaVetorialNoPostgres(banco).BuscarAsync([1f, 0f, 0f], 5, default);

        Assert.Equal([perto.Link, longe.Link], achadas.Select(m => m.Link));
        Assert.Equal(perto.Embedding, achadas[0].Embedding);
        await banco.Database.EnsureDeletedAsync();
    }

    private sealed class GeradorDeTermosFalso : IGeradorDeTermosDeBusca
    {
        public Task<IReadOnlyList<string>> GerarAsync(Alegacao alegacao, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(["termo"]);
    }

    private sealed class BuscaTextoFalsa(IReadOnlyList<Materia> materias) : IBuscaDeMaterias
    {
        public Task<IReadOnlyList<Materia>> BuscarAsync(IReadOnlyList<string> termos, int limite, CancellationToken ct) =>
            Task.FromResult(materias);
    }

    private sealed class BuscaVetorialFalsa(IReadOnlyList<Materia> materias) : IBuscaVetorialDeMaterias
    {
        public bool Chamada { get; private set; }

        public Task<IReadOnlyList<Materia>> BuscarAsync(float[] embedding, int limite, CancellationToken ct)
        {
            Chamada = true;
            return Task.FromResult(materias);
        }
    }

    private sealed class EmbeddingsFalso(bool lanca = false) : IGeradorDeEmbeddings
    {
        public Task<IReadOnlyList<float[]>> GerarAsync(IReadOnlyList<string> textos, CancellationToken ct) =>
            lanca
                ? throw new HttpRequestException("fora do ar")
                : Task.FromResult<IReadOnlyList<float[]>>(textos.Select(_ => new[] { 1f, 0f }).ToList());
    }

    private sealed class FontesFalsas(FeedConfigurado feed) : IFontesDeFeeds
    {
        public IReadOnlyList<FeedConfigurado> Listar() => [feed];
    }

    private sealed class LeitorFalso(Materia materia) : ILeitorDeFeed
    {
        public Task<IReadOnlyList<Materia>> LerAsync(FeedConfigurado feed, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Materia>>([materia]);
    }

    private sealed class RepositorioQueGuarda : IRepositorioDeMaterias
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
}
