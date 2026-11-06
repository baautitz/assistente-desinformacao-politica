using System.ClientModel;
using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Infrastructure.Analise;
using AssistenteDesinformacao.Infrastructure.Ia;
using AssistenteDesinformacao.Infrastructure.Pesquisa;
using AssistenteDesinformacao.Infrastructure.Sinteses;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace AssistenteDesinformacao.Infrastructure;

public static class InjecaoDeDependencias
{
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, IConfiguration config)
    {
        var ia = new OpcoesDeIa(
            new Uri(Obrigatorio(config, "IA_URL_BASE")),
            Obrigatorio(config, "IA_CHAVE"),
            Obrigatorio(config, "IA_MODELO_TEXTO"));

        services.AddSingleton<IChatClient>(_ =>
            new OpenAIClient(new ApiKeyCredential(ia.Chave), new OpenAIClientOptions { Endpoint = ia.UrlBase })
                .GetChatClient(ia.ModeloDeTexto)
                .AsIChatClient());

        services.AddSingleton(new OpcoesDaFactCheck(
            Obrigatorio(config, "GOOGLE_FACT_CHECK_API_KEY"),
            config["FACT_CHECK_IDIOMA"] is { Length: > 0 } idioma ? idioma : "pt-BR"));
        services.AddHttpClient<IBuscadorDeFontes, BuscadorNaFactCheck>(http =>
            http.BaseAddress = BuscadorNaFactCheck.Endereco);

        services.AddSingleton<IExtratorDeAlegacoes, ExtratorDeAlegacoesComIa>();
        services.AddSingleton<IRedatorDeSintese, RedatorDeSinteseComIa>();
        services.AddTransient<ResponderConsulta>();

        return services;
    }

    private static string Obrigatorio(IConfiguration config, string chave) =>
        config[chave] is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException($"Defina {chave} (veja .env.example).");
}
