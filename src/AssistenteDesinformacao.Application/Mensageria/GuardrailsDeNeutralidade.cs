using System.Text.RegularExpressions;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Mensageria;

/// <summary>Regras validadas antes do envio: neutralidade eleitoral e link em toda síntese.</summary>
public static partial class GuardrailsDeNeutralidade
{
    /// <summary>
    /// Atalho barato para os casos óbvios, só para poupar uma chamada à IA. Não é a defesa: quem decide
    /// os casos de verdade é o <see cref="IClassificadorDeNeutralidade"/>. Não acrescente variações aqui.
    /// </summary>
    [GeneratedRegex(
        @"\b(em\s+quem\s+(eu\s+)?(devo\s+|vou\s+|posso\s+)?votar|(devo|deveria|posso)\s+votar|vote\s+(em|no|na|nos|nas)\b|n[aã]o\s+vote\b|vote\s*\d{2,5}\b|melhor\s+(candidat[oa]|partido)|pior\s+(candidat[oa]|partido))",
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
