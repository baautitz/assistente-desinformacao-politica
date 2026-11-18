using System.Security.Cryptography;
using System.Text;
using AssistenteDesinformacao.Infrastructure;
using AssistenteDesinformacao.WhatsAppBot;

var builder = WebApplication.CreateBuilder(args);

string Obrigatoria(string nome) => builder.Configuration[nome] is { Length: > 0 } valor
    ? valor
    : throw new InvalidOperationException($"Defina {nome} (veja .env.example).");

var urlDaEvolution = Obrigatoria("EVOLUTION_URL");
var chaveDaEvolution = Obrigatoria("EVOLUTION_API_KEY");
var instancia = Obrigatoria("EVOLUTION_INSTANCIA");
var tokenDoWebhook = Obrigatoria("WHATSAPP_WEBHOOK_TOKEN");

builder.Services.AddHttpClient("evolution", http =>
{
    http.BaseAddress = new Uri(urlDaEvolution.TrimEnd('/') + "/");
    http.DefaultRequestHeaders.Add("apikey", chaveDaEvolution);
});
builder.Services.AddSingleton(sp =>
    new ClienteEvolution(sp.GetRequiredService<IHttpClientFactory>().CreateClient("evolution"), instancia));

builder.Services.AddSingleton<FeedbackPorEmoji>();
builder.Services.AddSingleton<WhatsAppWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WhatsAppWorker>());
builder.Services.AdicionarInfraestruturaDoBot(builder.Configuration);

var app = builder.Build();
await app.Services.MigrarBancoAsync();

app.MapGet("/saude", () => Results.Ok());

// A Evolution chama este endereço a cada mensagem. O token na URL impede que outra origem injete mensagens.
app.MapPost("/webhook", async (HttpRequest requisicao, WhatsAppWorker worker, ILogger<WhatsAppWorker> logger) =>
{
    var recebido = Encoding.UTF8.GetBytes(requisicao.Query["token"].ToString());
    if (!CryptographicOperations.FixedTimeEquals(recebido, Encoding.UTF8.GetBytes(tokenDoWebhook)))
        return Results.Unauthorized();

    using var leitor = new StreamReader(requisicao.Body);
    if (LeitorDeWebhook.Ler(await leitor.ReadToEndAsync()) is { } mensagem && !worker.Enfileirar(mensagem))
        logger.LogInformation("Mensagem repetida ou fila cheia: ignorada");

    // Responde rápido sempre: a consulta roda em segundo plano e a Evolution reenvia webhooks que demoram.
    return Results.Ok();
});

await app.RunAsync();
