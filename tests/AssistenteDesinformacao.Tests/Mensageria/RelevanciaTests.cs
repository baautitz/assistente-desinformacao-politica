using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Mensageria;
using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Infrastructure.Pesquisa;
using Microsoft.Extensions.Logging.Abstractions;

namespace AssistenteDesinformacao.Tests.Mensageria;

public class RelevanciaTests
{
    private static readonly Fonte Certa = new("Corte do Bolsa Família", new Uri("https://j.example/certa"), new DateOnly(2026, 11, 6), "Jornal");
    private static readonly Fonte Lixo = new("Alíquota de imposto", new Uri("https://j.example/lixo"), new DateOnly(2026, 11, 7), "Jornal");

    [Theory]
    [InlineData("1", new[] { 0 })]
    [InlineData("2, 1", new[] { 0, 1 })]
    [InlineData("As fontes 2.", new[] { 1 })]
    [InlineData("NENHUMA", new int[0])]
    [InlineData("", new int[0])]
    [InlineData("7", new int[0])]
    public void Juiz_le_os_numeros_das_fontes_relevantes(string resposta, int[] esperados)
    {
        var fontes = new[] { Certa, Lixo };

        var escolhidas = JuizDeRelevanciaComIa.Selecionar(resposta, fontes);

        Assert.Equal(esperados.Select(i => fontes[i]), escolhidas);
    }

    [Fact]
    public async Task So_as_fontes_relevantes_chegam_ao_resumo_e_a_resposta()
    {
        var redator = new RedatorFalso();
        var caso = Criar(new BuscaFixa([Lixo, Certa]), new JuizFalso(fontes => [.. fontes.Where(f => f == Certa)]), redator);

        var sintese = await caso.ExecutarAsync(new Consulta("texto"));

        Assert.Equal([Certa], sintese.Fontes);
        Assert.Equal([Certa], redator.Recebidas);
    }

    [Fact]
    public async Task Sem_fonte_relevante_reformula_e_busca_de_novo()
    {
        var busca = new BuscaPorTexto(alegacao => alegacao.Texto.Contains("reformulada") ? [Certa] : [Lixo]);
        var juiz = new JuizFalso(fontes => [.. fontes.Where(f => f == Certa)]);

        var sintese = await Criar(busca, juiz, new RedatorFalso()).ExecutarAsync(new Consulta("texto"));

        Assert.Equal([Certa], sintese.Fontes);
        Assert.Equal(["alegação", "alegação reformulada"], busca.Pesquisadas);
    }

    [Fact]
    public async Task Nunca_acha_fonte_relevante_diz_que_nao_encontrou_depois_de_algumas_tentativas()
    {
        var busca = new BuscaPorTexto(_ => [Lixo]);
        var redator = new RedatorFalso();

        var sintese = await Criar(busca, new JuizFalso(_ => []), redator).ExecutarAsync(new Consulta("texto"));

        Assert.Equal(MensagensDoBot.SemApuracao, sintese.Resumo);
        Assert.Empty(sintese.Fontes);
        Assert.Null(redator.Recebidas);
        Assert.Equal(ResponderConsulta.MaximoDeTentativasDeBusca, busca.Pesquisadas.Count);
    }

    private static ResponderConsulta Criar(IBuscadorDeFontes busca, IJuizDeRelevancia juiz, RedatorFalso redator) =>
        new(new ExtratorFalso(), new ExtratorDeImagemFalso(), busca, redator,
            ClassificadorDeNeutralidadeFalso.Liberando, NullLogger<ResponderConsulta>.Instance, juiz);

    private sealed class ExtratorFalso : IExtratorDeAlegacoes
    {
        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Alegacao>>([new Alegacao("alegação")]);
    }

    private sealed class ExtratorDeImagemFalso : IExtratorDeAlegacoesDeImagem
    {
        public bool Disponivel => false;
        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(Imagem imagem, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class BuscaFixa(IReadOnlyList<Fonte> fontes) : IBuscadorDeFontes
    {
        public Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct) => Task.FromResult(fontes);
    }

    private sealed class BuscaPorTexto(Func<Alegacao, IReadOnlyList<Fonte>> busca) : IBuscadorDeFontes
    {
        public List<string> Pesquisadas { get; } = [];

        public Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct)
        {
            Pesquisadas.Add(alegacao.Texto);
            return Task.FromResult(busca(alegacao));
        }
    }

    private sealed class JuizFalso(Func<IReadOnlyList<Fonte>, IReadOnlyList<Fonte>> filtro) : IJuizDeRelevancia
    {
        public Task<IReadOnlyList<Fonte>> FiltrarAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct) =>
            Task.FromResult(filtro(fontes));

        public Task<Alegacao> ReformularAsync(Alegacao alegacao, int tentativa, CancellationToken ct) =>
            Task.FromResult(new Alegacao(alegacao.Texto + " reformulada"));
    }

    private sealed class RedatorFalso : IRedatorDeSintese
    {
        public IReadOnlyList<Fonte>? Recebidas { get; private set; }

        public Task<string> RedigirAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct)
        {
            Recebidas = fontes;
            return Task.FromResult("Resumo neutro.");
        }
    }
}
