using System.Collections.Concurrent;

namespace AssistenteDesinformacao.WhatsAppBot;

/// <summary>
/// O WhatsApp sem API oficial não tem botão: o voto é um 👍 ou 👎 (texto ou reação) logo após a resposta.
/// Guarda, só em memória e por pouco tempo, o id da última resposta que pediu avaliação em cada chat.
/// </summary>
public sealed class FeedbackPorEmoji(TimeProvider? relogio = null)
{
    public const string Convite = "Foi útil? Responda com 👍 ou 👎.";

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

    /// <summary>Interpreta o emoji; devolve false se não for um voto ou se não há resposta esperando avaliação. O voto vale uma vez só.</summary>
    public bool TentarVotar(string chat, string texto, out Guid respostaId, out bool util)
    {
        respostaId = default;
        util = false;

        var emoji = texto.Trim();
        if (emoji.StartsWith("👍", StringComparison.Ordinal))
            util = true;
        else if (!emoji.StartsWith("👎", StringComparison.Ordinal))
            return false;

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
