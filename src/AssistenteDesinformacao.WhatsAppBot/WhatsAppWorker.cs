using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using AssistenteDesinformacao.Application.Feedbacks;
using AssistenteDesinformacao.Application.Limites;
using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Application.Metricas;
using AssistenteDesinformacao.Domain.Mensageria;

namespace AssistenteDesinformacao.WhatsAppBot;

/// <summary>
/// Recebe as mensagens que o webhook enfileirou e responde pela Evolution API. Mesmo caso de uso do bot do Telegram
/// (<see cref="ResponderConsulta"/>); muda só a ponta do canal.
/// </summary>
public sealed class WhatsAppWorker(
    ClienteEvolution evolution,
    FeedbackPorNumero feedback,
    IServiceScopeFactory escopos,
    LimitadorDeConsultas limitador,
    ILogger<WhatsAppWorker> logger) : BackgroundService
{
    private static readonly TimeSpan LimiteDaConsulta = TimeSpan.FromSeconds(240);
    private static readonly TimeSpan PassoDoDigitando = TimeSpan.FromSeconds(4);
    private const int ConsultasEmParalelo = 4;
    private const int LimiteParaLimpezaDeIds = 2000;

    private static readonly HashSet<string> Saudacoes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/start", "start", "oi", "olá", "ola", "ajuda", "/ajuda", "menu", "help", "bom dia", "boa tarde", "boa noite",
    };

    private readonly Channel<MensagemRecebida> _fila = Channel.CreateBounded<MensagemRecebida>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly ConcurrentDictionary<string, DateTimeOffset> _idsVistos = new();

    /// <summary>Enfileira a mensagem. Devolve false se já foi vista (a Evolution reenvia webhooks) ou se a fila está cheia.</summary>
    public bool Enfileirar(MensagemRecebida mensagem)
    {
        if (_idsVistos.Count >= LimiteParaLimpezaDeIds)
            LimparIds();
        return _idsVistos.TryAdd(mensagem.Id, DateTimeOffset.UtcNow) && _fila.Writer.TryWrite(mensagem);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Bot do WhatsApp iniciado (webhook da Evolution API)");
        using var vagas = new SemaphoreSlim(ConsultasEmParalelo);

        await foreach (var mensagem in _fila.Reader.ReadAllAsync(stoppingToken))
        {
            await vagas.WaitAsync(stoppingToken);
            _ = Task.Run(async () =>
            {
                try
                {
                    await Processar(mensagem, stoppingToken);
                }
                catch (Exception erro) when (erro is not OperationCanceledException)
                {
                    // Nunca logar o texto: mensagens de usuário não são guardadas.
                    logger.LogError(erro, "Falha ao tratar mensagem do WhatsApp");
                }
                finally
                {
                    vagas.Release();
                }
            }, stoppingToken);
        }
    }

    private async Task Processar(MensagemRecebida mensagem, CancellationToken ct)
    {
        var texto = mensagem.Texto.Trim();

        if (feedback.TentarVotar(mensagem.Chat, texto, out var respostaId, out var util))
        {
            await RegistrarVoto(mensagem.Chat, respostaId, util, ct);
            return;
        }

        // Reação (qualquer emoji) e "1"/"2" sem resposta esperando não são consulta.
        if (mensagem.EhReacao || FeedbackPorNumero.EhVoto(texto))
            return;

        if (!mensagem.TemImagem && Saudacoes.Contains(texto))
        {
            logger.LogInformation("Pedido de ajuda/início");
            await evolution.EnviarTextoAsync(mensagem.Chat, MensagensDoBot.Inicio, ct);
            return;
        }

        if (texto.Length == 0 && !mensagem.TemImagem)
            return;

        if (!limitador.TentarRegistrar(IdDoUsuario(mensagem.Chat)))
        {
            logger.LogInformation("Consulta recusada: limite de consultas por minuto atingido");
            await evolution.EnviarTextoAsync(mensagem.Chat, MensagensDoBot.LimiteAtingido, ct);
            return;
        }

        var (resposta, paraAvaliar) = await Gerar(mensagem, ct);
        await evolution.EnviarTextoAsync(mensagem.Chat, resposta, ct);
        logger.LogInformation("Resposta enviada");

        if (paraAvaliar is { } id)
            await PedirAvaliacao(mensagem.Chat, id, ct);
    }

    // Avaliação por texto: a pessoa responde 1 (útil) ou 2 (não ajudou).
    private async Task PedirAvaliacao(string chat, Guid respostaId, CancellationToken ct)
    {
        feedback.Lembrar(chat, respostaId);
        await evolution.EnviarTextoAsync(chat, FeedbackPorNumero.Convite, ct);
    }

    // O voto grava só id da resposta, voto e data: nada do usuário nem da mensagem é guardado.
    private async Task RegistrarVoto(string chat, Guid respostaId, bool util, CancellationToken ct)
    {
        try
        {
            using var escopo = escopos.CreateScope();
            var registrar = escopo.ServiceProvider.GetRequiredService<RegistrarFeedback>();
            if (!await registrar.ExecutarAsync(DadosDoBotao.Gerar(respostaId, util), ct))
                return;

            logger.LogInformation("Feedback registrado: {Voto}", util ? "👍 útil" : "👎 não ajudou");
            await evolution.EnviarTextoAsync(chat, MensagensDoBot.FeedbackRegistrado, ct);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogError(erro, "Falha ao registrar feedback");
        }
    }

    private async Task<(string Texto, Guid? RespostaId)> Gerar(MensagemRecebida mensagem, CancellationToken ct)
    {
        // Enquanto a consulta roda o chat mostra "digitando...".
        using var terminou = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var digitando = Digitando(mensagem.Chat, terminou.Token);

        try
        {
            var cronometro = Stopwatch.StartNew();
            Consulta consulta;
            if (mensagem.ChaveEMensagem is { } chaveEMensagem)
            {
                var imagem = await evolution.BaixarImagemAsync(chaveEMensagem, ct);
                if (imagem is null)
                    return (MensagensDoBot.Erro, null);
                if (imagem.Conteudo.Length > ResponderConsulta.MaximoDeBytesDaImagem)
                    return (MensagensDoBot.ImagemGrande, null);
                consulta = Consulta.DePrint(imagem);
            }
            else
            {
                consulta = new Consulta(mensagem.Texto);
            }

            // Só o tipo da mensagem vai para o log: nunca o texto nem quem enviou.
            logger.LogInformation("Consulta recebida ({Tipo})", mensagem.TemImagem ? "print" : "texto");

            using var escopo = escopos.CreateScope();
            var casoDeUso = escopo.ServiceProvider.GetRequiredService<ResponderConsulta>();
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(LimiteDaConsulta);
            var sintese = await casoDeUso.ExecutarAsync(consulta, limite.Token);

            logger.LogInformation("Consulta respondida em {Segundos:F1}s ({Fontes} fontes)", cronometro.Elapsed.TotalSeconds, sintese.Fontes.Count);
            await RegistrarTempo(escopo, cronometro.Elapsed, ct);

            var texto = FormatadorDeResposta.Formatar(sintese);
            // Só uma Síntese real (com fontes) pede avaliação; avisos e recusas não.
            return (texto, sintese.EncontrouFontes ? Guid.NewGuid() : null);
        }
        catch (Exception erro) when (erro is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(erro, "Falha ao responder consulta");
            return (MensagensDoBot.Erro, null);
        }
        finally
        {
            await terminou.CancelAsync();
            await digitando;
        }
    }

    private async Task Digitando(string chat, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
                await evolution.DigitandoAsync(chat, PassoDoDigitando, ct);
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Resposta pronta (ou cancelamento): o indicador simplesmente para.
        }
        catch (Exception erro)
        {
            // Falha no indicador não pode atrapalhar a resposta.
            logger.LogWarning(erro, "Não foi possível mostrar \"digitando...\" no WhatsApp");
        }
    }

    // Só a duração é guardada; falha ao registrar a métrica não pode impedir a resposta.
    private async Task RegistrarTempo(IServiceScope escopo, TimeSpan duracao, CancellationToken ct)
    {
        try
        {
            await escopo.ServiceProvider.GetRequiredService<RegistrarTempoDeResposta>().ExecutarAsync(duracao, ct);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogError(erro, "Falha ao registrar o tempo de resposta");
        }
    }

    /// <summary>O limitador usa número como chave; o JID vira um número estável (hash), sem guardar o telefone.</summary>
    public static long IdDoUsuario(string chat) =>
        BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(chat)), 0);

    private void LimparIds()
    {
        var corte = DateTimeOffset.UtcNow - TimeSpan.FromHours(1);
        foreach (var (id, quando) in _idsVistos)
            if (quando < corte)
                _idsVistos.TryRemove(id, out _);
    }
}
