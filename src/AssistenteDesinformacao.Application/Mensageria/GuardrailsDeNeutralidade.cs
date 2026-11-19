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

    /// <summary>
    /// Pré-julgamento da mensagem do usuário: pergunta direta ao bot pedindo em quem votar ("em quem devo votar?", "qual o melhor candidato?").
    /// Isso não é uma afirmação para conferir, então o bot nem busca fontes. Só vale para mensagem curta: texto longo é conteúdo encaminhado.
    /// </summary>
    [GeneratedRegex(
        @"\b(em\s+quem\s+(eu\s+)?(devo\s+|vou\s+|posso\s+|deveria\s+)?votar|(devo|deveria|posso)\s+votar|(qual|quem)\s+([ée]\s+)?(o\s+|a\s+)?(melhor|pior)\s+(candidat[oa]|partido|presidente)|qual\s+(candidat[oa]|partido)\s+(devo|eu\s+devo)\s+(votar|escolher))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PedidoDeRecomendacao();

    public const int MaximoDoPedidoDeVoto = 160;

    public static bool PedeRecomendacaoDeVoto(string texto) =>
        texto.Trim().Length <= MaximoDoPedidoDeVoto && PedidoDeRecomendacao().IsMatch(texto);

    [GeneratedRegex(@"https?://[^\s)\]>]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Links();

    /// <summary>Veredito dito pelo bot: "é falso", "é verdade", "é mentira", "fake news".</summary>
    [GeneratedRegex(
        @"(\b(é|são|foi|era)\s+(totalmente\s+|completamente\s+|uma\s+)?(falso|falsa|falsos|falsas|verdade|verdadeiro|verdadeira|mentira|mentiroso|mentirosa)\b|\bfake\s*news\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Veredito();

    /// <summary>Conclusão tirada da falta de notícia: "não há confirmação", "nada indica", "não foi confirmado".</summary>
    [GeneratedRegex(
        @"\bnada\s+indica\b|\b(n[aã]o\s+h[aá]|n[aã]o\s+existe|sem)\s+(qualquer\s+|nenhuma?\s+)?(confirma[cç][aã]o|registro|evid[eê]ncia|ind[ií]cio|prova|informa[cç][oõ]es|not[ií]cia)s?\b|\bn[aã]o\s+(foi|est[aá]|h[aá]\s+nada)\s+(confirmad|comprovad|registrad)[oa]s?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConclusaoPorAusencia();

    /// <summary>O bot avaliando a mensagem com voz própria: "a mensagem simplifica um debate complexo", "a mensagem ignora que...".</summary>
    [GeneratedRegex(
        @"\b(a|essa|esta)\s+(mensagem|afirma[cç][aã]o|compara[cç][aã]o|alega[cç][aã]o)\b[^.!?\n]*\b(simplific|exager|distorc|ignor|desconsider|n[aã]o\s+considera|deturp|generaliz)\w*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JulgamentoDaMensagem();

    /// <summary>
    /// Verdadeiro se o resumo julga a mensagem por conta própria. Dizer que a mensagem "simplifica" ou "exagera" é opinião do bot
    /// e soa como tomar partido; o resumo deve mostrar o fato que falta e deixar o leitor concluir.
    /// </summary>
    public static bool JulgaAMensagem(string resumo) => JulgamentoDaMensagem().IsMatch(resumo);

    [GeneratedRegex(@"[.!?\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex Frases();

    private static readonly string[] Atribuicoes = ["segundo", "de acordo com", "conforme", "classificou", "classificação", "para a agência", "para o "];

    /// <summary>Palavras comuns do inglês e alfabetos que não são o nosso: sinal de que o modelo misturou idiomas, soltou lixo ou vazou o próprio raciocínio ("Need ...", "Wait ...").
    /// Só entram palavras que não existem em português nem aparecem nas matérias coletadas (fora "influencers", "drones" e "will", que aparecem).</summary>
    [GeneratedRegex(@"\b(the|of|and|according|having|statements?|agencies|context|completion|that|with|were|not|need|wait|must|should|accidentally|allegation|sentences?|publish(?:es|ed)?|says?|said|reports?|reported|claims?|claimed|propos(?:es|ed)|defend(?:s|ed)|about|from|which|who|also|have|has|been|but|into|their|they|this|these|those|when|while|would|could|because|after|before|between|during|greater|embassy|government|minister|ministry|candidate|election)\b|[\u0400-\u04FF\u0590-\u05FF\u0600-\u06FF\u3040-\u30FF\u3400-\u9FFF\uAC00-\uD7AF]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForaDoPortugues();

    public static bool TemTextoForaDoPortugues(string texto) => ForaDoPortugues().IsMatch(texto);

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

    /// <summary>
    /// Verdadeiro se alguma frase conclui algo da falta de notícia nas fontes ("não há confirmação de que...") sem atribuir a quem disse.
    /// A ausência de uma notícia nas fontes encontradas não prova nada; se uma fonte afirma isso, a frase cita a fonte.
    /// </summary>
    /// Com checagem entre as fontes, a frase não é barrada: a agência apurou e muitas vezes diz justamente que algo não existe
    /// ("a Folha não publicou essa matéria"), e repetir isso não é concluir pela falta de notícia.
    public static bool TemConclusaoPorAusencia(string resumo, IReadOnlyList<Fonte> fontes) =>
        !fontes.OfType<Checagem>().Any() && Frases().Split(resumo)
            .Where(frase => ConclusaoPorAusencia().IsMatch(frase))
            .Any(frase => !Atribuicoes.Any(a => frase.Contains(a, StringComparison.OrdinalIgnoreCase))
                && !fontes.Any(f => frase.Contains(f.Veiculo, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Verdadeiro se o resumo cita algum link que não veio das fontes (possível invenção da IA).</summary>
    public static bool CitaLinkForaDasFontes(string resumo, IReadOnlyList<Fonte> fontes) =>
        Links().Matches(resumo).Any(m => !fontes.Any(f =>
            string.Equals(m.Value.TrimEnd('.', ',', ';'), f.Link.AbsoluteUri, StringComparison.OrdinalIgnoreCase)));
}
