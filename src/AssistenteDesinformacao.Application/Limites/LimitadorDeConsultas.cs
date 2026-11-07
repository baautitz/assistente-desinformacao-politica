using System.Collections.Concurrent;

namespace AssistenteDesinformacao.Application.Limites;

/// <summary>
/// Limita quantas consultas cada usuário pode fazer numa janela de tempo (janela deslizante).
/// Tudo fica em memória e só guarda o id numérico do usuário e horários; nada é persistido.
/// </summary>
public sealed class LimitadorDeConsultas
{
    public const int PadraoPorMinuto = 5;

    private const int LimiteParaLimpeza = 1000;

    private readonly int _maximo;
    private readonly TimeSpan _janela;
    private readonly TimeProvider _relogio;
    private readonly ConcurrentDictionary<long, Queue<DateTimeOffset>> _consultas = new();

    public LimitadorDeConsultas(int maximoPorJanela, TimeSpan janela, TimeProvider? relogio = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximoPorJanela, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(janela, TimeSpan.Zero);
        _maximo = maximoPorJanela;
        _janela = janela;
        _relogio = relogio ?? TimeProvider.System;
    }

    public static LimitadorDeConsultas PorMinuto(int maximo, TimeProvider? relogio = null) =>
        new(maximo, TimeSpan.FromMinutes(1), relogio);

    /// <summary>Registra a consulta e devolve true se ela cabe no limite; false se deve ser recusada.</summary>
    public bool TentarRegistrar(long usuario)
    {
        var agora = _relogio.GetUtcNow();

        if (_consultas.Count > LimiteParaLimpeza)
            RemoverExpirados(agora);

        while (true)
        {
            var fila = _consultas.GetOrAdd(usuario, _ => new Queue<DateTimeOffset>());
            lock (fila)
            {
                // A fila pode ter sido removida pela limpeza entre o GetOrAdd e o lock.
                if (!_consultas.TryGetValue(usuario, out var atual) || !ReferenceEquals(atual, fila))
                    continue;

                Descartar(fila, agora);
                if (fila.Count >= _maximo)
                    return false;

                fila.Enqueue(agora);
                return true;
            }
        }
    }

    private void Descartar(Queue<DateTimeOffset> fila, DateTimeOffset agora)
    {
        while (fila.Count > 0 && agora - fila.Peek() >= _janela)
            fila.Dequeue();
    }

    private void RemoverExpirados(DateTimeOffset agora)
    {
        foreach (var (usuario, fila) in _consultas)
        {
            lock (fila)
            {
                Descartar(fila, agora);
                if (fila.Count == 0)
                    _consultas.TryRemove(new KeyValuePair<long, Queue<DateTimeOffset>>(usuario, fila));
            }
        }
    }
}
