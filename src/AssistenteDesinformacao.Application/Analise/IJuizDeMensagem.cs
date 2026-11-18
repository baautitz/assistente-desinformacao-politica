namespace AssistenteDesinformacao.Application.Analise;

public enum TipoDeMensagem
{
    /// <summary>Tem fato que dá para conferir (ou não deu para decidir: segue o fluxo normal de apuração).</summary>
    Afirmacao,

    /// <summary>Pede ou dá orientação de voto, sem fato checável.</summary>
    PedidoDeVoto,

    /// <summary>Pede ao bot opinião, julgamento de valor ou ranking sobre políticos, partidos, governos ou ideologias.</summary>
    PedidoDeOpiniao,

    /// <summary>Opinião ou previsão do próprio usuário sobre um assunto político: ainda dá para buscar o contexto.</summary>
    Opiniao,

    /// <summary>Nada a ver com política, notícia ou checagem.</summary>
    ForaDoEscopo,
}

/// <summary>
/// Pré-julgamento por IA de toda mensagem de texto, antes de qualquer busca: decide se há o que apurar ou se o bot deve recusar.
/// Na dúvida devolve <see cref="TipoDeMensagem.Afirmacao"/>, porque o fluxo normal ainda passa pelo juiz de relevância e pelos guardrails.
/// </summary>
public interface IJuizDeMensagem
{
    Task<TipoDeMensagem> JulgarAsync(string texto, CancellationToken ct);
}
