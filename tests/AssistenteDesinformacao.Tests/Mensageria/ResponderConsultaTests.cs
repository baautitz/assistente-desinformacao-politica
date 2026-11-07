using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Mensageria;
using AssistenteDesinformacao.Domain.Pesquisa;
using Microsoft.Extensions.Logging.Abstractions;

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

    [Theory]
    [InlineData("A mensagem é falso e circula desde 2022.")]
    [InlineData("Isso é verdade.")]
    [InlineData("Trata-se de fake news.")]
    [InlineData("O boato é uma mentira.")]
    public async Task Veredito_sem_atribuicao_vira_texto_padrao(string resumoDaIa)
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso(resumoDaIa));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.ResumoPadrao, sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    [Theory]
    [InlineData("Segundo a Agência B, o dado é falso.")]
    [InlineData("A Agência B diz que a mensagem é mentira.")]
    public async Task Veredito_atribuido_a_uma_checagem_e_mantido(string resumoDaIa)
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso(resumoDaIa));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(resumoDaIa, sintese.Resumo);
    }

    [Fact]
    public async Task Texto_sem_alegacao_responde_que_nao_encontrou()
    {
        var caso = Criar([], [ChecagemRecente], new RedatorFalso("inventado"));

        var sintese = await caso.ExecutarAsync(new Consulta("bom dia!"));

        Assert.Equal(MensagensDoBot.SemAlegacao, sintese.Resumo);
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

    [Theory]
    [InlineData("Em quem eu devo votar?")]
    [InlineData("Qual o melhor candidato a presidente?")]
    [InlineData("Vote no 13!")]
    public async Task Pedido_de_recomendacao_eleitoral_e_bloqueado_sem_chamar_a_ia(string texto)
    {
        var redator = new RedatorFalso("resumo");
        var caso = Criar(["alegação"], [ChecagemRecente], redator);

        var sintese = await caso.ExecutarAsync(new Consulta(texto));

        Assert.Equal(MensagensDoBot.RecomendacaoEleitoral, sintese.Resumo);
        Assert.Empty(sintese.Fontes);
        Assert.Null(redator.FontesRecebidas);
    }

    [Theory]
    [InlineData("Por isso, vote no candidato X.")]
    [InlineData("O melhor candidato é o Y.")]
    public async Task Resumo_com_recomendacao_eleitoral_vira_resposta_padrao(string resumoDaIa)
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso(resumoDaIa));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.ResumoPadrao, sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    [Fact]
    public async Task Print_valido_gera_sintese_como_o_texto()
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso("resumo"));

        var sintese = await caso.ExecutarAsync(Consulta.DePrint(new Imagem([1, 2, 3], "image/jpeg")));

        Assert.Equal("resumo", sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    private static readonly IExtratorDeAlegacoesDeImagem SemVisao = new ExtratorDeImagemFalso([], disponivel: false);

    [Fact]
    public async Task Print_com_pedido_eleitoral_e_bloqueado_sem_chamar_a_ia()
    {
        var redator = new RedatorFalso("resumo");
        var caso = Criar(["Em quem eu devo votar?"], [ChecagemRecente], redator);

        var sintese = await caso.ExecutarAsync(Consulta.DePrint(new Imagem([1], "image/jpeg")));

        Assert.Equal(MensagensDoBot.RecomendacaoEleitoral, sintese.Resumo);
        Assert.Null(redator.FontesRecebidas);
    }

    [Fact]
    public async Task Print_sem_alegacao_sem_fonte_ou_com_resumo_invalido_segue_os_guardrails()
    {
        var img = Consulta.DePrint(new Imagem([1], "image/jpeg"));

        Assert.Equal(MensagensDoBot.SemAlegacao, (await Criar([], [ChecagemRecente], new RedatorFalso("x")).ExecutarAsync(img)).Resumo);
        Assert.Equal(MensagensDoBot.SemApuracao, (await Criar(["a"], [], new RedatorFalso("x")).ExecutarAsync(img)).Resumo);
        Assert.Equal(MensagensDoBot.ResumoPadrao,
            (await Criar(["a"], [ChecagemRecente], new RedatorFalso("Veja https://inventado.example/x")).ExecutarAsync(img)).Resumo);
    }

    public async Task Resumo_com_link_inventado_vira_resposta_padrao()
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso("Veja https://inventado.example/x"));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.ResumoPadrao, sintese.Resumo);
    }

    [Fact]
    public async Task Fonte_sem_link_valido_e_descartada_e_sem_nenhuma_responde_que_nao_encontrou()
    {
        var semLink = new Fonte("Matéria", new Uri("/relativo", UriKind.Relative), new DateOnly(2024, 1, 1), "Veículo");
        var caso = Criar(["alegação"], [semLink], new RedatorFalso("resumo"));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.SemApuracao, sintese.Resumo);
        Assert.Empty(sintese.Fontes);
    }

    [Fact]
    public async Task Erro_externo_gera_mensagem_amigavel()
    {
        var caso = new ResponderConsulta(
            new ExtratorQueLanca(new HttpRequestException("falha externa")), SemVisao, new BuscadorFalso([]), new RedatorFalso("x"),
            NullLogger<ResponderConsulta>.Instance);

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.Erro, sintese.Resumo);
    }

    [Fact]
    public async Task Cancelamento_nao_e_engolido()
    {
        var caso = new ResponderConsulta(
            new ExtratorQueLanca(new OperationCanceledException()), SemVisao, new BuscadorFalso([]), new RedatorFalso("x"),
            NullLogger<ResponderConsulta>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => caso.ExecutarAsync(new Consulta("texto")));
    }

    private sealed class ExtratorQueLanca(Exception erro) : IExtratorDeAlegacoes
    {
        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct) => throw erro;
    }

    public async Task Imagem_sem_modelo_de_visao_avisa_que_so_le_texto()
    {
        var sintese = await CriarSemVisao().ExecutarAsync(Consulta.DePrint(new Imagem([1, 2, 3], "image/jpeg")));

        Assert.Equal(MensagensDoBot.SoLeTexto, sintese.Resumo);
        Assert.False(sintese.EncontrouFontes);
    }

    [Fact]
    public async Task Imagem_grande_demais_e_recusada()
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso("resumo"));
        var grande = new Imagem(new byte[ResponderConsulta.MaximoDeBytesDaImagem + 1], "image/png");

        var sintese = await caso.ExecutarAsync(Consulta.DePrint(grande));

        Assert.Equal(MensagensDoBot.ImagemGrande, sintese.Resumo);
        Assert.False(sintese.EncontrouFontes);
    }

    private static ResponderConsulta Criar(string[] alegacoes, IReadOnlyList<Fonte> fontes, RedatorFalso redator) =>
        new(new ExtratorFalso(alegacoes), new ExtratorDeImagemFalso(alegacoes, disponivel: true), new BuscadorFalso(fontes), redator,
            NullLogger<ResponderConsulta>.Instance);

    private static ResponderConsulta CriarSemVisao() =>
        new(new ExtratorFalso(["alegação"]), new ExtratorDeImagemFalso(["alegação"], disponivel: false),
            new BuscadorFalso([ChecagemRecente]), new RedatorFalso("resumo"), NullLogger<ResponderConsulta>.Instance);

    private sealed class ExtratorDeImagemFalso(string[] alegacoes, bool disponivel) : IExtratorDeAlegacoesDeImagem
    {
        public bool Disponivel => disponivel;

        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(Imagem imagem, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Alegacao>>(alegacoes.Select(a => new Alegacao(a)).ToList());
    }

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
