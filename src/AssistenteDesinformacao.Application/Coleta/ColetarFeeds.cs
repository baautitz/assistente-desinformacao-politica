namespace AssistenteDesinformacao.Application.Coleta;

public sealed record OpcoesDeColeta(int JanelaEmDias = 90, int IntervaloEmMinutos = 60);

public sealed record ResultadoDaColeta(int Gravadas, IReadOnlyList<string> FeedsComFalha);

/// <summary>Lê todos os feeds, descarta matérias fora da janela e grava as novas. Falha em um feed não afeta os demais.</summary>
public sealed class ColetarFeeds(
    IFontesDeFeeds fontes,
    ILeitorDeFeed leitor,
    IRepositorioDeMaterias repositorio,
    OpcoesDeColeta opcoes,
    TimeProvider relogio)
{
    public async Task<ResultadoDaColeta> ExecutarAsync(CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var gravadas = 0;
        var falhas = new List<string>();

        foreach (var feed in fontes.Listar())
        {
            try
            {
                var materias = await leitor.LerAsync(feed, ct);
                var naJanela = materias.Where(m => m.EstaNaJanela(agora, opcoes.JanelaEmDias)).ToList();
                gravadas += await repositorio.AdicionarNovasAsync(naJanela, ct);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                falhas.Add(feed.Veiculo);
            }
        }

        return new ResultadoDaColeta(gravadas, falhas);
    }
}
