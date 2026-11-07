using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Domain.Mensageria;
using Microsoft.Extensions.AI;

namespace AssistenteDesinformacao.Infrastructure.Analise;

public sealed class ExtratorDeAlegacoesDeImagemComIa(IChatClient ia) : IExtratorDeAlegacoesDeImagem
{
    private const string Instrucoes =
        $"""
        Você recebe um print de conversa ou de rede social que circulou em grupos de mensagem.
        Leia o conteúdo e liste as afirmações de fato verificáveis que ele faz (no máximo 3), uma por linha.
        {ExtratorDeAlegacoesComIa.RegrasDeSaida}
        """;

    public bool Disponivel => true;

    public async Task<IReadOnlyList<Alegacao>> ExtrairAsync(Imagem imagem, CancellationToken ct)
    {
        var resposta = await ia.GetResponseAsync(
            [
                new ChatMessage(ChatRole.System, Instrucoes),
                new ChatMessage(ChatRole.User, [new DataContent(imagem.Conteudo, imagem.TipoDeMidia)]),
            ],
            new ChatOptions { Temperature = 0 },
            ct);

        return ExtratorDeAlegacoesComIa.Interpretar(resposta.Text);
    }
}
