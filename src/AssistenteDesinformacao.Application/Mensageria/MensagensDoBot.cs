using System.Globalization;
using AssistenteDesinformacao.Application.Metricas;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Application.Mensageria;

public static class MensagensDoBot
{
    public const string Inicio =
        """
        Olá! Me mande um texto ou um print político que você recebeu e eu procuro o que agências de checagem e veículos jornalísticos já publicaram sobre ele.

        O que eu faço:
        • Conto em um parágrafo o que se sabe sobre o assunto, dos fatos mais recentes ao contexto.
        • Aponto o que ainda está pendente, em disputa ou o que a mensagem deixa de fora.
        • Mostro o link e a data de cada fonte.
        • Quando uma agência classificou a afirmação, digo qual agência e qual foi a classificação.

        O que eu não faço:
        • Não digo por conta própria se algo é verdadeiro ou falso.
        • Não recomendo candidato, partido ou voto.
        • Não guardo suas mensagens.
        • Se não encontrar fonte, aviso que não encontrei.
        """;

    public const string SemApuracao = "Não encontrei apuração publicada sobre isso.";

    public const string ResumoPadrao = "Encontrei estas apurações sobre o assunto:";

    /// <summary>
    /// Resumo montado só com os dados das checagens (agência, classificação, o que foi checado, data), para quando o texto da IA
    /// é recusado: sem IA, não há o que inventar, e o usuário recebe mais que uma lista de links.
    /// </summary>
    public static string ResumoDasChecagens(IReadOnlyList<Checagem> checagens)
    {
        var frases = checagens.Select(c =>
            $"A agência {c.Agencia} classificou como \"{c.Classificacao}\" a checagem \"{c.Titulo.TrimEnd('.', ' ')}\""
            + (c.DataPublicacao is { } data ? $", publicada em {data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}." : "."));
        var antiga = checagens.Any(c => c.DataPublicacao is { } d && d < DateOnly.FromDateTime(DateTime.Today).AddMonths(-6));
        return string.Join(' ', frases)
            + (antiga ? " Ponto de atenção: a checagem é de meses atrás; se o conteúdo voltou a circular agora, pode ser o mesmo boato reaproveitado." : "");
    }

    public const string SemAlegacao =
        "Não identifiquei uma afirmação que dê para conferir. Me mande o texto completo que você recebeu, com a afirmação sobre fatos (quem disse o quê, números, datas).";

    public const string NaoRecomendoVoto =
        "Não recomendo candidato, partido ou voto: essa escolha é sua. Posso ajudar a conferir uma notícia ou afirmação sobre política que você recebeu. É só me mandar o texto ou o print.";

    public const string NaoOpino =
        "Não dou opinião nem faço ranking de partidos, candidatos ou políticos: isso não dá para conferir em fontes. Posso ajudar a conferir uma notícia ou afirmação que você recebeu. É só me mandar o texto ou o print.";

    public const string ForaDoEscopo =
        "Eu confiro afirmações e notícias sobre política em fontes públicas. Me mande o texto ou o print do que você recebeu e eu busco o que já foi publicado sobre isso.";

    public const string SoLeTexto = "No momento eu só consigo ler texto. Digite ou cole o conteúdo do print aqui na conversa.";

    public const string ImagemGrande = "Essa imagem é grande demais para eu ler. Mande um print menor ou cole o texto aqui.";

    /// <summary>Texto do /metricas: só números agregados, nunca conteúdo de mensagens.</summary>
    public static string Metricas(ResumoDeMetricas resumo) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Consultas: {resumo.Consultas}\nVotos 👍: {resumo.VotosUteis}\nVotos 👎: {resumo.VotosInuteis}\nTempo médio de resposta: {(resumo.TempoMedioEmMilissegundos is { } ms ? $"{ms:0} ms" : "sem dados")}");

    /// <summary>Texto da mensagem de status que o bot edita enquanto trabalha.</summary>
    public static string Etapa(EtapaDaConsulta etapa) => etapa switch
    {
        EtapaDaConsulta.Lendo => "🔎 Lendo a sua mensagem...",
        EtapaDaConsulta.Buscando => "📚 Buscando o que agências e veículos publicaram...",
        EtapaDaConsulta.BuscandoDeNovo => "🔁 Nada relevante ainda. Buscando de outro jeito...",
        EtapaDaConsulta.Conferindo => "🧐 Conferindo quais fontes tratam do assunto...",
        EtapaDaConsulta.Escrevendo => "✍️ Escrevendo o resumo...",
        _ => "⏳ Trabalhando nisso...",
    };

    public const string FeedbackRegistrado = "Obrigado pelo feedback!";

    public const string Erro = "Não consegui responder agora. Tente de novo em alguns minutos.";

    public const string LimiteAtingido =
        "Você fez muitas consultas em pouco tempo. Aguarde cerca de um minuto e envie de novo.";
}
