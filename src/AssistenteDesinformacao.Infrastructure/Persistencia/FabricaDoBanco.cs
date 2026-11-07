using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AssistenteDesinformacao.Infrastructure.Persistencia;

/// <summary>Usada só pelas ferramentas do EF (dotnet ef migrations add).</summary>
public sealed class FabricaDoBanco : IDesignTimeDbContextFactory<BancoDeDados>
{
    public BancoDeDados CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<BancoDeDados>().UseNpgsql("Host=localhost;Database=desinformacao").Options);
}
