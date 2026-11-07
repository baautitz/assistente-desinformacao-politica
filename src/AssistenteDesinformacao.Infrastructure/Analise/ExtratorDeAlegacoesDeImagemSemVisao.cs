using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Mensageria;

namespace AssistenteDesinformacao.Infrastructure.Analise;

/// <summary>Usado quando não há modelo de visão configurado.</summary>
public sealed class ExtratorDeAlegacoesDeImagemSemVisao : IExtratorDeAlegacoesDeImagem
{
    public bool Disponivel => false;

    public Task<IReadOnlyList<Alegacao>> ExtrairAsync(Imagem imagem, CancellationToken ct) =>
        throw new InvalidOperationException("Nenhum modelo de visão configurado.");
}
