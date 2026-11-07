using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Domain.Mensageria;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AssistenteDesinformacao.Bot;

public class Worker(ITelegramBotClient bot, IServiceScopeFactory escopos, ILogger<Worker> logger) : BackgroundService
{
    private static readonly LinkPreviewOptions SemPrevia = new() { IsDisabled = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await bot.GetMe(stoppingToken);
        logger.LogInformation("Bot @{usuario} iniciado (long polling)", me.Username);

        bot.StartReceiving(
            (cliente, update, ct) => update.Message switch
            {
                { Text: { } texto } mensagem => Responder(cliente, mensagem.Chat.Id, texto, null, ct),
                { Photo: { Length: > 0 } fotos } mensagem => Responder(cliente, mensagem.Chat.Id, null, fotos[^1], ct),
                _ => Task.CompletedTask,
            },
            (_, erro, _, _) =>
            {
                logger.LogError(erro, "Erro no polling do Telegram");
                return Task.CompletedTask;
            },
            new ReceiverOptions { AllowedUpdates = [UpdateType.Message] },
            stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task Responder(ITelegramBotClient cliente, long chat, string? texto, PhotoSize? foto, CancellationToken ct)
    {
        var resposta = await Gerar(cliente, chat, texto, foto, ct);
        await cliente.SendMessage(chat, resposta, linkPreviewOptions: SemPrevia, cancellationToken: ct);
    }

    private async Task<string> Gerar(ITelegramBotClient cliente, long chat, string? texto, PhotoSize? foto, CancellationToken ct)
    {
        if (texto?.Trim().StartsWith("/start", StringComparison.OrdinalIgnoreCase) == true)
            return MensagensDoBot.Inicio;

        try
        {
            if (foto is { FileSize: > ResponderConsulta.MaximoDeBytesDaImagem })
                return MensagensDoBot.ImagemGrande;

            await cliente.SendChatAction(chat, ChatAction.Typing, cancellationToken: ct);

            using var escopo = escopos.CreateScope();
            var casoDeUso = escopo.ServiceProvider.GetRequiredService<ResponderConsulta>();
            var consulta = foto is null ? new Consulta(texto!) : Consulta.DePrint(await Baixar(cliente, foto, ct));
            var sintese = await casoDeUso.ExecutarAsync(consulta, ct);
            return FormatadorDeResposta.Formatar(sintese);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // Nunca logar o texto da consulta: mensagens de usuário não são guardadas.
            logger.LogError(erro, "Falha ao responder consulta");
            return MensagensDoBot.Erro;
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
