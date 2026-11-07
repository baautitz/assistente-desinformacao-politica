using AssistenteDesinformacao.Application.Analise;
using AssistenteDesinformacao.Application.Pesquisa;
using AssistenteDesinformacao.Domain.Analise;
using AssistenteDesinformacao.Application.Sinteses;
using AssistenteDesinformacao.Domain.Mensageria;
using AssistenteDesinformacao.Domain.Pesquisa;
using AssistenteDesinformacao.Domain.Sinteses;

namespace AssistenteDesinformacao.Application.Mensageria;

public sealed class ResponderConsulta(
    IExtratorDeAlegacoes extrator,
    IExtratorDeAlegacoesDeImagem extratorDeImagem,
    IBuscadorDeFontes buscador,
    IRedatorDeSintese redator)
{
    public const int MaximoDeAlegacoes = 3;
    public const int MaximoDeFontes = 5;
    public const int MaximoDeBytesDaImagem = 5 * 1024 * 1024;

    public async Task<Sintese> ExecutarAsync(Consulta consulta, CancellationToken ct = default)
    {
        IReadOnlyList<Alegacao> extraidas;
        if (consulta.Imagem is { } imagem)
        {
            if (!extratorDeImagem.Disponivel)
                return new Sintese(MensagensDoBot.SoLeTexto, []);
            if (imagem.Conteudo.Length > MaximoDeBytesDaImagem)
                return new Sintese(MensagensDoBot.ImagemGrande, []);
            extraidas = await extratorDeImagem.ExtrairAsync(imagem, ct);
        }
        else if (string.IsNullOrWhiteSpace(consulta.Texto))
            return SemApuracao();
        else
            extraidas = await extrator.ExtrairAsync(consulta.Texto, ct);

        var alegacoes = extraidas
            .Take(MaximoDeAlegacoes)
            .ToList();
        if (alegacoes.Count == 0)
            return SemApuracao();

        var encontradas = new List<Fonte>();
        foreach (var alegacao in alegacoes)
            encontradas.AddRange(await buscador.BuscarAsync(alegacao, ct));

        var fontes = encontradas
            .DistinctBy(f => f.Link)
            .OrderByDescending(f => f.DataPublicacao)
            .Take(MaximoDeFontes)
            .ToList();
        if (fontes.Count == 0)
            return SemApuracao();

        var resumo = (await redator.RedigirAsync(alegacoes, fontes, ct)).Trim();
        return new Sintese(resumo.Length > 0 ? resumo : MensagensDoBot.ResumoPadrao, fontes);
    }

    private static Sintese SemApuracao() => new(MensagensDoBot.SemApuracao, []);
}
