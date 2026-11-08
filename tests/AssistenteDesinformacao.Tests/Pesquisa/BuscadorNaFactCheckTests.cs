using System.Net;
using System.Text;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Infrastructure.Pesquisa;

namespace AssistenteDesinformacao.Tests.Pesquisa;

public class BuscadorNaFactCheckTests
{
    private const string Json = """
        {"claims":[{"text":"Urna fraudada","claimReview":[
          {"publisher":{"name":"Agência X","site":"x.test"},"url":"https://x.test/1","title":"É falso","textualRating":"Falso","reviewDate":"2026-09-01T00:00:00Z"},
          {"publisher":{"name":"Sem link"},"url":"claims:invalido","textualRating":"Falso"}]}]}
        """;

    [Fact]
    public async Task Chama_o_endereco_certo_com_a_chave_no_cabecalho_e_le_a_checagem()
    {
        var manipulador = new ManipuladorFalso(Json);
        var http = new HttpClient(manipulador) { BaseAddress = BuscadorNaFactCheck.Endereco };
        http.DefaultRequestHeaders.Add(BuscadorNaFactCheck.CabecalhoDaChave, "chave-secreta");

        var fontes = await new BuscadorNaFactCheck(http, new OpcoesDaFactCheck("chave-secreta", "pt-BR"))
            .BuscarAsync(new Alegacao("urna fraudada"), default);

        var pedido = Assert.IsType<HttpRequestMessage>(manipulador.Pedido);
        Assert.Equal("https://factchecktools.googleapis.com/v1alpha1/claims:search", pedido.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("query=urna%20fraudada", pedido.RequestUri.Query);
        Assert.DoesNotContain("chave-secreta", pedido.RequestUri.ToString());
        var checagem = Assert.IsType<Checagem>(Assert.Single(fontes));
        Assert.Equal("Agência X", checagem.Agencia);
        Assert.Equal("Falso", checagem.Classificacao);
    }

    private sealed class ManipuladorFalso(string json) : HttpMessageHandler
    {
        public HttpRequestMessage? Pedido { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            Pedido = pedido;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
