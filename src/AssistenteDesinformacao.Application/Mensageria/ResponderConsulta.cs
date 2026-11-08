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
    IJuizDeRelevancia? juiz = null)
{
    public const int MaximoDeAlegacoes = 3;
    public const int MaximoDeTentativasDeBusca = 3;
    private const string MotivoForaDoPortugues = "texto fora do português";
    public const int MaximoDeFontes = 5;
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
            extraidas = await extrator.ExtrairAsync(consulta.Texto, ct);
            // Opinião ou previsão ("fulano vai destruir o país") não tem fato isolado, mas o assunto tem apuração publicada.
            // Em vez de pedir outro texto ao usuário, o bot busca pelo que ele escreveu.
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

        progresso?.Report(EtapaDaConsulta.Escrevendo);
        string resumo;
        string? motivo;
        var tentativa = 0;
        do
        {
            // O modelo às vezes mistura idiomas ou solta lixo: uma nova tentativa costuma resolver.
            resumo = (await redator.RedigirAsync(alegacoes, fontes, ct)).Trim();
            motivo = await MotivoParaDescartarAsync(resumo, fontes, ct);
            tentativa++;
        }
        while (motivo == MotivoForaDoPortugues && tentativa < 2);
        if (motivo is null)
            logger.LogInformation("Resumo aprovado pelos guardrails; {Fontes} fontes ({Checagens} checagens)",
                fontes.Count, fontes.Count(f => f is Checagem));
        else
            logger.LogWarning("Resumo descartado ({Motivo}); enviando só a lista de {Fontes} fontes", motivo, fontes.Count);
        return new Sintese(motivo is null ? resumo : MensagensDoBot.ResumoPadrao, fontes);
    }

    /// <summary>Nulo quando o resumo pode ser enviado; senão, o motivo (só para o log).</summary>
    private async Task<string?> MotivoParaDescartarAsync(string resumo, IReadOnlyList<Fonte> fontes, CancellationToken ct)
    {
        if (resumo.Length == 0)
            return "resumo vazio";
        if (GuardrailsDeNeutralidade.TemTextoForaDoPortugues(resumo))
            return MotivoForaDoPortugues;
        if (await InduzVotoAsync(resumo, ct))
            return "recomendação eleitoral";
        if (GuardrailsDeNeutralidade.CitaLinkForaDasFontes(resumo, fontes))
            return "link fora das fontes";
        if (GuardrailsDeNeutralidade.TemVereditoPorContaPropria(resumo, fontes))
            return "veredito sem agência";
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
                return relevantes.Take(MaximoDeFontes).ToList();

            if (juiz is null || tentativa >= MaximoDeTentativasDeBusca)
                return [];

            logger.LogInformation("Nenhuma fonte relevante; reformulando a alegação para a tentativa {Proxima}", tentativa + 1);

            var reformuladas = new List<Alegacao>();
            foreach (var alegacao in alegacoes)
                reformuladas.Add(await juiz.ReformularAsync(alegacao, tentativa, ct));
            atuais = reformuladas;
        }
    }

    /// <summary>
    /// A regra é sobre o que o BOT diz, não sobre o que o usuário envia: só o resumo gerado passa por aqui.
    /// Regex primeiro (barata e sem rede); se passar, o classificador julga o sentido.
    /// </summary>
    private async Task<bool> InduzVotoAsync(string texto, CancellationToken ct) =>
        GuardrailsDeNeutralidade.TemRecomendacaoEleitoral(texto) || await neutralidade.InduzVotoAsync(texto, ct);

    private static Sintese Mensagem(string texto) => new(texto, []);
}
