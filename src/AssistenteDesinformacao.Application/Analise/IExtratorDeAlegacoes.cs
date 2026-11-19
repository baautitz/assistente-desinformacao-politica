using AssistenteDesinformacao.Domain.Analise;

namespace AssistenteDesinformacao.Application.Analise;

public interface IExtratorDeAlegacoes
{
    Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, CancellationToken ct);

    /// <summary>
    /// Extrai com <paramref name="manchetes"/> de uma busca pelo texto como contexto: é delas que a IA tira de que assunto a
    /// mensagem trata. Quem não usa o contexto só extrai do texto.
    /// </summary>
    Task<IReadOnlyList<Alegacao>> ExtrairAsync(string texto, IReadOnlyList<string> manchetes, CancellationToken ct) =>
        ExtrairAsync(texto, ct);
}
