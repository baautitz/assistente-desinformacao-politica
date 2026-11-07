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
    ILogger<ResponderConsulta> logger)
{
    public const int MaximoDeAlegacoes = 3;
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
            foreach (var extraida in extraidas)
                if (await InduzVotoAsync(extraida.Texto, ct))
                    return Mensagem(MensagensDoBot.RecomendacaoEleitoral);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(consulta.Texto))
                return Mensagem(MensagensDoBot.SemAlegacao);
            if (await InduzVotoAsync(consulta.Texto, ct))
                return Mensagem(MensagensDoBot.RecomendacaoEleitoral);
            extraidas = await extrator.ExtrairAsync(consulta.Texto, ct);
        }

        var alegacoes = extraidas
            .Take(MaximoDeAlegacoes)
            .ToList();
        if (alegacoes.Count == 0)
            return Mensagem(MensagensDoBot.SemAlegacao);

        var encontradas = new List<Fonte>();
        foreach (var alegacao in alegacoes)
            encontradas.AddRange(await buscador.BuscarAsync(alegacao, ct));

        var fontes = encontradas
            .Where(GuardrailsDeNeutralidade.TemLink)
            .DistinctBy(f => f.Link)
            .OrderByDescending(f => f.DataPublicacao)
            .Take(MaximoDeFontes)
            .ToList();
        if (fontes.Count == 0)
            return Mensagem(MensagensDoBot.SemApuracao);

        var resumo = (await redator.RedigirAsync(alegacoes, fontes, ct)).Trim();
        var valido = resumo.Length > 0
            && !await InduzVotoAsync(resumo, ct)
            && !GuardrailsDeNeutralidade.CitaLinkForaDasFontes(resumo, fontes)
            && !GuardrailsDeNeutralidade.TemVereditoPorContaPropria(resumo, fontes);
        return new Sintese(valido ? resumo : MensagensDoBot.ResumoPadrao, fontes);
    }

    /// <summary>Regex primeiro (barata e sem rede); se passar, o classificador julga o sentido.</summary>
    private async Task<bool> InduzVotoAsync(string texto, CancellationToken ct) =>
        GuardrailsDeNeutralidade.TemRecomendacaoEleitoral(texto) || await neutralidade.InduzVotoAsync(texto, ct);

    private static Sintese Mensagem(string texto) => new(texto, []);
}
