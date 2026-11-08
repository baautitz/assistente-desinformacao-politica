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
    public const int MaximoDeFontes = 5;
    public const int MaximoDeBytesDaImagem = 5 * 1024 * 1024;

    public async Task<Sintese> ExecutarAsync(Consulta consulta, CancellationToken ct = default)
    {
        try
        {
            return await ResponderAsync(consulta, ct);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // Nunca logar o texto da consulta: mensagens de usuário não são guardadas.
            logger.LogError(erro, "Falha ao responder consulta");
            return Mensagem(MensagensDoBot.Erro);
        }
    }

    private async Task<Sintese> ResponderAsync(Consulta consulta, CancellationToken ct)
    {
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
        }

        var alegacoes = extraidas
            .Take(MaximoDeAlegacoes)
            .ToList();
        if (alegacoes.Count == 0)
            return Mensagem(MensagensDoBot.SemAlegacao);

        var fontes = await BuscarFontesRelevantesAsync(alegacoes, ct);
        if (fontes.Count == 0)
            return Mensagem(MensagensDoBot.SemApuracao);

        var resumo = (await redator.RedigirAsync(alegacoes, fontes, ct)).Trim();
        var valido = resumo.Length > 0
            && !await InduzVotoAsync(resumo, ct)
            && !GuardrailsDeNeutralidade.CitaLinkForaDasFontes(resumo, fontes)
            && !GuardrailsDeNeutralidade.TemVereditoPorContaPropria(resumo, fontes);
        return new Sintese(valido ? resumo : MensagensDoBot.ResumoPadrao, fontes);
    }

    /// <summary>
    /// Busca, confere se as fontes tratam mesmo da alegação e, se nenhuma serviu, reescreve a alegação e busca de novo
    /// (até <see cref="MaximoDeTentativasDeBusca"/> vezes). Sem fonte relevante, devolve vazio: melhor dizer que não achou do que listar lixo.
    /// </summary>
    private async Task<IReadOnlyList<Fonte>> BuscarFontesRelevantesAsync(IReadOnlyList<Alegacao> alegacoes, CancellationToken ct)
    {
        var atuais = alegacoes;
        for (var tentativa = 1; ; tentativa++)
        {
            var encontradas = new List<Fonte>();
            foreach (var alegacao in atuais)
                encontradas.AddRange(await buscador.BuscarAsync(alegacao, ct));

            var validas = encontradas.Where(GuardrailsDeNeutralidade.TemLink).DistinctBy(f => f.Link).ToList();
            // Checagem de agência é a fonte principal (mais recente primeiro); as matérias seguem a ordem de relevância da busca.
            var candidatas = validas.Where(f => f is Checagem).OrderByDescending(f => f.DataPublicacao)
                .Concat(validas.Where(f => f is not Checagem))
                .ToList();

            var relevantes = candidatas.Count == 0 || juiz is null
                ? candidatas
                : await juiz.FiltrarAsync(alegacoes, candidatas, ct);
            if (relevantes.Count > 0)
                return relevantes.Take(MaximoDeFontes).ToList();

            if (juiz is null || tentativa >= MaximoDeTentativasDeBusca)
                return [];

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
