using System.Text.RegularExpressions;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Mensageria;

/// <summary>Regras validadas antes do envio: neutralidade eleitoral e link em toda síntese.</summary>
public static partial class GuardrailsDeNeutralidade
{
    /// <summary>Pedido ou texto que induz a recomendar candidato, partido ou voto.</summary>
    [GeneratedRegex(
        @"\b(em\s+quem\s+(eu\s+)?(devo\s+|vou\s+|posso\s+)?votar|(devo|deveria|posso)\s+votar|vote\s+(em|no|na|nos|nas)\b|n[aã]o\s+vote\b|vot(e|ar)\s+(certo|errado)|melhor\s+(candidat[oa]|partido)|pior\s+(candidat[oa]|partido)|(recomend\w+|indic\w+|sugir\w+)\s+(um\s+|o\s+|a\s+)?(candidat[oa]|partido|voto)|candidat[oa]\s+(ideal|certo)|(apoi\w+|escolh\w+|elei\w+)\s+(o\s+|a\s+)?(candidat[oa]|partido)|(merece|mere[cç]em)\s+((o|seu|meu|nosso)\s+)*voto|voto\s+([uú]til|consciente)|anul(e|ar)\s+(o\s+|seu\s+)?voto|\bvot(e|em|a|ar|ando)\s*(n[oa]s?\s+|em\s+)?\d{2,5}\b|\bvot(e|em|a|ar)\s+(n[oa]s?\s+)?(vinte|trinta|quarenta|cinquenta|sessenta|setenta|oitenta|noventa|cem)\b|\b(digit\w+|coloqu\w+|aperte|aperten\w+|tecle\w*|marqu\w+)\s+(o\s+|no\s+)?\d{2,5}\s+na\s+urna)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RecomendacaoEleitoral();

    [GeneratedRegex(@"https?://[^\s)\]>]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Links();

    /// <summary>Veredito dito pelo bot: "é falso", "é verdade", "é mentira", "fake news".</summary>
    [GeneratedRegex(
        @"(\b(é|são|foi|era)\s+(totalmente\s+|completamente\s+|uma\s+)?(falso|falsa|falsos|falsas|verdade|verdadeiro|verdadeira|mentira|mentiroso|mentirosa)\b|\bfake\s*news\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Veredito();

    [GeneratedRegex(@"[.!?\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex Frases();

    private static readonly string[] Atribuicoes = ["segundo", "de acordo com", "conforme", "classificou", "classificação", "para a agência", "para o "];

    public static bool TemRecomendacaoEleitoral(string texto) => RecomendacaoEleitoral().IsMatch(texto);

    public static bool TemLink(Fonte fonte) =>
        fonte.Link.IsAbsoluteUri && fonte.Link.Scheme is "http" or "https";

    /// <summary>
    /// Verdadeiro se alguma frase do resumo dá um veredito sem atribuí-lo (a uma agência das fontes ou por "segundo...").
    /// Heurística modesta: só pega as formulações mais comuns.
    /// </summary>
    public static bool TemVereditoPorContaPropria(string resumo, IReadOnlyList<Fonte> fontes) =>
        Frases().Split(resumo)
            .Where(frase => Veredito().IsMatch(frase))
            .Any(frase => !Atribuicoes.Any(a => frase.Contains(a, StringComparison.OrdinalIgnoreCase))
                && !fontes.OfType<Checagem>().Any(c => frase.Contains(c.Agencia, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Verdadeiro se o resumo cita algum link que não veio das fontes (possível invenção da IA).</summary>
    public static bool CitaLinkForaDasFontes(string resumo, IReadOnlyList<Fonte> fontes) =>
        Links().Matches(resumo).Any(m => !fontes.Any(f =>
            string.Equals(m.Value.TrimEnd('.', ',', ';'), f.Link.AbsoluteUri, StringComparison.OrdinalIgnoreCase)));
}
