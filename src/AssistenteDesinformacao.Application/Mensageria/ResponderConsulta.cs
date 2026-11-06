using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Domain.Mensageria;
using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Domain.Sinteses;

namespace AssistenteDesinformacao.Application.Mensageria;

public sealed class ResponderConsulta(
    IExtratorDeAlegacoes extrator,
    IBuscadorDeFontes buscador,
    IRedatorDeSintese redator)
{
    public const int MaximoDeAlegacoes = 3;
    public const int MaximoDeFontes = 5;

    public async Task<Sintese> ExecutarAsync(Consulta consulta, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(consulta.Texto))
            return SemApuracao();

        var alegacoes = (await extrator.ExtrairAsync(consulta.Texto, ct))
            .Take(MaximoDeAlegacoes)
            .ToList();
        if (alegacoes.Count == 0)
            return SemApuracao();

        var encontradas = new List<Fonte>();
        foreach (var alegacao in alegacoes)
            encontradas.AddRange(await buscador.BuscarAsync(alegacao, ct));

        var fontes = encontradas
            .DistinctBy(f => f.Link)
            .OrderByDescending(f => f.DataPublicacao)
            .Take(MaximoDeFontes)
            .ToList();
        if (fontes.Count == 0)
            return SemApuracao();

        var resumo = (await redator.RedigirAsync(alegacoes, fontes, ct)).Trim();
        return new Sintese(resumo.Length > 0 ? resumo : MensagensDoBot.ResumoPadrao, fontes);
    }

    private static Sintese SemApuracao() => new(MensagensDoBot.SemApuracao, []);
}
