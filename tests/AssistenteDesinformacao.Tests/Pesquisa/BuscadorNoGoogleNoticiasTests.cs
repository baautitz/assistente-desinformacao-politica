using System.Text;
using AssistenteDesinformacao.Infrastructure.Pesquisa;

namespace AssistenteDesinformacao.Tests.Pesquisa;

public class BuscadorNoGoogleNoticiasTests
{
    [Fact]
    public void Le_o_endereco_direto_da_resposta_do_google()
    {
        const string resposta = ")]}'\n\n[[\"wrb.fr\",\"Fbv4je\",\"[\\\"garturlres\\\",\\\"https://g1.globo.com/politica/noticia.ghtml\\\",1]\",null,null,null,\"generic\"],[\"di\",16]]";

        Assert.Equal(new Uri("https://g1.globo.com/politica/noticia.ghtml"), BuscadorNoGoogleNoticias.LerEnderecoDireto(resposta));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>bloqueado</html>")]
    [InlineData(")]}'\n\n[[\"di\",16]]")]
    [InlineData(")]}'\n\n[[\"wrb.fr\",\"Fbv4je\",null,null]]")]
    public void Resposta_estranha_nao_gera_endereco(string resposta) =>
        Assert.Null(BuscadorNoGoogleNoticias.LerEnderecoDireto(resposta));

    [Fact]
    public void Le_o_feed_tirando_o_veiculo_do_titulo()
    {
        const string xml = """
            <rss version="2.0"><channel>
              <item><title>Cury propõe drones - G1</title><link>https://news.google.com/rss/articles/abc</link>
                <pubDate>Sat, 29 Aug 2026 22:00:00 GMT</pubDate><source url="https://g1.globo.com">G1</source></item>
              <item><title>Sem veículo</title><link>https://news.google.com/rss/articles/def</link></item>
            </channel></rss>
            """;

        var fontes = BuscadorNoGoogleNoticias.Ler(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        var fonte = Assert.Single(fontes);
        Assert.Equal("Cury propõe drones", fonte.Titulo);
        Assert.Equal("G1", fonte.Veiculo);
        Assert.Equal(new DateOnly(2026, 8, 29), fonte.DataPublicacao);
    }
}
