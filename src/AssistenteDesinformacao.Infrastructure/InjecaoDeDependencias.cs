using System.Net;
using System.ClientModel;
using System.ClientModel.Primitives;
using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Coleta;
using AssistenteDesinformacao.Application.Feedbacks;
using AssistenteDesinformacao.Application.Limites;
using AssistenteDesinformacao.Application.Mensageria;
using AssistenteDesinformacao.Application.Metricas;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Infrastructure.Analise;
using AssistenteDesinformacao.Infrastructure.Coleta;
using AssistenteDesinformacao.Infrastructure.Ia;
using AssistenteDesinformacao.Infrastructure.Persistencia;
using AssistenteDesinformacao.Infrastructure.Pesquisa;
using AssistenteDesinformacao.Infrastructure.Sinteses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        var camposExtras = InjetorDeCamposNaRequisicao.Ler(config["IA_CAMPOS_EXTRAS"]);
        OpenAIClient NovoClienteDeIa() => ClienteDeIa(ia, camposExtras);

        services.AddSingleton<IChatClient>(sp =>
            new ClienteComLog(
                NovoClienteDeIa()
                    .GetChatClient(ia.ModeloDeTexto)
                    .AsIChatClient()
                    .SemRaciocinio(),
                sp.GetRequiredService<ILogger<ClienteComLog>>()));

        if (config["IA_MODELO_VISAO"] is { Length: > 0 } modeloDeVisao)
        {
            services.AddSingleton<IExtratorDeAlegacoesDeImagem>(_ =>
                new ExtratorDeAlegacoesDeImagemComIa(
                    NovoClienteDeIa()
                        .GetChatClient(modeloDeVisao)
                        .AsIChatClient()
                        .SemRaciocinio()));
        }
        else
        {
            services.AddSingleton<IExtratorDeAlegacoesDeImagem, ExtratorDeAlegacoesDeImagemSemVisao>();
        }

        var factCheck = new OpcoesDaFactCheck(
            Obrigatorio(config, "GOOGLE_FACT_CHECK_API_KEY"),
            config["FACT_CHECK_IDIOMA"] is { Length: > 0 } idioma ? idioma : "pt-BR");
        services.AddSingleton(factCheck);
        services.AddHttpClient<BuscadorNaFactCheck>(http =>
        {
            http.BaseAddress = BuscadorNaFactCheck.Endereco;
            http.DefaultRequestHeaders.Add(BuscadorNaFactCheck.CabecalhoDaChave, factCheck.Chave);
        });
        services.AddSingleton<IGeradorDeTermosDeBusca, GeradorDeTermosComIa>();
        services.AddScoped<IBuscaDeMaterias, BuscaDeMateriasNoPostgres>();
        services.AddScoped<IBuscaVetorialDeMaterias, BuscaVetorialNoPostgres>();
        // Opcional: sem IA_MODELO_EMBEDDINGS não há IGeradorDeEmbeddings e a busca usa só texto.
        if (config["IA_MODELO_EMBEDDINGS"] is { Length: > 0 } modeloDeEmbeddings)
        {
            services.AddSingleton<IGeradorDeEmbeddings>(_ =>
                new GeradorDeEmbeddingsComIa(
                    ClienteDeIa(ia, null) // embeddings não recebem os campos extras do chat
                        .GetEmbeddingClient(modeloDeEmbeddings)
                        .AsIEmbeddingGenerator()));
        }

        services.AddScoped<IBuscadorDeFontes>(sp => new BuscadorCombinado(
            [
                sp.GetRequiredService<BuscadorNaFactCheck>(),
                new BuscadorDeMaterias(
                    sp.GetRequiredService<IGeradorDeTermosDeBusca>(), sp.GetRequiredService<IBuscaDeMaterias>(),
                    sp.GetService<IGeradorDeEmbeddings>(), sp.GetRequiredService<IBuscaVetorialDeMaterias>(),
                    sp.GetRequiredService<OpcoesDeColeta>().JanelaEmDias, sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<BuscadorDeMaterias>>()),
            ],
            sp.GetRequiredService<ILogger<BuscadorCombinado>>()));

        services.AddSingleton<IExtratorDeAlegacoes, ExtratorDeAlegacoesComIa>();
        // O resumo é a etapa que mais pede qualidade: com o raciocínio desligado, alguns modelos soltam palavras soltas e misturam idiomas.
        // Por isso o redator tem o próprio cliente, sem os campos extras das etapas rápidas, e mais tempo para responder.
        var camposDoRedator = InjetorDeCamposNaRequisicao.Ler(config["IA_CAMPOS_EXTRAS_REDATOR"]);
        services.AddSingleton<IRedatorDeSintese>(sp => new RedatorDeSinteseComIa(
            new ClienteComLog(
                ClienteDeIa(ia, camposDoRedator, TimeSpan.FromSeconds(150))
                    .GetChatClient(ia.ModeloDeTexto)
                    .AsIChatClient()
                    .SemRaciocinio(),
                sp.GetRequiredService<ILogger<ClienteComLog>>())));
        services.AddSingleton<IClassificadorDeNeutralidade, ClassificadorDeNeutralidadeComIa>();
        services.AddSingleton<IJuizDeRelevancia, JuizDeRelevanciaComIa>();
        services.AddTransient<ResponderConsulta>();

        AdicionarColeta(services, config);
        var limite = config["LIMITE_CONSULTAS_POR_MINUTO"] is { Length: > 0 } texto
            ? int.TryParse(texto, out var n) && n > 0
                ? n
                : throw new InvalidOperationException("LIMITE_CONSULTAS_POR_MINUTO deve ser um inteiro maior que zero.")
            : LimitadorDeConsultas.PadraoPorMinuto;
        services.AddSingleton(LimitadorDeConsultas.PorMinuto(limite));

        return services;
    }

    private static void AdicionarColeta(IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<BancoDeDados>(o =>
            o.UseNpgsql(Obrigatorio(config, "ConnectionStrings:Postgres"), npgsql => npgsql.UseVector()));

        var opcoes = new OpcoesDeColeta(
            Inteiro(config, "COLETA_JANELA_DIAS", 90),
            Inteiro(config, "COLETA_INTERVALO_MINUTOS", 60));
        services.AddSingleton(opcoes);
        services.AddSingleton(TimeProvider.System);

        var arquivo = config["FONTES_ARQUIVO"] is { Length: > 0 } caminho
            ? caminho
            : Path.Combine(AppContext.BaseDirectory, "fontes.yaml");
        services.AddSingleton<IFontesDeFeeds>(new FontesDeFeedsEmYaml(arquivo));
        services.AddHttpClient<ILeitorDeFeed, LeitorDeFeedRss>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("AssistenteDesinformacao/1.0");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Alguns feeds (g1) respondem comprimidos em gzip.
            AutomaticDecompression = DecompressionMethods.All,
        });
        services.AddScoped<IRepositorioDeMaterias, RepositorioDeMaterias>();
        services.AddTransient<ColetarFeeds>();
        services.AddScoped<IRepositorioDeFeedbacks, RepositorioDeFeedbacks>();
        services.AddTransient<RegistrarFeedback>();
        services.AddScoped<IRepositorioDeMetricas, RepositorioDeMetricas>();
        services.AddTransient<RegistrarTempoDeResposta>();
        services.AddTransient<ConsultarMetricas>();
    }

    /// <summary>Aplica as migrations pendentes. Chamado na inicialização do bot.</summary>
    public static async Task MigrarBancoAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var escopo = services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<BancoDeDados>().Database.MigrateAsync(ct);
    }

    private static int Inteiro(IConfiguration config, string chave, int padrao) =>
        int.TryParse(config[chave], out var valor) && valor > 0 ? valor : padrao;

    /// <summary>
    /// Cliente do provedor de IA com limite de tempo (25s nas etapas rápidas, que normalmente levam poucos segundos) e uma única nova tentativa: sem isso, uma chamada travada deixa o usuário esperando
    /// minutos (o padrão é 100s por chamada e 3 repetições).
    /// </summary>
    private static OpenAIClient ClienteDeIa(OpcoesDeIa ia, System.Text.Json.Nodes.JsonObject? camposExtras, TimeSpan? limite = null)
    {
        var opcoes = new OpenAIClientOptions
        {
            Endpoint = ia.UrlBase,
            NetworkTimeout = limite ?? TimeSpan.FromSeconds(25),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 1),
        };
        if (camposExtras is not null)
            opcoes.Transport = new HttpClientPipelineTransport(
                new HttpClient(new InjetorDeCamposNaRequisicao(camposExtras, new HttpClientHandler())));
        return new OpenAIClient(new ApiKeyCredential(ia.Chave), opcoes);
    }

    private static IChatClient SemRaciocinio(this IChatClient cliente) => new ClienteSemRaciocinio(cliente);

    private static string Obrigatorio(IConfiguration config, string chave) =>
        config[chave] is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException($"Defina {chave} (veja .env.example).");
}
