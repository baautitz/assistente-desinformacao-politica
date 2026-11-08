using AssistenteDesinformacao.Domain.Sinteses;
using AssistenteDesinformacao.Application.Feedbacks;
using AssistenteDesinformacao.Application.Limites;
using System.Diagnostics;
using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Application.Metricas;
using AssistenteDesinformacao.Domain.Mensageria;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace AssistenteDesinformacao.Bot;

public class Worker(ITelegramBotClient bot, IServiceScopeFactory escopos, LimitadorDeConsultas limitador, IConfiguration config, ILogger<Worker> logger) : BackgroundService
{
    private static readonly LinkPreviewOptions SemPrevia = new() { IsDisabled = true };

    // Opcional: sem ADMIN_TELEGRAM_ID válido, o comando /metricas não existe (é ignorado).
    private readonly long? _admin = long.TryParse(config["ADMIN_TELEGRAM_ID"], out var id) ? id : null;

    private static readonly TimeSpan LimiteDaConsulta = TimeSpan.FromSeconds(240);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await bot.GetMe(stoppingToken);
        logger.LogInformation("Bot @{usuario} iniciado (long polling)", me.Username);
        logger.LogInformation(_admin is null
            ? "ADMIN_TELEGRAM_ID não configurado: o comando /metricas fica desativado"
            : "Comando /metricas ativo para o administrador configurado");

        bot.StartReceiving(
            (cliente, update, ct) => update.CallbackQuery is { } clique
                ? Votar(cliente, clique, ct)
                : update.Message switch
            {
                { Text: { } texto } mensagem when ComandosDoBot.EhMetricas(texto) => EnviarMetricas(cliente, mensagem, ct),
                { Text: { } texto } mensagem => Responder(cliente, mensagem.Chat.Id, IdDoUsuario(mensagem), texto, null, ct),
                { Photo: { Length: > 0 } fotos } mensagem => Responder(cliente, mensagem.Chat.Id, IdDoUsuario(mensagem), null, fotos[^1], ct),
                _ => Task.CompletedTask,
            },
            (_, erro, _, _) =>
            {
                // Falha do lado do Telegram (5xx) ou de rede é passageira: o cliente tenta de novo sozinho, então basta uma linha.
                if (erro is Telegram.Bot.Exceptions.ApiRequestException { ErrorCode: >= 500 } api)
                    logger.LogWarning("Telegram indisponível ({Codigo}: {Motivo}); tentando de novo", api.ErrorCode, api.Message);
                else if (erro is HttpRequestException or TaskCanceledException)
                    logger.LogWarning("Sem conexão com o Telegram ({Motivo}); tentando de novo", erro.Message);
                else
                    logger.LogError(erro, "Erro no polling do Telegram");
                return Task.CompletedTask;
            },
            new ReceiverOptions { AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery] },
            stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static long IdDoUsuario(Message mensagem) => mensagem.From?.Id ?? mensagem.Chat.Id;


    // Só o admin configurado recebe resposta; para os demais o comando é ignorado. Só números agregados.
    private async Task EnviarMetricas(ITelegramBotClient cliente, Message mensagem, CancellationToken ct)
    {
        if (_admin is null)
        {
            logger.LogInformation("Comando /metricas ignorado: ADMIN_TELEGRAM_ID não está configurado");
            return;
        }

        // O ID de quem pediu nunca vai para o log: só se era ou não o administrador.
        if (IdDoUsuario(mensagem) != _admin)
        {
            logger.LogInformation("Comando /metricas ignorado: quem pediu não é o administrador");
            return;
        }

        await SemFalhar("Falha ao consultar as métricas", async () =>
        {
            using var escopo = escopos.CreateScope();
            var resumo = await escopo.ServiceProvider.GetRequiredService<ConsultarMetricas>().ExecutarAsync(ct);
            await cliente.SendMessage(mensagem.Chat.Id, MensagensDoBot.Metricas(resumo), cancellationToken: ct);
            logger.LogInformation("Métricas enviadas ao administrador: {Consultas} consultas, {Uteis} 👍, {Inuteis} 👎",
                resumo.Consultas, resumo.VotosUteis, resumo.VotosInuteis);
        });
    }

    // Falha em tarefa secundária (métrica, feedback) é registrada e nunca derruba o fluxo principal.
    private async Task SemFalhar(string descricao, Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogError(erro, "{Descricao}", descricao);
        }
    }

    private async Task Responder(ITelegramBotClient cliente, long chat, long usuario, string? texto, PhotoSize? foto, CancellationToken ct)
    {
        var (resposta, botoes, statusId) = await Gerar(cliente, chat, usuario, texto, foto, ct);
        await Entregar(cliente, chat, resposta, botoes, statusId, ct);
        logger.LogInformation("Resposta enviada ({Botoes})", botoes is null ? "sem pedido de feedback" : "com pedido de feedback");
    }

    // A resposta entra no lugar da mensagem de status; se não der para editar, vai como mensagem nova.
    private async Task Entregar(ITelegramBotClient cliente, long chat, string resposta, InlineKeyboardMarkup? botoes, int? statusId, CancellationToken ct)
    {
        if (statusId is { } id)
        {
            try
            {
                await cliente.EditMessageText(chat, id, resposta, linkPreviewOptions: SemPrevia, replyMarkup: botoes, cancellationToken: ct);
                return;
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                logger.LogWarning(erro, "Não foi possível editar a mensagem de status; enviando a resposta como mensagem nova");
                await SemFalhar("Falha ao apagar a mensagem de status", () => cliente.DeleteMessage(chat, id, ct));
            }
        }

        await cliente.SendMessage(chat, resposta, linkPreviewOptions: SemPrevia, replyMarkup: botoes, cancellationToken: ct);
    }

    // O voto grava só id da resposta, voto e data: nada do usuário nem da mensagem é guardado.
    private async Task Votar(ITelegramBotClient cliente, CallbackQuery clique, CancellationToken ct)
    {
        await SemFalhar("Falha ao registrar feedback", async () =>
        {
            using var escopo = escopos.CreateScope();
            var registrar = escopo.ServiceProvider.GetRequiredService<RegistrarFeedback>();
            if (!await registrar.ExecutarAsync(clique.Data, ct))
            {
                logger.LogWarning("Clique em botão ignorado: não é um feedback válido");
                return;
            }

            DadosDoBotao.TentarLer(clique.Data, out _, out var util);
            logger.LogInformation("Feedback registrado: {Voto}", util ? "👍 útil" : "👎 não ajudou");
            await cliente.AnswerCallbackQuery(clique.Id, MensagensDoBot.FeedbackRegistrado, cancellationToken: ct);
        });
    }

    private static InlineKeyboardMarkup Botoes()
    {
        var respostaId = Guid.NewGuid();
        return new InlineKeyboardMarkup(
        [
            [
                InlineKeyboardButton.WithCallbackData(DadosDoBotao.TextoUtil, DadosDoBotao.Gerar(respostaId, true)),
                InlineKeyboardButton.WithCallbackData(DadosDoBotao.TextoInutil, DadosDoBotao.Gerar(respostaId, false)),
            ],
        ]);
    }

    private async Task<(string Texto, InlineKeyboardMarkup? Botoes, int? StatusId)> Gerar(ITelegramBotClient cliente, long chat, long usuario, string? texto, PhotoSize? foto, CancellationToken ct)
    {
        if (texto?.Trim().StartsWith("/start", StringComparison.OrdinalIgnoreCase) == true)
        {
            logger.LogInformation("Comando /start");
            return (MensagensDoBot.Inicio, null, null);
        }

        if (!limitador.TentarRegistrar(usuario))
        {
            logger.LogInformation("Consulta recusada: limite de consultas por minuto atingido");
            return (MensagensDoBot.LimiteAtingido, null, null);
        }

        if (foto is { FileSize: > ResponderConsulta.MaximoDeBytesDaImagem })
            return (MensagensDoBot.ImagemGrande, null, null);

        // Mensagem de status: o usuário vê o andamento em vez de um silêncio de vários segundos.
        var statusId = await EnviarStatus(cliente, chat, ct);
        var andamento = new Andamento();
        using var terminou = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var acompanhamento = Acompanhar(cliente, chat, statusId, andamento, terminou.Token);

        try
        {
            var cronometro = Stopwatch.StartNew();
            using var escopo = escopos.CreateScope();
            var casoDeUso = escopo.ServiceProvider.GetRequiredService<ResponderConsulta>();
            // Só o tipo da mensagem vai para o log: nunca o texto nem quem enviou.
            logger.LogInformation("Consulta recebida ({Tipo})", foto is null ? "texto" : "print");
            var consulta = foto is null ? new Consulta(texto!) : Consulta.DePrint(await Baixar(cliente, foto, ct));

            // Nenhuma consulta fica pendurada para sempre: passou do limite, o usuário recebe o aviso de erro.
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(LimiteDaConsulta);
            var sintese = await casoDeUso.ExecutarAsync(consulta, limite.Token, andamento);

            logger.LogInformation("Consulta respondida em {Segundos:F1}s ({Fontes} fontes)", cronometro.Elapsed.TotalSeconds, sintese.Fontes.Count);
            await RegistrarTempo(escopo, cronometro.Elapsed, ct);
            // Só uma Síntese real (com fontes) pede avaliação; avisos e recusas não.
            return (FormatadorDeResposta.Formatar(sintese), sintese.EncontrouFontes ? Botoes() : null, statusId);
        }
        catch (Exception erro) when (erro is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Nunca logar o texto da consulta: mensagens de usuário não são guardadas.
            logger.LogError(erro, "Falha ao responder consulta");
            return (MensagensDoBot.Erro, null, statusId);
        }
        finally
        {
            await terminou.CancelAsync();
            await acompanhamento;
        }
    }

    private async Task<int?> EnviarStatus(ITelegramBotClient cliente, long chat, CancellationToken ct)
    {
        try
        {
            return (await cliente.SendMessage(chat, MensagensDoBot.Etapa(EtapaDaConsulta.Lendo), cancellationToken: ct)).Id;
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogWarning(erro, "Não foi possível enviar a mensagem de status");
            return null;
        }
    }

    /// <summary>Guarda a etapa mais recente; quem edita a mensagem é o <see cref="Acompanhar"/>, um passo de cada vez.</summary>
    private sealed class Andamento : IProgress<EtapaDaConsulta>
    {
        private int _etapa = -1;

        public EtapaDaConsulta? Atual => _etapa < 0 ? null : (EtapaDaConsulta)_etapa;

        public void Report(EtapaDaConsulta etapa) => Volatile.Write(ref _etapa, (int)etapa);
    }

    // Enquanto a consulta roda: renova o "digitando..." (some em ~5s) e atualiza a mensagem de status quando a etapa muda.
    private async Task Acompanhar(ITelegramBotClient cliente, long chat, int? statusId, Andamento andamento, CancellationToken ct)
    {
        var mostrada = EtapaDaConsulta.Lendo;
        var passo = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (passo++ % 3 == 0)
                    await cliente.SendChatAction(chat, ChatAction.Typing, cancellationToken: ct);

                if (statusId is { } id && andamento.Atual is { } etapa && etapa != mostrada)
                {
                    await cliente.EditMessageText(chat, id, MensagensDoBot.Etapa(etapa), cancellationToken: ct);
                    mostrada = etapa;
                    logger.LogInformation("Etapa: {Etapa}", etapa);
                }

                await Task.Delay(TimeSpan.FromSeconds(1.5), ct);
            }
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Resposta pronta (ou cancelamento): o acompanhamento simplesmente para.
        }
        catch (Exception erro)
        {
            // Falha no indicador ou no status não pode atrapalhar a resposta.
            logger.LogWarning(erro, "Não foi possível atualizar o andamento no Telegram");
        }
    }

    // Só a duração é guardada; falha ao registrar a métrica não pode impedir a resposta.
    private async Task RegistrarTempo(IServiceScope escopo, TimeSpan duracao, CancellationToken ct)
    {
        await SemFalhar("Falha ao registrar o tempo de resposta", async () =>
        {
            await escopo.ServiceProvider.GetRequiredService<RegistrarTempoDeResposta>().ExecutarAsync(duracao, ct);
            logger.LogInformation("Consulta respondida em {Milissegundos} ms", (long)duracao.TotalMilliseconds);
        });
    }

    // Fotos enviadas pelo Telegram são sempre JPEG. A imagem fica só em memória.
    private static async Task<Imagem> Baixar(ITelegramBotClient cliente, PhotoSize foto, CancellationToken ct)
    {
        var arquivo = await cliente.GetFile(foto.FileId, ct);
        using var memoria = new MemoryStream();
        await cliente.DownloadFile(arquivo.FilePath!, memoria, ct);
        return new Imagem(memoria.ToArray(), "image/jpeg");
    }
}
