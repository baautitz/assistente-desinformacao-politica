namespace AssistenteDesinformacao.Infrastructure.Pesquisa;

/// <summary>
/// Lista fechada de veículos que o Google Notícias pode trazer como Fonte. Fica de fora o que não é imprensa
/// ou agência de checagem (redes sociais, blogs, sites de boato): num bot que combate desinformação,
/// é melhor responder com menos fontes do que citar uma não confiável. Para incluir um veículo, adicione o domínio.
/// </summary>
public static class VeiculosConfiaveis
{
    private static readonly string[] Dominios =
    [
        // Grande imprensa
        "g1.globo.com", "oglobo.globo.com", "valor.globo.com", "folha.uol.com.br", "noticias.uol.com.br", "uol.com.br",
        "estadao.com.br", "cnnbrasil.com.br", "metropoles.com", "poder360.com.br", "bbc.com", "terra.com.br",
        "band.uol.com.br", "veja.abril.com.br", "correiobraziliense.com.br", "nexojornal.com.br", "cartacapital.com.br",
        "congressoemfoco.uol.com.br", "gazetadopovo.com.br", "infomoney.com.br", "r7.com", "istoe.com.br", "exame.com",
        // Agências de notícias e órgãos públicos
        "agenciabrasil.ebc.com.br", "camara.leg.br", "senado.leg.br", "gov.br", "tse.jus.br", "stf.jus.br",
        "reuters.com", "apnews.com", "afp.com",
        // Checagem
        "aosfatos.org", "lupa.news", "projetocomprova.com.br", "e-farsas.com", "boatos.org",
        "checamos.afp.com",
    ];

    public static bool Aceita(Uri endereco) =>
        Dominios.Any(d => endereco.Host == d || endereco.Host.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
}
