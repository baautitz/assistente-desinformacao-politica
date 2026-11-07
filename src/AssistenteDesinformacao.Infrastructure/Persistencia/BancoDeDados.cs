using AssistenteDesinformacao.Domain.Coleta;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Infrastructure.Persistencia;

public class BancoDeDados(DbContextOptions<BancoDeDados> opcoes) : DbContext(opcoes)
{
    public DbSet<Materia> Materias => Set<Materia>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Materia>(m =>
        {
            m.ToTable("materias");
            m.Property<int>("Id").ValueGeneratedOnAdd();
            m.HasKey("Id");
            m.Property(x => x.Titulo).IsRequired();
            m.Property(x => x.Resumo).IsRequired();
            m.Property(x => x.Veiculo).IsRequired();
            m.HasIndex(x => x.Link).IsUnique();
            m.HasIndex(x => x.DataPublicacao);
        });
    }
}
