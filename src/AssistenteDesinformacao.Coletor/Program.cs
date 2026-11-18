using AssistenteDesinformacao.Coletor;
using AssistenteDesinformacao.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AdicionarInfraestruturaDoColetor(builder.Configuration);
builder.Services.AddHostedService<ColetorWorker>();

var app = builder.Build();
await app.Services.MigrarBancoAsync();
await app.RunAsync();
