namespace AssistenteDesinformacao.Application.Mensageria;

public static class MensagensDoBot
{
    public const string Inicio =
        """
        Olá! Me mande um texto político que você recebeu e eu procuro o que agências de checagem já publicaram sobre ele.

        O que eu faço:
        • Resumo em poucas linhas o que as fontes dizem.
        • Mostro o link e a data de cada fonte.
        • Quando uma agência classificou a afirmação, digo qual agência e qual foi a classificação.

        O que eu não faço:
        • Não digo por conta própria se algo é verdadeiro ou falso.
        • Não recomendo candidato, partido ou voto.
        • Não guardo suas mensagens.
        • Se não encontrar fonte, aviso que não encontrei.

        Por enquanto eu leio só texto.
        """;

    public const string SemApuracao = "Não encontrei apuração publicada sobre isso.";

    public const string ResumoPadrao = "Encontrei estas apurações sobre o assunto:";

    public const string Erro = "Não consegui responder agora. Tente de novo em alguns minutos.";
}
