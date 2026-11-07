using AssistenteDesinformacao.Bot;
using AssistenteDesinformacao.Infrastructure;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

var token = builder.Configuration["TELEGRAM_BOT_TOKEN"]
    ?? throw new InvalidOperationException("Defina TELEGRAM_BOT_TOKEN (veja .env.example).");

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(token));
builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<ColetorWorker>();

var app = builder.Build();
await app.Services.MigrarBancoAsync();
await app.RunAsync();
