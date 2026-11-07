using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Domain.Mensageria;
using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Domain.Sinteses;
using Microsoft.Extensions.Logging;

namespace AssistenteDesinformacao.Application.Mensageria;

public sealed class ResponderConsulta(
    IExtratorDeAlegacoes extrator,
    IBuscadorDeFontes buscador,
    IRedatorDeSintese redator,
    ILogger<ResponderConsulta> logger)
{
    public const int MaximoDeAlegacoes = 3;
    public const int MaximoDeFontes = 5;

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
        if (string.IsNullOrWhiteSpace(consulta.Texto))
            return Mensagem(MensagensDoBot.SemAlegacao);

        if (GuardrailsDeNeutralidade.TemRecomendacaoEleitoral(consulta.Texto))
            return Mensagem(MensagensDoBot.RecomendacaoEleitoral);

        var alegacoes = (await extrator.ExtrairAsync(consulta.Texto, ct))
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
            && !GuardrailsDeNeutralidade.TemRecomendacaoEleitoral(resumo)
            && !GuardrailsDeNeutralidade.CitaLinkForaDasFontes(resumo, fontes);
        return new Sintese(valido ? resumo : MensagensDoBot.ResumoPadrao, fontes);
    }

    private static Sintese Mensagem(string texto) => new(texto, []);
}
