using System.Globalization;
using AssistenteDesinformacao.Domain.Feedbacks;

namespace AssistenteDesinformacao.Application.Feedbacks;

public interface IRepositorioDeFeedbacks
{
    /// <summary>Grava o voto da resposta. Se a resposta já tem voto, substitui em vez de duplicar.</summary>
    Task RegistrarAsync(Feedback feedback, CancellationToken ct);
}

/// <summary>Conteúdo do botão inline: identifica a resposta e o voto, sem nenhum dado do usuário.</summary>
public static class DadosDoBotao
{
    public const string TextoUtil = "👍";
    public const string TextoInutil = "👎";

    private const string Prefixo = "fb:";

    public static string Gerar(Guid respostaId, bool util) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefixo}{respostaId:N}:{(util ? 1 : 0)}");

    public static bool TentarLer(string? dados, out Guid respostaId, out bool util)
    {
        respostaId = default;
        util = false;
        if (dados is null || !dados.StartsWith(Prefixo, StringComparison.Ordinal))
            return false;

        var partes = dados[Prefixo.Length..].Split(':');
        if (partes.Length != 2
            || !Guid.TryParseExact(partes[0], "N", out respostaId)
            || partes[1] is not ("0" or "1"))
            return false;

        util = partes[1] == "1";
        return true;
    }
}

public sealed class RegistrarFeedback(IRepositorioDeFeedbacks repositorio, TimeProvider relogio)
{
    /// <summary>Devolve false quando os dados do botão não são de feedback.</summary>
    public async Task<bool> ExecutarAsync(string? dadosDoBotao, CancellationToken ct)
    {
        if (!DadosDoBotao.TentarLer(dadosDoBotao, out var respostaId, out var util))
            return false;

        await repositorio.RegistrarAsync(new Feedback(respostaId, util, relogio.GetUtcNow()), ct);
        return true;
    }
}
