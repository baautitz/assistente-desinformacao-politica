using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AssistenteDesinformacao.Infrastructure.Ia;

namespace AssistenteDesinformacao.Tests.Ia;

public class InjetorDeCamposTests
{
    [Fact]
    public async Task Acrescenta_os_campos_ao_json_sem_perder_os_originais()
    {
        var servidor = new Servidor();
        var campos = InjetorDeCamposNaRequisicao.Ler("""{"thinking":{"type":"disabled"}}""")!;
        using var http = new HttpClient(new InjetorDeCamposNaRequisicao(campos, servidor));

        await http.PostAsync("https://ia.example/chat", new StringContent("""{"model":"m","temperature":0}""", Encoding.UTF8, "application/json"));

        var corpo = JsonNode.Parse(servidor.Corpo!)!;
        Assert.Equal("m", (string?)corpo["model"]);
        Assert.Equal(0, (int?)corpo["temperature"]);
        Assert.Equal("disabled", (string?)corpo["thinking"]!["type"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Vazio_nao_injeta_nada(string? json) => Assert.Null(InjetorDeCamposNaRequisicao.Ler(json));

    [Theory]
    [InlineData("{thinking:disabled}")]
    [InlineData("[1,2]")]
    public void Json_invalido_ou_que_nao_e_objeto_falha_na_subida_com_mensagem_clara(string json) =>
        Assert.Throws<InvalidOperationException>(() => InjetorDeCamposNaRequisicao.Ler(json));

    private sealed class Servidor : HttpMessageHandler
    {
        public string? Corpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage pedido, CancellationToken ct)
        {
            Corpo = await pedido.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
