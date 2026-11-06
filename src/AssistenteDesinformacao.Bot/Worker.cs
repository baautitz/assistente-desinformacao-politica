using AssistenteDesinformacao.Application.Mensageria;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace AssistenteDesinformacao.Bot;

public class Worker(ITelegramBotClient bot, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await bot.GetMe(stoppingToken);
        logger.LogInformation("Bot @{usuario} iniciado (long polling)", me.Username);

        bot.StartReceiving(
            async (cliente, update, ct) =>
            {
                var resposta = Eco.Responder(update.Message?.Text);
                if (resposta is not null)
                    await cliente.SendMessage(update.Message!.Chat.Id, resposta, cancellationToken: ct);
            },
            (_, erro, _, _) =>
            {
                logger.LogError(erro, "Erro no polling do Telegram");
                return Task.CompletedTask;
            },
            new ReceiverOptions { AllowedUpdates = [Telegram.Bot.Types.Enums.UpdateType.Message] },
            stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
