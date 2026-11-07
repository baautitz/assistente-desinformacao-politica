using AssistenteDesinformacao.Application.Feedbacks;
using AssistenteDesinformacao.Domain.Feedbacks;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Infrastructure.Persistencia;

public sealed class RepositorioDeFeedbacks(BancoDeDados banco) : IRepositorioDeFeedbacks
{
    public async Task RegistrarAsync(Feedback feedback, CancellationToken ct)
    {
        try
        {
            await SubstituirAsync(feedback, ct);
        }
        catch (DbUpdateException)
        {
            // Dois votos simultâneos da mesma resposta bateram no índice único: o último voto vale.
            banco.ChangeTracker.Clear();
            await SubstituirAsync(feedback, ct);
        }
    }

    private async Task SubstituirAsync(Feedback feedback, CancellationToken ct)
    {
        var atuais = await banco.Feedbacks.Where(f => f.RespostaId == feedback.RespostaId).ToListAsync(ct);
        banco.Feedbacks.RemoveRange(atuais);
        banco.Feedbacks.Add(feedback);
        await banco.SaveChangesAsync(ct);
    }
}
