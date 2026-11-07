using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Coleta;
using AssistenteDesinformacao.Domain.Mensageria;
using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Infrastructure.Persistencia;
using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Sinteses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistenteDesinformacao.Tests.Pesquisa;

[Collection("Postgres")]
public class BuscaTextualTests
{
    private static readonly Materia Reportagem = new(
        "Urnas passam por auditoria", "Tribunal explica a auditoria", new Uri("https://jornal.example/urnas"),
        new DateTimeOffset(2026, 10, 1, 23, 30, 0, TimeSpan.Zero), "Jornal");

    private static readonly Checagem Checagem1 = new(
        "É falso", new Uri("https://agencia.example/1"), new DateOnly(2026, 9, 1), "Agência", "Falso");

    [Fact]
    public async Task Busca_nas_materias_respeita_a_janela_em_dias()
    {
        var busca = new BuscaFalsa([Reportagem]);
        var agora = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var buscador = new BuscadorDeMaterias(
            new GeradorFalso(["urnas"]), busca, janelaEmDias: 90, relogio: new RelogioFixo(agora));

        await buscador.BuscarAsync(new Alegacao("As urnas não são auditadas"), default);

        Assert.Equal(agora.AddDays(-90), busca.DesdeRecebido);
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    [Fact]
    public async Task Materia_vira_fonte_com_link_e_data_de_publicacao()
    {
        var buscador = new BuscadorDeMaterias(new GeradorFalso(["urnas auditoria"]), new BuscaFalsa([Reportagem]));

        var fontes = await buscador.BuscarAsync(new Alegacao("As urnas não são auditadas"), default);

        var fonte = Assert.Single(fontes);
        Assert.Equal(Reportagem.Link, fonte.Link);
        Assert.Equal(new DateOnly(2026, 10, 1), fonte.DataPublicacao);
        Assert.Equal("Jornal", fonte.Veiculo);
        Assert.IsNotType<Checagem>(fonte);
    }

    [Fact]
    public async Task Sem_termos_da_ia_busca_pela_propria_alegacao()
    {
        var busca = new BuscaFalsa([]);
        var buscador = new BuscadorDeMaterias(new GeradorFalso([" ", ""]), busca);

        await buscador.BuscarAsync(new Alegacao("As urnas não são auditadas"), default);

        Assert.Equal(["As urnas não são auditadas"], busca.TermosRecebidos);
    }

    [Fact]
    public async Task Combinado_junta_fact_check_e_materias_e_sobrevive_a_falha_de_um_deles()
    {
        var materias = new BuscadorDeMaterias(new GeradorFalso(["urnas"]), new BuscaFalsa([Reportagem]));
        var checagens = new BuscadorFalso([Checagem1]);
        var logger = NullLogger<BuscadorCombinado>.Instance;

        var juntas = await new BuscadorCombinado([checagens, materias], logger).BuscarAsync(new Alegacao("a"), default);
        Assert.Equal(2, juntas.Count);

        var semBanco = new BuscadorCombinado([checagens, new BuscadorQueLanca()], logger);
        Assert.Equal([Checagem1], await semBanco.BuscarAsync(new Alegacao("a"), default));

        var tudoFalha = new BuscadorCombinado([new BuscadorQueLanca(), new BuscadorQueLanca()], logger);
        await Assert.ThrowsAsync<AggregateException>(() => tudoFalha.BuscarAsync(new Alegacao("a"), default));
    }

    [Fact]
    public async Task Responder_consulta_cita_checagem_e_materia_com_data_e_continua_atribuindo_classificacao_a_agencia()
    {
        var combinado = new BuscadorCombinado(
            [new BuscadorFalso([Checagem1]), new BuscadorDeMaterias(new GeradorFalso(["urnas"]), new BuscaFalsa([Reportagem]))],
            NullLogger<BuscadorCombinado>.Instance);
        var redator = new RedatorFalso("Segundo a Agência, é falso. Veja https://inventado.example/x");
        var caso = new ResponderConsulta(
            new ExtratorFalso(), new ImagemFalsa(), combinado, redator, NullLogger<ResponderConsulta>.Instance);

        var sintese = await caso.ExecutarAsync(new Consulta("As urnas não são auditadas"));

        Assert.Equal([Reportagem.Link, Checagem1.Link], sintese.Fontes.Select(f => f.Link));
        Assert.All(sintese.Fontes, f => Assert.NotNull(f.DataPublicacao));
        Assert.Equal(MensagensDoBot.ResumoPadrao, sintese.Resumo); // link inventado continua barrado
    }

    /// <summary>
    /// Roda só com TESTE_POSTGRES definido (ex.: "Host=localhost;Port=55437;Username=postgres;Password=x;Database=teste").
    /// Confere a busca com o dicionário de português (radical e stop words) e a migration do índice.
    /// </summary>
    [Fact]
    public async Task Busca_no_postgres_usa_dicionario_de_portugues()
    {
        if (Environment.GetEnvironmentVariable("TESTE_POSTGRES") is not { Length: > 0 } conexao)
            return;

        var opcoes = new DbContextOptionsBuilder<BancoDeDados>().UseNpgsql(conexao, n => n.UseVector()).Options;
        await using var banco = new BancoDeDados(opcoes);
        await banco.Database.EnsureDeletedAsync();
        await banco.Database.MigrateAsync();
        banco.Materias.AddRange(
            Reportagem,
            new Materia("Chuvas atingem o sul", "Defesa Civil alerta", new Uri("https://jornal.example/chuva"),
                Reportagem.DataPublicacao, "Jornal"));
        await banco.SaveChangesAsync();

        var achadas = await new BuscaDeMateriasNoPostgres(banco).BuscarAsync(["urnas auditorias"], 5, new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), default);

        Assert.Equal([Reportagem.Link], achadas.Select(m => m.Link));
        await banco.Database.EnsureDeletedAsync();
    }

    private sealed class GeradorFalso(string[] termos) : IGeradorDeTermosDeBusca
    {
        public Task<IReadOnlyList<string>> GerarAsync(Alegacao alegacao, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(termos);
    }

    private sealed class BuscaFalsa(IReadOnlyList<Materia> materias) : IBuscaDeMaterias
    {
        public IReadOnlyList<string>? TermosRecebidos { get; private set; }

        public DateTimeOffset? DesdeRecebido { get; private set; }

        public Task<IReadOnlyList<Materia>> BuscarAsync(IReadOnlyList<string> termos, int limite, DateTimeOffset desde, CancellationToken ct)
        {
            TermosRecebidos = termos;
            DesdeRecebido = desde;
            return Task.FromResult(materias);
        }
    }

    private sealed class BuscadorFalso(IReadOnlyList<Fonte> fontes) : IBuscadorDeFontes
    {
        public Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct) => Task.FromResult(fontes);
    }

    private sealed class BuscadorQueLanca : IBuscadorDeFontes
    {
        public Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct) =>
            throw new InvalidOperationException("falha");
    }

    private sealed class ExtratorFalso : IExtratorDeAlegacoes
    {
        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Alegacao>>([new Alegacao(texto)]);
    }

    private sealed class ImagemFalsa : IExtratorDeAlegacoesDeImagem
    {
        public bool Disponivel => false;

        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(Imagem imagem, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Alegacao>>([]);
    }

    private sealed class RedatorFalso(string resumo) : IRedatorDeSintese
    {
        public Task<string> RedigirAsync(IReadOnlyList<Alegacao> a, IReadOnlyList<Fonte> f, CancellationToken ct) =>
            Task.FromResult(resumo);
    }
}
