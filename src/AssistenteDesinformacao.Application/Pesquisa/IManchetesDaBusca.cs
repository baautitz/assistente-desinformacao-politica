namespace AssistenteDesinformacao.Application.Pesquisa;

/// <summary>
/// Busca rápida pelo texto como o usuário escreveu, só para ver as manchetes. É de onde a IA tira o contexto (de que assunto
/// se trata, como a imprensa o chama), em vez de adivinhar com o próprio conhecimento, que pode estar desatualizado.
/// </summary>
public interface IManchetesDaBusca
{
    /// <summary>Manchetes recentes no formato "Título (Veículo, dd/MM/aaaa)". Vazio se não achar nada.</summary>
    Task<IReadOnlyList<string>> BuscarAsync(string texto, CancellationToken ct);

    /// <summary>Texto que vai para a IA junto da mensagem ou da Alegação. Vazio quando não há manchetes.</summary>
    static string ParaContexto(IReadOnlyList<string> manchetes) =>
        manchetes.Count == 0
            ? ""
            : "\n\nManchetes recentes de uma busca por esse texto (só para entender de que assunto se trata e como a imprensa o chama; "
              + "não fazem parte da mensagem):\n" + string.Join('\n', manchetes.Select(m => "- " + m));
}
