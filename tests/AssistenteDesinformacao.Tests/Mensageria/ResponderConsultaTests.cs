using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Mensageria;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Tests.Mensageria;

public class ResponderConsultaTests
{
    private static readonly Checagem ChecagemAntiga = new(
        "Vídeo antigo", new Uri("https://agencia-a.example/1"), new DateOnly(2022, 3, 1), "Agência A", "Falso");

    private static readonly Checagem ChecagemRecente = new(
        "Dado distorcido", new Uri("https://agencia-b.example/2"), new DateOnly(2024, 8, 10), "Agência B", "Enganoso");

    [Fact]
    public async Task Devolve_sintese_com_o_resumo_e_as_fontes_encontradas()
    {
        var redator = new RedatorFalso("  Segundo a Agência B, o dado é enganoso.  ");
        var caso = Criar(["alegação"], [ChecagemAntiga, ChecagemRecente], redator);

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal("Segundo a Agência B, o dado é enganoso.", sintese.Resumo);
        Assert.Equal([ChecagemRecente, ChecagemAntiga], sintese.Fontes);
        Assert.Equal(sintese.Fontes, redator.FontesRecebidas);
    }

    [Fact]
    public async Task Sem_fonte_responde_que_nao_encontrou_e_nao_chama_a_ia_para_redigir()
    {
        var redator = new RedatorFalso("inventado");
        var caso = Criar(["alegação"], [], redator);

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.False(sintese.EncontrouFontes);
        Assert.Equal(MensagensDoBot.SemApuracao, sintese.Resumo);
        Assert.Null(redator.FontesRecebidas);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Resumo_vazio_da_ia_vira_texto_padrao_mantendo_as_fontes(string resumoDaIa)
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso(resumoDaIa));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.ResumoPadrao, sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    [Fact]
    public async Task Texto_sem_alegacao_responde_que_nao_encontrou()
    {
        var caso = Criar([], [ChecagemRecente], new RedatorFalso("inventado"));

        var sintese = await caso.ExecutarAsync(new Consulta("bom dia!"));

        Assert.Equal(MensagensDoBot.SemApuracao, sintese.Resumo);
    }

    [Fact]
    public async Task Remove_fontes_repetidas_e_limita_a_quantidade()
    {
        var fontes = Enumerable.Range(1, 10)
            .Select(i => new Fonte($"Matéria {i}", new Uri($"https://veiculo.example/{i}"), new DateOnly(2024, 1, i), "Veículo"))
            .ToList();
        var caso = Criar(["alegação 1", "alegação 2"], fontes, new RedatorFalso("resumo"));

        var sintese = await caso.ExecutarAsync(new Consulta("texto"));

        Assert.Equal(ResponderConsulta.MaximoDeFontes, sintese.Fontes.Count);
        Assert.Equal(sintese.Fontes.Count, sintese.Fontes.DistinctBy(f => f.Link).Count());
    }

    private static ResponderConsulta Criar(string[] alegacoes, IReadOnlyList<Fonte> fontes, RedatorFalso redator) =>
        new(new ExtratorFalso(alegacoes), new BuscadorFalso(fontes), redator);

    private sealed class ExtratorFalso(string[] alegacoes) : IExtratorDeAlegacoes
    {
        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Alegacao>>(alegacoes.Select(a => new Alegacao(a)).ToList());
    }

    private sealed class BuscadorFalso(IReadOnlyList<Fonte> fontes) : IBuscadorDeFontes
    {
        public Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct) => Task.FromResult(fontes);
    }

    private sealed class RedatorFalso(string resumo) : IRedatorDeSintese
    {
        public IReadOnlyList<Fonte>? FontesRecebidas { get; private set; }

        public Task<string> RedigirAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct)
        {
            FontesRecebidas = fontes;
            return Task.FromResult(resumo);
        }
    }
}
