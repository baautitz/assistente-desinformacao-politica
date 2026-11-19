using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Domain.Mensageria;
using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Domain.Sinteses;
using Microsoft.Extensions.Logging;

namespace AssistenteDesinformacao.Application.Mensageria;

public sealed class ResponderConsulta(
    IExtratorDeAlegacoes extrator,
    IExtratorDeAlegacoesDeImagem extratorDeImagem,
    IBuscadorDeFontes buscador,
    IRedatorDeSintese redator,
    IClassificadorDeNeutralidade neutralidade,
    ILogger<ResponderConsulta> logger,
    IJuizDeRelevancia? juiz = null,
    IVerificadorDeResumo? verificador = null,
    IJuizDeMensagem? juizDeMensagem = null,
    IEnriquecedorDeFontes? enriquecedor = null,
    IManchetesDaBusca? manchetes = null)
{
    public const int MaximoDeAlegacoes = 3;
    public const int MaximoDeTentativasDeBusca = 3;
    private const string MotivoForaDoPortugues = "texto fora do português";
    private const string MotivoNaoFundamentado = "resumo não apoiado pelas fontes";
    public const int MaximoDeTentativasDoResumo = 4;
    public const int MaximoDeFontes = 6;
    /// <summary>Checagens que entram no resumo: mais que isso costuma ser a mesma afirmação checada por várias agências.</summary>
    public const int MaximoDeChecagens = 2;
    public const int MaximoDeCaracteresDaBusca = 500;
    public const int MaximoDeBytesDaImagem = 5 * 1024 * 1024;

    public async Task<Sintese> ExecutarAsync(
        Consulta consulta, CancellationToken ct = default, IProgress<EtapaDaConsulta>? progresso = null)
    {
        try
        {
            return await ResponderAsync(consulta, progresso, ct);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // Nunca logar o texto da consulta: mensagens de usuário não são guardadas.
            if (EhFalhaDeRede(erro))
                // Rede ou provedor fora do ar: uma linha basta, sem o muro de stack trace.
                logger.LogError("Falha ao responder consulta: {Tipo}: {Mensagem}", erro.GetType().Name, PrimeiraLinha(erro));
            else
                logger.LogError(erro, "Falha ao responder consulta");
            return Mensagem(MensagensDoBot.Erro);
        }
    }

    private static bool EhFalhaDeRede(Exception erro) =>
        erro is HttpRequestException or TimeoutException or TaskCanceledException
        || (erro is AggregateException agregado && agregado.InnerExceptions.All(EhFalhaDeRede))
        || (erro.GetType().Name == "ClientResultException" && erro.InnerException is { } interna && EhFalhaDeRede(interna));

    private static string PrimeiraLinha(Exception erro) => erro.Message.Split('\n')[0].Trim();

    private async Task<Sintese> ResponderAsync(Consulta consulta, IProgress<EtapaDaConsulta>? progresso, CancellationToken ct)
    {
        progresso?.Report(EtapaDaConsulta.Lendo);
        IReadOnlyList<Alegacao> extraidas;
        if (consulta.Imagem is { } imagem)
        {
            if (!extratorDeImagem.Disponivel)
                return Mensagem(MensagensDoBot.SoLeTexto);
            if (imagem.Conteudo.Length > MaximoDeBytesDaImagem)
                return Mensagem(MensagensDoBot.ImagemGrande);
            extraidas = await extratorDeImagem.ExtrairAsync(imagem, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(consulta.Texto))
                return Mensagem(MensagensDoBot.SemAlegacao);
            // Pré-julgamento: pedido de voto não é afirmação para conferir. Responde direto, sem gastar IA nem buscar fonte.
            if (GuardrailsDeNeutralidade.PedeRecomendacaoDeVoto(consulta.Texto))
            {
                logger.LogInformation("Pedido de recomendação de voto: respondendo sem buscar fontes");
                return Mensagem(MensagensDoBot.NaoRecomendoVoto);
            }

            // A IA julga toda mensagem antes de buscar: pedido de voto, pedido de opinião/ranking e assunto fora do escopo não têm o que apurar.
            if (juizDeMensagem is not null && await JulgarMensagemAsync(consulta.Texto, juizDeMensagem, ct) is { } recusa)
                return Mensagem(recusa);

            // Primeiro pesquisa o texto como veio: as manchetes dizem à IA de que assunto se trata, sem ela adivinhar.
            extraidas = await extrator.ExtrairAsync(consulta.Texto, await ManchetesAsync(consulta.Texto, ct), ct);
            // Opinião ou previsão ("fulano vai destruir o país") não tem fato isolado, mas o assunto tem apuração publicada.
            // Em vez de pedir outro texto ao usuário, o bot busca pelo que ele escreveu.
            // Sem afirmação, a IA julga se a mensagem pede ou induz voto (regex só como atalho). Se sim, não há o que apurar:
            // buscar pelo texto traria notícia aleatória de política. Só roda sem afirmação, para não barrar notícia encaminhada que cita voto.
            if (extraidas.Count == 0 && await InduzVotoAsync(consulta.Texto, ct))
            {
                logger.LogInformation("Mensagem sem afirmação que pede ou induz voto: respondendo sem buscar fontes");
                return Mensagem(MensagensDoBot.NaoRecomendoVoto);
            }

            if (extraidas.Count == 0)
            {
                logger.LogInformation("Nenhum fato isolado na mensagem; buscando pelo texto como um todo");
                var texto = consulta.Texto.Trim();
                extraidas = [new Alegacao(texto.Length > MaximoDeCaracteresDaBusca ? texto[..MaximoDeCaracteresDaBusca] : texto)];
            }
        }

        var alegacoes = extraidas
            .Take(MaximoDeAlegacoes)
            .ToList();
        logger.LogInformation("Alegações extraídas: {Quantidade}", alegacoes.Count);
        if (alegacoes.Count == 0)
            return Mensagem(MensagensDoBot.SemAlegacao);

        var fontes = await BuscarFontesRelevantesAsync(alegacoes, progresso, ct);
        if (fontes.Count == 0)
        {
            logger.LogInformation("Nenhuma fonte relevante: respondendo que não encontrou apuração");
            return Mensagem(MensagensDoBot.SemApuracao);
        }

        fontes = Fonte.SemChecagensDefasadas(fontes);

        // As fontes já foram escolhidas: agora lê o começo do texto de cada uma para o resumo não depender só do título.
        if (enriquecedor is not null)
        {
            try
            {
                fontes = await enriquecedor.EnriquecerAsync(fontes, ct);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                logger.LogWarning(erro, "Falha ao ler o texto das matérias; seguindo só com os títulos");
            }
        }

        progresso?.Report(EtapaDaConsulta.Escrevendo);
        string resumo;
        Recusa? recusaDoResumo = null;
        var tentativa = 0;
        do
        {
            // O modelo às vezes mistura idiomas, acrescenta fato ou erra data: a nova tentativa recebe o motivo da recusa para corrigir.
            resumo = (await redator.RedigirAsync(alegacoes, fontes, recusaDoResumo?.Correcao, ct)).Trim();
            recusaDoResumo = await MotivoParaDescartarAsync(resumo, fontes, ct);
            tentativa++;
            if (recusaDoResumo is not null)
                logger.LogInformation("Resumo recusado na tentativa {Tentativa}: {Motivo}", tentativa, recusaDoResumo.Correcao);
        }
        while (recusaDoResumo is { Repetir: true } && tentativa < MaximoDeTentativasDoResumo);
        var motivo = recusaDoResumo?.Motivo;
        if (motivo is null)
            logger.LogInformation("Resumo aprovado pelos guardrails; {Fontes} fontes ({Checagens} checagens)",
                fontes.Count, fontes.Count(f => f is Checagem));
        else
            logger.LogWarning("Resumo descartado ({Motivo}); enviando só a lista de {Fontes} fontes", motivo, fontes.Count);
        if (motivo is null)
            return new Sintese(resumo, fontes);
        // Recomendação de voto não ganha nem o resumo das checagens: o texto inteiro é descartado.
        var checagens = fontes.OfType<Checagem>().ToList();
        return new Sintese(checagens.Count > 0 && motivo != "recomendação eleitoral"
            ? MensagensDoBot.ResumoDasChecagens(checagens)
            : MensagensDoBot.ResumoPadrao, fontes);
    }

    /// <summary>Por que o resumo foi recusado: o motivo vai para o log, a correção volta ao redator e só alguns motivos valem nova tentativa.</summary>
    private sealed record Recusa(string Motivo, string Correcao, bool Repetir);

    /// <summary>Nulo quando o resumo pode ser enviado; senão, a recusa.</summary>
    private async Task<Recusa?> MotivoParaDescartarAsync(string resumo, IReadOnlyList<Fonte> fontes, CancellationToken ct)
    {
        if (resumo.Length == 0)
            return new("resumo vazio", "a resposta veio vazia", true);
        if (GuardrailsDeNeutralidade.TemTextoForaDoPortugues(resumo))
            return new(MotivoForaDoPortugues, "havia palavras em outro idioma ou sem sentido; escreva só em português do Brasil", true);
        if (await InduzVotoAsync(resumo, ct))
            return new("recomendação eleitoral", "o texto parecia recomendar voto", false);
        if (GuardrailsDeNeutralidade.CitaLinkForaDasFontes(resumo, fontes))
            return new("link fora das fontes", "não escreva links", true);
        if (GuardrailsDeNeutralidade.TemVereditoPorContaPropria(resumo, fontes))
            return new("veredito sem agência", "o texto disse por conta própria que algo é verdadeiro ou falso; atribua a quem disse", true);
        if (GuardrailsDeNeutralidade.TemConclusaoPorAusencia(resumo, fontes))
            return new("conclusão por ausência",
                "o texto concluiu algo da falta de notícia (\"não há confirmação\", \"nada indica\"); relate só o que as fontes afirmam", true);
        // Por último, porque é uma chamada à IA: só roda se as regras baratas acima já aprovaram.
        if (verificador is not null)
        {
            try
            {
                if (await verificador.ProblemaAsync(resumo, fontes, ct) is { } problema)
                    return new(MotivoNaoFundamentado, problema, true);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                // Sem conseguir conferir, não envia o resumo (nem repete a tentativa): vai só a lista de fontes.
                logger.LogWarning(erro, "Falha ao verificar o resumo contra as fontes");
                return new("verificador indisponível", "", false);
            }
        }
        return null;
    }

    /// <summary>
    /// Busca, confere se as fontes tratam mesmo da alegação e, se nenhuma serviu, reescreve a alegação e busca de novo
    /// (até <see cref="MaximoDeTentativasDeBusca"/> vezes). Sem fonte relevante, devolve vazio: melhor dizer que não achou do que listar lixo.
    /// </summary>
    private async Task<IReadOnlyList<Fonte>> BuscarFontesRelevantesAsync(
        IReadOnlyList<Alegacao> alegacoes, IProgress<EtapaDaConsulta>? progresso, CancellationToken ct)
    {
        var atuais = alegacoes;
        for (var tentativa = 1; ; tentativa++)
        {
            progresso?.Report(tentativa == 1 ? EtapaDaConsulta.Buscando : EtapaDaConsulta.BuscandoDeNovo);
            var encontradas = new List<Fonte>();
            foreach (var alegacao in atuais)
                encontradas.AddRange(await buscador.BuscarAsync(alegacao, ct));

            var validas = encontradas.Where(GuardrailsDeNeutralidade.TemLink).DistinctBy(f => f.Link).ToList();
            // Checagem de agência é a fonte principal (mais recente primeiro); as matérias seguem a ordem de relevância da busca.
            var candidatas = validas.Where(f => f is Checagem).OrderByDescending(f => f.DataPublicacao)
                .Concat(validas.Where(f => f is not Checagem))
                .ToList();

            if (candidatas.Count > 0 && juiz is not null)
                progresso?.Report(EtapaDaConsulta.Conferindo);
            var relevantes = candidatas.Count == 0 || juiz is null
                ? candidatas
                : await juiz.FiltrarAsync(alegacoes, candidatas, ct);
            logger.LogInformation("Busca {Tentativa}/{Maximo}: {Candidatas} candidatas ({Checagens} checagens), {Relevantes} relevantes",
                tentativa, MaximoDeTentativasDeBusca, candidatas.Count, candidatas.Count(f => f is Checagem), relevantes.Count);
            if (relevantes.Count > 0)
                return Selecionar(relevantes);

            if (juiz is null || tentativa >= MaximoDeTentativasDeBusca)
                return [];

            logger.LogInformation("Nenhuma fonte relevante; reformulando a alegação para a tentativa {Proxima}", tentativa + 1);

            var reformuladas = new List<Alegacao>();
            foreach (var alegacao in alegacoes)
                reformuladas.Add(await juiz.ReformularAsync(alegacao, tentativa, ct));
            atuais = reformuladas;
        }
    }

    /// <summary>Sem manchetes (busca fora do ar), a extração segue só com o texto.</summary>
    private async Task<IReadOnlyList<string>> ManchetesAsync(string texto, CancellationToken ct)
    {
        if (manchetes is null)
            return [];
        try
        {
            var achadas = await manchetes.BuscarAsync(texto, ct);
            logger.LogInformation("Manchetes de contexto: {Quantidade}", achadas.Count);
            return achadas;
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogWarning("Falha ao buscar manchetes de contexto: {Erro}", erro.Message);
            return [];
        }
    }

    /// <summary>
    /// Checagens primeiro (no máximo <see cref="MaximoDeChecagens"/>), depois as matérias da mais recente para a mais antiga:
    /// a situação atual vem das notícias novas, e a ordem da busca não diz nada sobre atualidade.
    /// </summary>
    public static IReadOnlyList<Fonte> Selecionar(IReadOnlyList<Fonte> relevantes) =>
        relevantes.Where(f => f is Checagem).Take(MaximoDeChecagens)
            .Concat(relevantes.Where(f => f is not Checagem).OrderByDescending(f => f.DataPublicacao ?? DateOnly.MinValue))
            // A mesma matéria chega por links diferentes ("G1" e "g1 Política", Google e RSS): o título igual denuncia.
            .DistinctBy(f => f.Titulo.Trim().ToLowerInvariant())
            .Take(MaximoDeFontes)
            .ToList();

    /// <summary>
    /// A regra é sobre o que o BOT diz, não sobre o que o usuário envia: só o resumo gerado passa por aqui.
    /// Regex primeiro (barata e sem rede); se passar, o classificador julga o sentido.
    /// </summary>
    private async Task<bool> InduzVotoAsync(string texto, CancellationToken ct) =>
        GuardrailsDeNeutralidade.TemRecomendacaoEleitoral(texto) || await neutralidade.InduzVotoAsync(texto, ct);

    /// <summary>Texto da recusa quando a juíza barra a mensagem; nulo quando a mensagem segue para a apuração. Se a IA falhar, segue (os guardrails continuam valendo).</summary>
    private async Task<string?> JulgarMensagemAsync(string texto, IJuizDeMensagem juiz, CancellationToken ct)
    {
        TipoDeMensagem tipo;
        try
        {
            tipo = await juiz.JulgarAsync(texto, ct);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogWarning(erro, "Falha ao julgar a mensagem; seguindo com a apuração normal");
            return null;
        }

        logger.LogInformation("Mensagem julgada como {Tipo}", tipo);
        return tipo switch
        {
            TipoDeMensagem.PedidoDeVoto => MensagensDoBot.NaoRecomendoVoto,
            TipoDeMensagem.PedidoDeOpiniao => MensagensDoBot.NaoOpino,
            TipoDeMensagem.ForaDoEscopo => MensagensDoBot.ForaDoEscopo,
            _ => null,
        };
    }

    private static Sintese Mensagem(string texto) => new(texto, []);
}
