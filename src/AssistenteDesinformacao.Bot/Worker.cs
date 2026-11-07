using AssistenteDesinformacao.Application.Limites;
using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Domain.Mensageria;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AssistenteDesinformacao.Bot;

public class Worker(ITelegramBotClient bot, IServiceScopeFactory escopos, LimitadorDeConsultas limitador, ILogger<Worker> logger) : BackgroundService
{
    private static readonly LinkPreviewOptions SemPrevia = new() { IsDisabled = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var me = await bot.GetMe(stoppingToken);
        logger.LogInformation("Bot @{usuario} iniciado (long polling)", me.Username);

        bot.StartReceiving(
            (cliente, update, ct) => update.Message is { Text: { } texto } mensagem
                ? Responder(cliente, mensagem.Chat.Id, mensagem.From?.Id ?? mensagem.Chat.Id, texto, ct)
                : Task.CompletedTask,
            (_, erro, _, _) =>
            {
                logger.LogError(erro, "Erro no polling do Telegram");
                return Task.CompletedTask;
            },
            new ReceiverOptions { AllowedUpdates = [UpdateType.Message] },
            stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task Responder(ITelegramBotClient cliente, long chat, long usuario, string texto, CancellationToken ct)
    {
        var resposta = await Gerar(cliente, chat, usuario, texto, ct);
        await cliente.SendMessage(chat, resposta, linkPreviewOptions: SemPrevia, cancellationToken: ct);
    }

    private async Task<string> Gerar(ITelegramBotClient cliente, long chat, long usuario, string texto, CancellationToken ct)
    {
        if (texto.Trim().StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            return MensagensDoBot.Inicio;

        if (!limitador.TentarRegistrar(usuario))
            return MensagensDoBot.LimiteAtingido;

        try
        {
            await cliente.SendChatAction(chat, ChatAction.Typing, cancellationToken: ct);

            using var escopo = escopos.CreateScope();
            var casoDeUso = escopo.ServiceProvider.GetRequiredService<ResponderConsulta>();
            var sintese = await casoDeUso.ExecutarAsync(new Consulta(texto), ct);
            return FormatadorDeResposta.Formatar(sintese);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // Nunca logar o texto da consulta: mensagens de usuário não são guardadas.
            logger.LogError(erro, "Falha ao responder consulta");
            return MensagensDoBot.Erro;
        }
    }
}
