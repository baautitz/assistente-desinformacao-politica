namespace AssistenteDesinformacao.Domain.Pesquisa;

/// <summary>Matéria jornalística ou checagem publicada, identificada por link e data de publicação.</summary>
public record Fonte(string Titulo, Uri Link, DateOnly? DataPublicacao, string Veiculo)
{
    /// <summary>Trecho do que a fonte diz (resumo do RSS ou afirmação checada). Só apoia o resumo e a triagem; nunca é exibido.</summary>
    public string? Trecho { get; init; }

    /// <summary>Ano da publicação; checagens costumam vir sem data, então usa o ano do link (/2024/11/14/).</summary>
    public int? Ano
    {
        get
        {
            if (DataPublicacao is { } data)
                return data.Year;
            var achado = System.Text.RegularExpressions.Regex.Match(Link.AbsolutePath, @"/(20\d\d)(?=[/-])");
            return achado.Success ? int.Parse(achado.Groups[1].Value) : null;
        }
    }

    /// <summary>
    /// Remove checagens com mais de um ano de diferença para a notícia mais recente: o assunto andou e a checagem velha
    /// (ex.: "a PEC ainda não foi votada") passaria uma situação que não vale mais.
    /// </summary>
    public static IReadOnlyList<Fonte> SemChecagensDefasadas(IReadOnlyList<Fonte> fontes)
    {
        var maisRecente = fontes.Where(f => f is not Checagem).Select(f => f.Ano).Max();
        if (maisRecente is null)
            return fontes;
        return fontes.Where(f => f is not Checagem || f.Ano is null || maisRecente - f.Ano <= 1).ToList();
    }
}
