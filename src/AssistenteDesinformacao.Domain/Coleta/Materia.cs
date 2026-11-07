namespace AssistenteDesinformacao.Domain.Coleta;

/// <summary>Matéria coletada de um feed RSS. O link é a identidade: o mesmo link nunca é guardado duas vezes.</summary>
public sealed record Materia(string Titulo, string Resumo, Uri Link, DateTimeOffset DataPublicacao, string Veiculo)
{
    /// <summary>Dentro da janela quando publicada há no máximo <paramref name="janelaEmDias"/> dias.</summary>
    public bool EstaNaJanela(DateTimeOffset agora, int janelaEmDias) =>
        DataPublicacao >= agora.AddDays(-janelaEmDias);
}
