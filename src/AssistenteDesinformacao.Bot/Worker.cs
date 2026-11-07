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

public class Worker(ITelegramBotClient bot, IServiceScopeFactory escopos, LimitadorDeConsultas limitador, ILogger<Worker> logger) : BackgroundService
{
    private static readonly LinkPreviewOptions SemPrevia = new() { IsDisabled = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await bot.GetMe(stoppingToken);
        logger.LogInformation("Bot @{usuario} iniciado (long polling)", me.Username);

        bot.StartReceiving(
            (cliente, update, ct) => update.CallbackQuery is { } clique
                ? Votar(cliente, clique, ct)
                : update.Message switch
            {
                { Text: { } texto } mensagem => Responder(cliente, mensagem.Chat.Id, mensagem.From?.Id ?? mensagem.Chat.Id, texto, null, ct),
                { Photo: { Length: > 0 } fotos } mensagem => Responder(cliente, mensagem.Chat.Id, mensagem.From?.Id ?? mensagem.Chat.Id, null, fotos[^1], ct),
                _ => Task.CompletedTask,
            },
            (_, erro, _, _) =>
            {
                logger.LogError(erro, "Erro no polling do Telegram");
                return Task.CompletedTask;
            },
            new ReceiverOptions { AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery] },
            stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task Responder(ITelegramBotClient cliente, long chat, long usuario, string? texto, PhotoSize? foto, CancellationToken ct)
    {
        var (resposta, botoes) = await Gerar(cliente, chat, usuario, texto, foto, ct);
        await cliente.SendMessage(chat, resposta, linkPreviewOptions: SemPrevia, replyMarkup: botoes, cancellationToken: ct);
    }

    // O voto grava só id da resposta, voto e data: nada do usuário nem da mensagem é guardado.
    private async Task Votar(ITelegramBotClient cliente, CallbackQuery clique, CancellationToken ct)
    {
        try
        {
            using var escopo = escopos.CreateScope();
            var registrar = escopo.ServiceProvider.GetRequiredService<RegistrarFeedback>();
            if (!await registrar.ExecutarAsync(clique.Data, ct))
                return;

            await cliente.AnswerCallbackQuery(clique.Id, MensagensDoBot.FeedbackRegistrado, cancellationToken: ct);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogError(erro, "Falha ao registrar feedback");
        }
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

    private async Task<(string Texto, InlineKeyboardMarkup? Botoes)> Gerar(ITelegramBotClient cliente, long chat, long usuario, string? texto, PhotoSize? foto, CancellationToken ct)
    {
        if (texto?.Trim().StartsWith("/start", StringComparison.OrdinalIgnoreCase) == true)
            return (MensagensDoBot.Inicio, null);

        if (!limitador.TentarRegistrar(usuario))
            return (MensagensDoBot.LimiteAtingido, null);

        try
        {
            if (foto is { FileSize: > ResponderConsulta.MaximoDeBytesDaImagem })
                return (MensagensDoBot.ImagemGrande, null);

            await cliente.SendChatAction(chat, ChatAction.Typing, cancellationToken: ct);

            var cronometro = Stopwatch.StartNew();
            using var escopo = escopos.CreateScope();
            var casoDeUso = escopo.ServiceProvider.GetRequiredService<ResponderConsulta>();
            var consulta = foto is null ? new Consulta(texto!) : Consulta.DePrint(await Baixar(cliente, foto, ct));
            var sintese = await casoDeUso.ExecutarAsync(consulta, ct);
            await RegistrarTempo(escopo, cronometro.Elapsed, ct);
            return (FormatadorDeResposta.Formatar(sintese), Botoes());
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // Nunca logar o texto da consulta: mensagens de usuário não são guardadas.
            logger.LogError(erro, "Falha ao responder consulta");
            return (MensagensDoBot.Erro, null);
        }
    }

    // Só a duração é guardada; falha ao registrar a métrica não pode impedir a resposta.
    private async Task RegistrarTempo(IServiceScope escopo, TimeSpan duracao, CancellationToken ct)
    {
        try
        {
            await escopo.ServiceProvider.GetRequiredService<RegistrarTempoDeResposta>().ExecutarAsync(duracao, ct);
            logger.LogInformation("Consulta respondida em {Milissegundos} ms", (long)duracao.TotalMilliseconds);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            logger.LogError(erro, "Falha ao registrar o tempo de resposta");
        }
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
