using AssistenteDesinformacao.Application.Pesquisa;

namespace AssistenteDesinformacao.Infrastructure.Pesquisa;

/// <summary>
/// Manchetes do Google Notícias para o texto como veio (com erro de digitação, sigla minúscula e tudo): a busca do Google entende
/// isso melhor do que qualquer regra nossa. Não resolve links nem baixa matérias, então custa uma chamada só.
/// </summary>
public sealed class ManchetesNoGoogleNoticias(HttpClient http, int janelaEmDias = 90) : IManchetesDaBusca
{
    public const int Maximo = 8;
    public const int MaximoDeCaracteresDaConsulta = 200;

    public async Task<IReadOnlyList<string>> BuscarAsync(string texto, CancellationToken ct)
    {
        var consulta = texto.ReplaceLineEndings(" ").Trim();
        if (consulta.Length == 0)
            return [];
        if (consulta.Length > MaximoDeCaracteresDaConsulta)
            consulta = consulta[..MaximoDeCaracteresDaConsulta];

        await using var fluxo = await http.GetStreamAsync(BuscadorNoGoogleNoticias.EnderecoDaBusca(consulta, janelaEmDias), ct);
        return BuscadorNoGoogleNoticias.Ler(fluxo)
            .Take(Maximo)
            .Select(f => $"{f.Titulo} ({f.Veiculo}{(f.DataPublicacao is { } d ? ", " + d.ToString("dd/MM/yyyy") : "")})")
            .ToList();
    }
}
