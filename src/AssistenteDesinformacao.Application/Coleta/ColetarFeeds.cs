using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Coleta;

namespace AssistenteDesinformacao.Application.Coleta;

public sealed record OpcoesDeColeta(int JanelaEmDias = 90, int IntervaloEmMinutos = 60);

public sealed record ResultadoDaColeta(int Gravadas, IReadOnlyList<string> FeedsComFalha);

/// <summary>Lê todos os feeds, descarta matérias fora da janela e grava as novas. Falha em um feed não afeta os demais.</summary>
public sealed class ColetarFeeds(
    IFontesDeFeeds fontes,
    ILeitorDeFeed leitor,
    IRepositorioDeMaterias repositorio,
    OpcoesDeColeta opcoes,
    TimeProvider relogio,
    IGeradorDeEmbeddings? embeddings = null)
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
                var novas = await repositorio.FiltrarNovasAsync(naJanela, ct);
                gravadas += await repositorio.AdicionarNovasAsync(await ComEmbeddingsAsync(novas, ct), ct);
            }
            catch (Exception erro) when (!ct.IsCancellationRequested)
            {
                falhas.Add($"{feed.Veiculo} ({erro.GetType().Name}: {erro.Message})");
            }
        }

        return new ResultadoDaColeta(gravadas, falhas);
    }

    /// <summary>Sem modelo configurado, ou se ele falhar, a matéria é gravada sem vetor e continua achável por texto.</summary>
    private async Task<IReadOnlyList<Materia>> ComEmbeddingsAsync(IReadOnlyList<Materia> materias, CancellationToken ct)
    {
        if (embeddings is null || materias.Count == 0)
            return materias;
        try
        {
            var vetores = await embeddings.GerarAsync(materias.Select(m => $"{m.Titulo}. {m.Resumo}").ToList(), ct);
            if (vetores.Count != materias.Count)
                return materias;
            return materias.Select((m, i) => m with { Embedding = vetores[i] }).ToList();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return materias;
        }
    }
}
