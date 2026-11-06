using AssistenteDesinformacao.Bot;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

var token = builder.Configuration["TELEGRAM_BOT_TOKEN"]
    ?? throw new InvalidOperationException("Defina TELEGRAM_BOT_TOKEN (veja .env.example).");

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(token));
builder.Services.AddHostedService<Worker>();

builder.Build().Run();
