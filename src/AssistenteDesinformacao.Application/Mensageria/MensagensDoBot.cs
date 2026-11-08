using System.Globalization;
using AssistenteDesinformacao.Application.Metricas;

namespace AssistenteDesinformacao.Application.Mensageria;

public static class MensagensDoBot
{
    public const string Inicio =
        """
        Olá! Me mande um texto ou um print político que você recebeu e eu procuro o que agências de checagem já publicaram sobre ele.

        O que eu faço:
        • Resumo em poucas linhas o que as fontes dizem.
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

    public const string SemAlegacao =
        "Não identifiquei uma afirmação que dê para conferir. Me mande o texto completo que você recebeu, com a afirmação sobre fatos (quem disse o quê, números, datas).";

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
