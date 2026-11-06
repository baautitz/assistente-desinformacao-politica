using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Domain.Sinteses;

/// <summary>Resposta do bot: resumo neutro do que as fontes dizem sobre a alegação, com as fontes usadas.</summary>
public sealed record Sintese(string Resumo, IReadOnlyList<Fonte> Fontes)
{
    public bool EncontrouFontes => Fontes.Count > 0;
}
