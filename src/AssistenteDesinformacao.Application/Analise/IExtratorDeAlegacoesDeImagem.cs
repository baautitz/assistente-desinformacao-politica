using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Mensageria;

namespace AssistenteDesinformacao.Application.Analise;

/// <summary>Lê um print com modelo de visão e extrai as alegações. Sem modelo configurado, <see cref="Disponivel"/> é falso.</summary>
public interface IExtratorDeAlegacoesDeImagem
{
    bool Disponivel { get; }

    Task<IReadOnlyList<Alegacao>> ExtrairAsync(Imagem imagem, CancellationToken ct);
}
