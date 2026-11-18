using System.Collections.Concurrent;

namespace AssistenteDesinformacao.WhatsAppBot;

/// <summary>
/// Voto por resposta curta logo após a síntese: "1" (útil) ou "2" (não ajudou). Não há botão nem emoji: sem a API oficial
/// os botões não aparecem de forma confiável no WhatsApp.
/// Guarda, só em memória e por pouco tempo, o id da última resposta que pediu avaliação em cada chat.
/// </summary>
public sealed class FeedbackPorNumero(TimeProvider? relogio = null)
{
    public const string Convite = "Essa resposta foi útil?\nResponda 1 (sim) ou 2 (não).";

    private static readonly TimeSpan Validade = TimeSpan.FromHours(24);
    private const int LimiteParaLimpeza = 1000;

    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, (Guid RespostaId, DateTimeOffset Quando)> _pendentes = new();

    public void Lembrar(string chat, Guid respostaId)
    {
        if (_pendentes.Count >= LimiteParaLimpeza)
            Limpar();
        _pendentes[chat] = (respostaId, _relogio.GetUtcNow());
    }

    /// <summary>"1" e "2" são votos mesmo quando não há resposta esperando (nesse caso são ignorados, não viram consulta).</summary>
    public static bool EhVoto(string texto) => texto.Trim() is "1" or "2";

    /// <summary>Interpreta o voto; devolve false se não for um voto ou se não há resposta esperando avaliação. O voto vale uma vez só.</summary>
    public bool TentarVotar(string chat, string texto, out Guid respostaId, out bool util)
    {
        respostaId = default;
        util = false;

        if (!EhVoto(texto))
            return false;
        util = texto.Trim() == "1";

        if (!_pendentes.TryRemove(chat, out var pendente) || _relogio.GetUtcNow() - pendente.Quando > Validade)
            return false;

        respostaId = pendente.RespostaId;
        return true;
    }

    private void Limpar()
    {
        var corte = _relogio.GetUtcNow() - Validade;
        foreach (var (chat, pendente) in _pendentes)
            if (pendente.Quando < corte)
                _pendentes.TryRemove(chat, out _);
    }
}
