using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;

namespace AssistenteDesinformacao.Tests.Pesquisa;

public class GeradorDeTermosComCacheTests
{
    [Fact]
    public async Task Mesma_alegacao_chama_a_ia_uma_vez_so()
    {
        var ia = new GeradorContado();
        var cache = new GeradorDeTermosComCache(ia);

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => cache.GerarAsync(new Alegacao("Câmara aprova escala 6x1"), default)));

        Assert.Equal(1, ia.Chamadas);
    }

    [Fact]
    public async Task Falha_nao_fica_guardada()
    {
        var ia = new GeradorContado { FalharNaPrimeira = true };
        var cache = new GeradorDeTermosComCache(ia);

        await Assert.ThrowsAsync<HttpRequestException>(() => cache.GerarAsync(new Alegacao("x"), default));
        var termos = await cache.GerarAsync(new Alegacao("x"), default);

        Assert.Equal(["termo"], termos);
        Assert.Equal(2, ia.Chamadas);
    }

    private sealed class GeradorContado : IGeradorDeTermosDeBusca
    {
        private int _chamadas;
        public int Chamadas => _chamadas;
        public bool FalharNaPrimeira { get; init; }

        public async Task<IReadOnlyList<string>> GerarAsync(Alegacao alegacao, CancellationToken ct)
        {
            var n = Interlocked.Increment(ref _chamadas);
            await Task.Delay(20, ct);
            if (FalharNaPrimeira && n == 1)
                throw new HttpRequestException("sem rede");
            return ["termo"];
        }
    }
}
