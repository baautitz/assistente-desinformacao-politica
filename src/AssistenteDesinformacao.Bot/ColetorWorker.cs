using AssistenteDesinformacao.Application.Coleta;

namespace AssistenteDesinformacao.Bot;

/// <summary>Coleta os feeds RSS periodicamente, no mesmo processo do bot.</summary>
public class ColetorWorker(IServiceScopeFactory escopos, OpcoesDeColeta opcoes, ILogger<ColetorWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromMinutes(opcoes.IntervaloEmMinutos));

        do
        {
            try
            {
                using var escopo = escopos.CreateScope();
                var coleta = escopo.ServiceProvider.GetRequiredService<ColetarFeeds>();
                var resultado = await coleta.ExecutarAsync(stoppingToken);

                logger.LogInformation("Coleta concluída: {Gravadas} matérias novas", resultado.Gravadas);
                foreach (var veiculo in resultado.FeedsComFalha)
                    logger.LogWarning("Falha ao coletar o feed de {Veiculo}", veiculo);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                logger.LogError(erro, "Falha na rodada de coleta");
            }
        } while (await temporizador.WaitForNextTickAsync(stoppingToken));
    }
}
