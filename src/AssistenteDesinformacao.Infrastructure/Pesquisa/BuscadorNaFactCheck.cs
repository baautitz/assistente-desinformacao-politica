using System.Globalization;
using System.Net.Http.Json;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;

namespace AssistenteDesinformacao.Infrastructure.Pesquisa;

public sealed record OpcoesDaFactCheck(string Chave, string Idioma, int ResultadosPorBusca = 10);

/// <summary>Busca checagens na Google Fact Check Tools API (claims:search).</summary>
public sealed class BuscadorNaFactCheck(HttpClient http, OpcoesDaFactCheck opcoes, IGeradorDeTermosDeBusca? gerador = null) : IBuscadorDeFontes
{
    public static readonly Uri Endereco = new("https://factchecktools.googleapis.com/v1alpha1/");

    /// <summary>A chave vai no cabeçalho, não na URL, para não aparecer em logs nem em mensagens de erro.</summary>
    public const string CabecalhoDaChave = "X-Goog-Api-Key";

    public const int TermosDaIa = 2;

    /// <summary>
    /// A API devolve poucos resultados quando a consulta é uma frase específica demais (a mesma checagem aparece com "Lula 7 bilhões viagens"
    /// e some com "Lula gastou R$ 7 bilhões com viagens da primeira-dama"). Por isso busca a frase e também termos curtos (os da IA; sem ela,
    /// as palavras-chave), e junta sem repetir links.
    /// </summary>
    public async Task<IReadOnlyList<Fonte>> BuscarAsync(Alegacao alegacao, CancellationToken ct)
    {
        var consultas = new List<string> { alegacao.Texto };
        var termos = await TermosDaIaAsync(alegacao, ct);
        if (termos.Count > 0)
            consultas.AddRange(termos);
        else if (PalavrasChave(alegacao.Texto) is { Length: > 0 } palavrasChave)
            consultas.Add(palavrasChave);
        consultas = consultas.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var tarefas = consultas.Select(c => ConsultarAsync(c, ct)).ToList();
        try
        {
            await Task.WhenAll(tarefas);
        }
        catch when (tarefas.Any(t => t.IsCompletedSuccessfully))
        {
            // Uma das consultas falhou (ex.: 503 passageiro), mas a outra respondeu: segue com o que veio.
        }

        return tarefas.Where(t => t.IsCompletedSuccessfully)
            .SelectMany(t => t.Result)
            .DistinctBy(f => f.Link)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> TermosDaIaAsync(Alegacao alegacao, CancellationToken ct)
    {
        if (gerador is null)
            return [];
        try
        {
            return (await gerador.GerarAsync(alegacao, ct)).Take(TermosDaIa).ToList();
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return []; // sem os termos da IA, as palavras-chave ainda servem
        }
    }

    private async Task<IReadOnlyList<Fonte>> ConsultarAsync(string consulta, CancellationToken ct)
    {
        var url = "./claims:search" // "./" evita que "claims:" seja lido como esquema de URL
            + $"?query={Uri.EscapeDataString(consulta)}"
            + $"&languageCode={Uri.EscapeDataString(opcoes.Idioma)}"
            + $"&pageSize={opcoes.ResultadosPorBusca}";

        var resposta = await http.GetFromJsonAsync<RespostaDaBusca>(url, ct);

        return (resposta?.Claims ?? [])
            .SelectMany(item => (item.ClaimReview ?? []).Select(revisao => ParaChecagem(item, revisao)))
            .OfType<Fonte>()
            .ToList();
    }

    private static readonly HashSet<string> PalavrasSemValor = new(StringComparer.OrdinalIgnoreCase)
    {
        "que", "com", "para", "pelo", "pela", "pelos", "pelas", "uma", "uns", "umas", "como", "mais", "quase", "mesmo", "disse", "diz", "foi",
        "ser", "vai", "vão", "ficarão", "ficará", "gastou", "sobre", "entre", "depois", "antes", "este", "esta", "esse", "essa", "isso",
        "aprovada", "aprovado", "nacional", "presidente", "reais", "real", "seus", "suas", "dele", "dela", "tem", "têm", "não", "nos", "nas", "dos", "das",
        // Palavras curtas sem conteúdo. Sigla curta ("pec", "stf", "pt", "mp") é justamente o assunto, em maiúscula ou não, por isso não há corte por tamanho.
        "de", "da", "do", "em", "no", "na", "os", "as", "um", "ao", "aos", "à", "às", "se", "ou", "eu", "já", "há", "só", "é", "lá", "me", "te", "lhe",
        "por", "sem", "mas", "nem", "até", "seu", "sua", "meu", "ele", "ela", "eles", "elas", "são", "está", "estão", "era", "pra", "pro", "num", "numa",
        "aí", "ai", "sim", "ter", "vou", "tá", "né", "vc", "q",
    };

    /// <summary>
    /// Até 6 palavras que carregam o assunto (nomes, siglas, números, substantivos), sem artigos, preposições nem verbos comuns.
    /// Termos curtos como "6x1", "pec", "stf" e "pt" entram: quem decide o que sai é a lista de palavras sem valor, não o tamanho.
    /// </summary>
    public static string PalavrasChave(string texto) =>
        string.Join(' ', System.Text.RegularExpressions.Regex.Matches(texto, @"[\p{L}\p{N}]+")
            .Select(m => m.Value)
            .Where(p => (p.Length >= 2 || p.Any(char.IsDigit)) && !PalavrasSemValor.Contains(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6));

    /// <summary>Descarta revisões sem link válido ou sem classificação: sem isso não há o que mostrar.</summary>
    private static Checagem? ParaChecagem(AlegacaoDaApi item, RevisaoDaApi revisao)
    {
        if (!Uri.TryCreate(revisao.Url, UriKind.Absolute, out var link)
            || link.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(revisao.TextualRating))
            return null;

        var agencia = PrimeiroPreenchido(revisao.Publisher?.Name, revisao.Publisher?.Site) ?? link.Host;
        var titulo = PrimeiroPreenchido(revisao.Title, item.Text) ?? link.AbsoluteUri;
        DateOnly? data = DateTimeOffset.TryParse(revisao.ReviewDate, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out var quando)
            ? DateOnly.FromDateTime(quando.UtcDateTime)
            : null;

        return new Checagem(titulo, link, data, agencia, revisao.TextualRating.Trim()) { Trecho = item.Text };
    }

    private static string? PrimeiroPreenchido(params string?[] valores) =>
        valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private sealed record RespostaDaBusca(List<AlegacaoDaApi>? Claims);

    private sealed record AlegacaoDaApi(string? Text, List<RevisaoDaApi>? ClaimReview);

    private sealed record RevisaoDaApi(
        EditoraDaApi? Publisher, string? Url, string? Title, string? ReviewDate, string? TextualRating);

    private sealed record EditoraDaApi(string? Name, string? Site);
}
