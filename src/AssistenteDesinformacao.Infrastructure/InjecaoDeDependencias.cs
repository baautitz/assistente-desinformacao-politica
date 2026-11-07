using System.ClientModel;
using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Limites;
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

        if (config["IA_MODELO_VISAO"] is { Length: > 0 } modeloDeVisao)
        {
            services.AddSingleton<IExtratorDeAlegacoesDeImagem>(_ =>
                new ExtratorDeAlegacoesDeImagemComIa(
                    new OpenAIClient(new ApiKeyCredential(ia.Chave), new OpenAIClientOptions { Endpoint = ia.UrlBase })
                        .GetChatClient(modeloDeVisao)
                        .AsIChatClient()));
        }
        else
        {
            services.AddSingleton<IExtratorDeAlegacoesDeImagem, ExtratorDeAlegacoesDeImagemSemVisao>();
        }

        var factCheck = new OpcoesDaFactCheck(
            Obrigatorio(config, "GOOGLE_FACT_CHECK_API_KEY"),
            config["FACT_CHECK_IDIOMA"] is { Length: > 0 } idioma ? idioma : "pt-BR");
        services.AddSingleton(factCheck);
        services.AddHttpClient<IBuscadorDeFontes, BuscadorNaFactCheck>(http =>
        {
            http.BaseAddress = BuscadorNaFactCheck.Endereco;
            http.DefaultRequestHeaders.Add(BuscadorNaFactCheck.CabecalhoDaChave, factCheck.Chave);
        });

        services.AddSingleton<IExtratorDeAlegacoes, ExtratorDeAlegacoesComIa>();
        services.AddSingleton<IRedatorDeSintese, RedatorDeSinteseComIa>();
        services.AddTransient<ResponderConsulta>();

        var limite = config["LIMITE_CONSULTAS_POR_MINUTO"] is { Length: > 0 } texto
            ? int.TryParse(texto, out var n) && n > 0
                ? n
                : throw new InvalidOperationException("LIMITE_CONSULTAS_POR_MINUTO deve ser um inteiro maior que zero.")
            : LimitadorDeConsultas.PadraoPorMinuto;
        services.AddSingleton(LimitadorDeConsultas.PorMinuto(limite));

        return services;
    }

    private static string Obrigatorio(IConfiguration config, string chave) =>
        config[chave] is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException($"Defina {chave} (veja .env.example).");
}
