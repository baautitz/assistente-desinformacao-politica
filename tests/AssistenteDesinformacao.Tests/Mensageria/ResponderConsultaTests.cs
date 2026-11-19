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

        Assert.Equal(MensagensDoBot.ResumoDasChecagens([ChecagemRecente]), sintese.Resumo);
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

        Assert.Equal(MensagensDoBot.ResumoDasChecagens([ChecagemRecente]), sintese.Resumo);
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
    public async Task Texto_sem_fato_isolado_ainda_busca_pelo_texto_inteiro()
    {
        var redator = new RedatorFalso("Isso é uma previsão; as fontes mostram o contexto.");
        var caso = Criar([], [ChecagemRecente], redator);

        var sintese = await caso.ExecutarAsync(new Consulta("fulano vai destruir o país"));

        Assert.Equal("Isso é uma previsão; as fontes mostram o contexto.", sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    [Theory]
    [InlineData("em quem devo votar para presidente?")]
    [InlineData("Em quem eu devo votar?")]
    [InlineData("qual o melhor candidato?")]
    [InlineData("quem é o melhor presidente")]
    public async Task Pedido_de_voto_recebe_resposta_fixa_sem_buscar_fontes(string texto)
    {
        var redator = new RedatorFalso("não deveria ser chamado");
        var caso = Criar(["alegação"], [ChecagemRecente], redator);

        var sintese = await caso.ExecutarAsync(new Consulta(texto));

        Assert.Equal(MensagensDoBot.NaoRecomendoVoto, sintese.Resumo);
        Assert.Empty(sintese.Fontes);
        Assert.Null(redator.FontesRecebidas);
    }

    [Fact]
    public async Task Sem_afirmacao_a_ia_julga_se_pede_voto_e_nao_busca_fontes()
    {
        var redator = new RedatorFalso("não deveria ser chamado");
        var caso = Criar([], [ChecagemRecente], redator, new ClassificadorDeNeutralidadeFalso(t => t.Contains("apoiaria")));

        var sintese = await caso.ExecutarAsync(new Consulta("quem você apoiaria nessa eleição?"));

        Assert.Equal(MensagensDoBot.NaoRecomendoVoto, sintese.Resumo);
        Assert.Empty(sintese.Fontes);
        Assert.Null(redator.FontesRecebidas);
    }

    [Fact]
    public async Task Com_afirmacao_o_texto_que_cita_voto_ainda_e_apurado()
    {
        var redator = new RedatorFalso("resumo neutro");
        var caso = Criar(["Candidato X teria dito algo"], [ChecagemRecente], redator, new ClassificadorDeNeutralidadeFalso(t => t.Contains("votar")));

        var sintese = await caso.ExecutarAsync(new Consulta("Candidato X mandou votar nele e disse algo, confere?"));

        Assert.Equal("resumo neutro", sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    [Theory]
    [InlineData("O candidato X disse que ninguém deve votar em quem apoia a reforma, e que o melhor candidato é ele. Isso circulou no grupo da família e muita gente repassou sem conferir nada do que estava escrito ali, inclusive números e datas.")]
    [InlineData("Lula gastou R$ 7 bilhões com viagens da primeira-dama")]
    public void Afirmacao_ou_texto_longo_nao_e_pedido_de_voto(string texto) =>
        Assert.False(GuardrailsDeNeutralidade.PedeRecomendacaoDeVoto(texto));

    [Fact]
    public async Task Texto_sem_fato_e_sem_fonte_responde_que_nao_encontrou_apuracao()
    {
        var sintese = await Criar([], [], new RedatorFalso("inventado")).ExecutarAsync(new Consulta("bom dia!"));

        Assert.Equal(MensagensDoBot.SemApuracao, sintese.Resumo);
    }

    [Fact]
    public async Task Mensagem_vazia_pede_o_texto()
    {
        var sintese = await Criar([], [ChecagemRecente], new RedatorFalso("x")).ExecutarAsync(new Consulta("   "));

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
    public async Task Print_sem_alegacao_sem_fonte_ou_com_resumo_invalido_segue_os_guardrails()
    {
        var img = Consulta.DePrint(new Imagem([1], "image/jpeg"));

        Assert.Equal(MensagensDoBot.SemAlegacao, (await Criar([], [ChecagemRecente], new RedatorFalso("x")).ExecutarAsync(img)).Resumo);
        Assert.Equal(MensagensDoBot.SemApuracao, (await Criar(["a"], [], new RedatorFalso("x")).ExecutarAsync(img)).Resumo);
        Assert.Equal(MensagensDoBot.ResumoDasChecagens([ChecagemRecente]),
            (await Criar(["a"], [ChecagemRecente], new RedatorFalso("Veja https://inventado.example/x")).ExecutarAsync(img)).Resumo);
    }

    [Fact]
    public async Task Resumo_com_link_inventado_vira_resposta_padrao()
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso("Veja https://inventado.example/x"));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.ResumoDasChecagens([ChecagemRecente]), sintese.Resumo);
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
            ClassificadorDeNeutralidadeFalso.Liberando, NullLogger<ResponderConsulta>.Instance);

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.Erro, sintese.Resumo);
    }

    [Fact]
    public async Task Cancelamento_nao_e_engolido()
    {
        var caso = new ResponderConsulta(
            new ExtratorQueLanca(new OperationCanceledException()), SemVisao, new BuscadorFalso([]), new RedatorFalso("x"),
            ClassificadorDeNeutralidadeFalso.Liberando, NullLogger<ResponderConsulta>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => caso.ExecutarAsync(new Consulta("texto")));
    }

    private sealed class ExtratorQueLanca(Exception erro) : IExtratorDeAlegacoes
    {
        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct) => throw erro;
    }

    [Fact]
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

    [Theory]
    [InlineData("Em quem devo votar?")]
    [InlineData("Qual o melhor candidato?")]
    [InlineData("vote 22")]
    [InlineData("Vote no 13!")]
    [InlineData("Não vote nesse partido")]
    public void Regex_pega_so_os_casos_obvios(string texto) =>
        Assert.True(GuardrailsDeNeutralidade.TemRecomendacaoEleitoral(texto));

    [Theory]
    [InlineData("A urna eletrônica não foi auditada em 2022")]
    [InlineData("O candidato X disse que vai cortar impostos")]
    public void Regex_nao_bloqueia_afirmacao_de_fato(string texto) =>
        Assert.False(GuardrailsDeNeutralidade.TemRecomendacaoEleitoral(texto));

    [Theory]
    [InlineData("Bolsa Família será cortado em 2 anos, diz coordenadora de candidato")]
    public async Task Texto_do_usuario_nao_e_bloqueado_so_o_que_o_bot_escreve(string texto)
    {
        var redator = new RedatorFalso("resumo neutro");
        var caso = Criar(["alegação"], [ChecagemRecente], redator, new ClassificadorDeNeutralidadeFalso(t => t.Contains("votar") || t.Contains("candidato")));

        var sintese = await caso.ExecutarAsync(new Consulta(texto));

        Assert.Equal("resumo neutro", sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    [Theory]
    [InlineData("As fontes relatam isso, Having completion context: According to the agencies, nada.", true)]
    [InlineData("A agência classificou como falso e explicou o motivo.", false)]
    [InlineData("A agência disse 这是假的 sobre o caso.", true)]
    [InlineData("A CNN registrou a proposta. Need final proper. Wait I accidentally inserted weird.", true)]
    [InlineData("O G1 publish que Augusto Cury propõe um ministério da IA.", true)]
    [InlineData("Cury propõe treinar embaixadores para serem influencers e usar drones.", false)]
    [InlineData("O Metrópoles relata que Cury defendeu drones contra feminicídio no JN.", false)]
    public void Detecta_texto_fora_do_portugues(string texto, bool fora) =>
        Assert.Equal(fora, GuardrailsDeNeutralidade.TemTextoForaDoPortugues(texto));

    [Theory]
    [InlineData("Portanto, não há confirmação de que o governo esteja taxando o Pix.", true)]
    [InlineData("Nada indica que a medida já esteja valendo.", true)]
    [InlineData("A decisão não foi confirmada pelo ministério.", true)]
    [InlineData("Segundo o ministério, não há registro de cobrança sobre o Pix.", false)]
    [InlineData("O Jornal B informa que não há previsão de nova votação.", false)]
    [InlineData("O decreto deve ser assinado na quinta, segundo o ministro.", false)]
    public void Detecta_conclusao_tirada_da_falta_de_noticia(string resumo, bool conclui) =>
        Assert.Equal(conclui, GuardrailsDeNeutralidade.TemConclusaoPorAusencia(resumo,
            [new Fonte("Matéria", new Uri("https://jornal.example/m"), null, "Jornal B")]));

    [Fact]
    public async Task Resumo_com_idioma_misturado_e_refeito_uma_vez()
    {
        var redator = new RedatorSequencial("Segundo a agência, According to the agencies, lixo.", "Segundo a agência, é falso.");
        var caso = Criar(["alegação"], [ChecagemRecente], redator);

        var sintese = await caso.ExecutarAsync(new Consulta("texto"));

        Assert.Equal("Segundo a agência, é falso.", sintese.Resumo);
        Assert.Equal(2, redator.Chamadas);
    }

    [Fact]
    public async Task Resumo_com_defeito_e_refeito_ate_sair_limpo()
    {
        var redator = new RedatorSequencial("According to the agencies, lixo.", "Need final proper, lixo.", "Wait, lixo.", "Segundo a agência, é falso.");

        var sintese = await Criar(["alegação"], [ChecagemRecente], redator).ExecutarAsync(new Consulta("texto"));

        Assert.Equal("Segundo a agência, é falso.", sintese.Resumo);
        Assert.Equal(ResponderConsulta.MaximoDeTentativasDoResumo, redator.Chamadas);
    }

    [Theory]
    [InlineData(TipoDeMensagem.PedidoDeVoto, "Não recomendo candidato, partido ou voto")]
    [InlineData(TipoDeMensagem.PedidoDeOpiniao, "Não dou opinião nem faço ranking")]
    [InlineData(TipoDeMensagem.ForaDoEscopo, "Eu confiro afirmações e notícias sobre política")]
    public async Task Juiza_que_recusa_a_mensagem_responde_sem_extrair_nem_buscar(TipoDeMensagem tipo, string inicioDaResposta)
    {
        var redator = new RedatorFalso("não deveria ser chamado");
        var juiza = new JuizDeMensagemFalso(tipo);

        var sintese = await Criar(["alegação"], [ChecagemRecente], redator, juizDeMensagem: juiza)
            .ExecutarAsync(new Consulta("qual o partido mais honesto do país?"));

        Assert.StartsWith(inicioDaResposta, sintese.Resumo);
        Assert.Empty(sintese.Fontes);
        Assert.Null(redator.FontesRecebidas);
        Assert.Equal(1, juiza.Chamadas);
    }

    [Theory]
    [InlineData(TipoDeMensagem.Afirmacao)]
    [InlineData(TipoDeMensagem.Opiniao)]
    public async Task Juiza_que_libera_a_mensagem_segue_para_a_apuracao(TipoDeMensagem tipo)
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso("resumo neutro"), juizDeMensagem: new JuizDeMensagemFalso(tipo));

        var sintese = await caso.ExecutarAsync(new Consulta("Lula gastou R$ 7 bilhões com viagens"));

        Assert.Equal("resumo neutro", sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    [Fact]
    public async Task Redator_recebe_o_texto_lido_das_materias_e_a_sintese_mantem_os_links()
    {
        var redator = new RedatorFalso("resumo neutro");

        var sintese = await Criar(["alegação"], [ChecagemRecente], redator, enriquecedor: new EnriquecedorFalso())
            .ExecutarAsync(new Consulta("texto"));

        Assert.Equal("TEXTO DA MATÉRIA", Assert.Single(redator.FontesRecebidas!).Trecho);
        Assert.Equal(ChecagemRecente.Link, Assert.Single(sintese.Fontes).Link);
        Assert.Equal("resumo neutro", sintese.Resumo);
    }

    [Fact]
    public async Task Falha_ao_ler_as_materias_segue_so_com_os_titulos()
    {
        var redator = new RedatorFalso("resumo neutro");

        var sintese = await Criar(["alegação"], [ChecagemRecente], redator, enriquecedor: new EnriquecedorQueFalha())
            .ExecutarAsync(new Consulta("texto"));

        Assert.Equal("resumo neutro", sintese.Resumo);
        Assert.Equal(ChecagemRecente.Trecho, Assert.Single(redator.FontesRecebidas!).Trecho);
    }

    [Fact]
    public async Task Juiza_que_falha_nao_derruba_a_resposta()
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso("resumo neutro"), juizDeMensagem: new JuizDeMensagemQueFalha());

        var sintese = await caso.ExecutarAsync(new Consulta("texto"));

        Assert.Equal("resumo neutro", sintese.Resumo);
    }

    [Fact]
    public async Task Imagem_nao_passa_pela_juiza_de_mensagem_de_texto()
    {
        var juiza = new JuizDeMensagemFalso(TipoDeMensagem.PedidoDeOpiniao);
        var img = Consulta.DePrint(new Imagem([1, 2, 3], "image/png"));

        var sintese = await Criar(["alegação"], [ChecagemRecente], new RedatorFalso("resumo"), juizDeMensagem: juiza).ExecutarAsync(img);

        Assert.Equal("resumo", sintese.Resumo);
        Assert.Equal(0, juiza.Chamadas);
    }

    [Fact]
    public async Task Resumo_que_o_verificador_reprova_e_refeito_ate_ser_aprovado()
    {
        var redator = new RedatorSequencial("O G1 relata que Cury lidera as pesquisas com 35%.", "O G1 relata que Cury propõe drones.");
        var verificador = new VerificadorFalso(r => !r.Contains("35%"));

        var sintese = await Criar(["alegação"], [ChecagemRecente], redator, verificador: verificador).ExecutarAsync(new Consulta("texto"));

        Assert.Equal("O G1 relata que Cury propõe drones.", sintese.Resumo);
        Assert.Equal(2, verificador.Chamadas);
    }

    [Fact]
    public async Task Resumo_que_o_verificador_sempre_reprova_vira_so_a_lista_de_fontes()
    {
        var redator = new RedatorSequencial("Resumo com fato inventado.");
        var verificador = new VerificadorFalso(_ => false);

        var sintese = await Criar(["alegação"], [ChecagemRecente], redator, verificador: verificador).ExecutarAsync(new Consulta("texto"));

        Assert.Equal(MensagensDoBot.ResumoDasChecagens([ChecagemRecente]), sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
        Assert.Equal(ResponderConsulta.MaximoDeTentativasDoResumo, redator.Chamadas);
    }

    [Fact]
    public async Task Verificador_que_falha_manda_so_a_lista_de_fontes_sem_repetir()
    {
        var redator = new RedatorSequencial("Resumo bom.");
        var verificador = new VerificadorQueFalha();

        var sintese = await Criar(["alegação"], [ChecagemRecente], redator, verificador: verificador).ExecutarAsync(new Consulta("texto"));

        Assert.Equal(MensagensDoBot.ResumoDasChecagens([ChecagemRecente]), sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
        Assert.Equal(1, verificador.Chamadas);
    }

    [Fact]
    public async Task Resumo_que_continua_misturado_vira_so_a_lista_de_fontes()
    {
        var redator = new RedatorSequencial("According to the agencies, lixo.", "Having completion context, lixo.");

        var sintese = await Criar(["alegação"], [ChecagemRecente], redator).ExecutarAsync(new Consulta("texto"));

        Assert.Equal(MensagensDoBot.ResumoDasChecagens([ChecagemRecente]), sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    [Fact]
    public async Task Resumo_da_ia_com_recomendacao_obvia_vira_resposta_padrao()
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso("Fica a dica: vote no 13."));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.ResumoPadrao, sintese.Resumo);
    }

    [Fact]
    public async Task Resumo_da_ia_com_parafrase_eleitoral_vira_resposta_padrao()
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso("Fica claro que o melhor caminho é ficar com o fulano."),
            new ClassificadorDeNeutralidadeFalso(t => t.Contains("fulano")));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.ResumoPadrao, sintese.Resumo);
        Assert.Equal([ChecagemRecente], sintese.Fontes);
    }

    [Fact]
    public async Task Falha_do_classificador_gera_mensagem_amigavel_e_nunca_libera()
    {
        var caso = Criar(["alegação"], [ChecagemRecente], new RedatorFalso("resumo"),
            new ClassificadorDeNeutralidadeFalso(_ => throw new HttpRequestException("fora do ar")));

        var sintese = await caso.ExecutarAsync(new Consulta("texto recebido"));

        Assert.Equal(MensagensDoBot.Erro, sintese.Resumo);
    }

    [Theory]
    [InlineData("NAO", false)]
    [InlineData("não.", false)]
    [InlineData("SIM", true)]
    [InlineData("Talvez", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("<think>vamos ver</think>\n\nNAO", false)]
    [InlineData("<think>vamos ver</think>\n\nSIM", true)]
    [InlineData("<think>raciocínio cortado no meio", true)]
    [InlineData("<think>só pensou</think>\n\n", true)]
    public void Classificador_so_libera_com_nao_claro(string? resposta, bool bloqueia) =>
        Assert.Equal(bloqueia, AssistenteDesinformacao.Infrastructure.Analise.ClassificadorDeNeutralidadeComIa.Interpretar(resposta));

    [Fact]
    public void Selecao_poe_checagem_primeiro_e_materias_da_mais_recente_para_a_mais_antiga()
    {
        Fonte Materia(string titulo, int dia) => new(titulo, new Uri($"https://jornal.example/{titulo}"), new DateOnly(2026, 11, dia), "Jornal");
        var antiga = Materia("antiga", 1);
        var nova = Materia("nova", 17);
        var meio = Materia("meio", 10);

        var escolhidas = ResponderConsulta.Selecionar([antiga, ChecagemRecente, nova, meio]);

        Assert.Equal([ChecagemRecente, nova, meio, antiga], escolhidas);
    }

    [Fact]
    public async Task Resumo_recusado_pelo_verificador_volta_ao_redator_com_o_motivo()
    {
        var redator = new RedatorQueAnotaCorrecao("primeira versão", "segunda versão");
        var verificador = new VerificadorComMotivo(r => r == "primeira versão" ? "D. A data 27/10 não aparece em nenhuma fonte." : null);

        var sintese = await Criar(["alegação"], [ChecagemRecente], redator, verificador: verificador)
            .ExecutarAsync(new Consulta("mensagem"));

        Assert.Equal("segunda versão", sintese.Resumo);
        Assert.Equal([null, "D. A data 27/10 não aparece em nenhuma fonte."], redator.Correcoes);
    }

    private sealed class RedatorQueAnotaCorrecao(params string[] resumos) : IRedatorDeSintese
    {
        public List<string?> Correcoes { get; } = [];

        public Task<string> RedigirAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct) =>
            RedigirAsync(alegacoes, fontes, null, ct);

        public Task<string> RedigirAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, string? correcao, CancellationToken ct)
        {
            Correcoes.Add(correcao);
            return Task.FromResult(resumos[Math.Min(Correcoes.Count - 1, resumos.Length - 1)]);
        }
    }

    private sealed class VerificadorComMotivo(Func<string, string?> problema) : IVerificadorDeResumo
    {
        public Task<bool> EstaFundamentadoAsync(string resumo, IReadOnlyList<Fonte> fontes, CancellationToken ct) =>
            Task.FromResult(problema(resumo) is null);

        public Task<string?> ProblemaAsync(string resumo, IReadOnlyList<Fonte> fontes, CancellationToken ct) =>
            Task.FromResult(problema(resumo));
    }

    [Fact]
    public async Task Manchetes_da_busca_pelo_texto_chegam_ao_extrator_como_contexto()
    {
        var extrator = new ExtratorQueAnotaManchetes();
        var caso = new ResponderConsulta(extrator, new ExtratorDeImagemFalso(["alegação"], disponivel: true),
            new BuscadorFalso([ChecagemRecente]), new RedatorFalso("resumo"), ClassificadorDeNeutralidadeFalso.Liberando,
            NullLogger<ResponderConsulta>.Instance, manchetes: new ManchetesFalsas(["Senado aprova fim da escala 6x1 (Agência Brasil, 14/10/2026)"]));

        await caso.ExecutarAsync(new Consulta("camara aprova fim da esca 6x1"));

        Assert.Equal(["Senado aprova fim da escala 6x1 (Agência Brasil, 14/10/2026)"], extrator.Manchetes);
    }

    [Fact]
    public async Task Falha_na_busca_de_manchetes_nao_impede_a_resposta()
    {
        var extrator = new ExtratorQueAnotaManchetes();
        var caso = new ResponderConsulta(extrator, new ExtratorDeImagemFalso(["alegação"], disponivel: true),
            new BuscadorFalso([ChecagemRecente]), new RedatorFalso("resumo"), ClassificadorDeNeutralidadeFalso.Liberando,
            NullLogger<ResponderConsulta>.Instance, manchetes: new ManchetesFalsas(null));

        var sintese = await caso.ExecutarAsync(new Consulta("texto"));

        Assert.Equal("resumo", sintese.Resumo);
        Assert.Empty(extrator.Manchetes!);
    }

    private sealed class ExtratorQueAnotaManchetes : IExtratorDeAlegacoes
    {
        public IReadOnlyList<string>? Manchetes { get; private set; }

        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct) => ExtrairAsync(texto, [], ct);

        public Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, IReadOnlyList<string> manchetes, CancellationToken ct)
        {
            Manchetes = manchetes;
            return Task.FromResult<IReadOnlyList<Alegacao>>([new Alegacao("alegação")]);
        }
    }

    /// <summary>Com null, simula a busca fora do ar.</summary>
    private sealed class ManchetesFalsas(IReadOnlyList<string>? manchetes) : IManchetesDaBusca
    {
        public Task<IReadOnlyList<string>> BuscarAsync(string texto, CancellationToken ct) =>
            manchetes is null ? throw new HttpRequestException("sem rede") : Task.FromResult(manchetes);
    }

    [Fact]
    public void Resumo_das_checagens_usa_so_os_dados_e_avisa_quando_e_antiga()
    {
        var antiga = new Checagem("Folha não publicou que Janja fez ritual", new Uri("https://agencia.example/c"), new DateOnly(2024, 11, 6), "Aos Fatos", "falso");

        var texto = MensagensDoBot.ResumoDasChecagens([antiga]);

        Assert.StartsWith("A agência Aos Fatos classificou como \"falso\" a checagem \"Folha não publicou que Janja fez ritual\", publicada em 06/11/2024.", texto);
        Assert.Contains("Ponto de atenção", texto);
    }

    [Theory]
    [InlineData("Ponto de atenção: a mensagem que compara Lula à Venezuela simplifica um debate complexo.", true)]
    [InlineData("Ponto de atenção: a mensagem ignora que a proposta ainda está em discussão.", true)]
    [InlineData("Ponto de atenção: a mensagem não menciona que a proposta ainda depende do Congresso.", false)]
    [InlineData("A oposição diz que o governo exagera nos gastos.", false)]
    public void Detecta_o_bot_julgando_a_mensagem(string resumo, bool julga) =>
        Assert.Equal(julga, GuardrailsDeNeutralidade.JulgaAMensagem(resumo));

    [Fact]
    public void Conclusao_por_ausencia_nao_e_barrada_quando_uma_agencia_checou() =>
        Assert.False(GuardrailsDeNeutralidade.TemConclusaoPorAusencia("Não há registro de que a Folha publicou essa matéria.", [ChecagemRecente]));

    private static ResponderConsulta Criar(string[] alegacoes, IReadOnlyList<Fonte> fontes, IRedatorDeSintese redator,
        ClassificadorDeNeutralidadeFalso? classificador = null, IVerificadorDeResumo? verificador = null, IJuizDeMensagem? juizDeMensagem = null, IEnriquecedorDeFontes? enriquecedor = null) =>
        new(new ExtratorFalso(alegacoes), new ExtratorDeImagemFalso(alegacoes, disponivel: true), new BuscadorFalso(fontes), redator,
            classificador ?? ClassificadorDeNeutralidadeFalso.Liberando, NullLogger<ResponderConsulta>.Instance, verificador: verificador, juizDeMensagem: juizDeMensagem, enriquecedor: enriquecedor);

    private sealed class EnriquecedorFalso : IEnriquecedorDeFontes
    {
        public Task<IReadOnlyList<Fonte>> EnriquecerAsync(IReadOnlyList<Fonte> fontes, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Fonte>>(fontes.Select(f => f with { Trecho = "TEXTO DA MATÉRIA" }).ToList());
    }

    private sealed class EnriquecedorQueFalha : IEnriquecedorDeFontes
    {
        public Task<IReadOnlyList<Fonte>> EnriquecerAsync(IReadOnlyList<Fonte> fontes, CancellationToken ct) =>
            throw new HttpRequestException("sem rede");
    }

    private sealed class JuizDeMensagemFalso(TipoDeMensagem tipo) : IJuizDeMensagem
    {
        public int Chamadas { get; private set; }

        public Task<TipoDeMensagem> JulgarAsync(string texto, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(tipo);
        }
    }

    private sealed class JuizDeMensagemQueFalha : IJuizDeMensagem
    {
        public Task<TipoDeMensagem> JulgarAsync(string texto, CancellationToken ct) => throw new HttpRequestException("sem rede");
    }

    private sealed class VerificadorFalso(Func<string, bool> aprova) : IVerificadorDeResumo
    {
        public int Chamadas { get; private set; }

        public Task<bool> EstaFundamentadoAsync(string resumo, IReadOnlyList<Fonte> fontes, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(aprova(resumo));
        }
    }

    private sealed class VerificadorQueFalha : IVerificadorDeResumo
    {
        public int Chamadas { get; private set; }

        public Task<bool> EstaFundamentadoAsync(string resumo, IReadOnlyList<Fonte> fontes, CancellationToken ct)
        {
            Chamadas++;
            throw new HttpRequestException("sem rede");
        }
    }

    private static ResponderConsulta CriarSemVisao() =>
        new(new ExtratorFalso(["alegação"]), new ExtratorDeImagemFalso(["alegação"], disponivel: false),
            new BuscadorFalso([ChecagemRecente]), new RedatorFalso("resumo"), ClassificadorDeNeutralidadeFalso.Liberando, NullLogger<ResponderConsulta>.Instance);

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

    private sealed class RedatorSequencial(params string[] resumos) : IRedatorDeSintese
    {
        public int Chamadas { get; private set; }

        public Task<string> RedigirAsync(IReadOnlyList<Alegacao> alegacoes, IReadOnlyList<Fonte> fontes, CancellationToken ct) =>
            Task.FromResult(resumos[Math.Min(Chamadas++, resumos.Length - 1)]);
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
