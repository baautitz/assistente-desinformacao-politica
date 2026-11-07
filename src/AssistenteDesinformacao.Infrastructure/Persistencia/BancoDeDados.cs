using AssistenteDesinformacao.Domain.Coleta;
using AssistenteDesinformacao.Domain.Feedbacks;
using AssistenteDesinformacao.Domain.Metricas;
using Microsoft.EntityFrameworkCore;

namespace AssistenteDesinformacao.Infrastructure.Persistencia;

public class BancoDeDados(DbContextOptions<BancoDeDados> opcoes) : DbContext(opcoes)
{
    public DbSet<Materia> Materias => Set<Materia>();

    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    public DbSet<TempoDeResposta> TemposDeResposta => Set<TempoDeResposta>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.HasPostgresExtension("vector");

        modelo.Entity<Materia>(m =>
        {
            m.ToTable("materias");
            m.Property<int>("Id").ValueGeneratedOnAdd();
            m.HasKey("Id");
            m.Property(x => x.Titulo).IsRequired();
            m.Property(x => x.Resumo).IsRequired();
            m.Property(x => x.Veiculo).IsRequired();
            // Provedores sem pgvector (InMemory dos testes) não guardam o vetor.
            if (Database.IsNpgsql())
            {
                var embedding = m.Property(x => x.Embedding)
                    .HasColumnType("vector")
                    .HasConversion(
                        v => v == null ? null : new Pgvector.Vector(v),
                        v => v == null ? null : v.ToArray());
                embedding.Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<float[]?>(
                    (a, b) => ReferenceEquals(a, b) || (a != null && b != null && a.SequenceEqual(b)),
                    v => v == null ? 0 : v.Aggregate(0, HashCode.Combine),
                    v => v == null ? null : v.ToArray()));
            }
            else
                m.Ignore(x => x.Embedding);
            m.HasIndex(x => x.Link).IsUnique();
            m.HasIndex(x => x.DataPublicacao);
        });

        modelo.Entity<Feedback>(f =>
        {
            f.ToTable("feedbacks");
            f.Property<int>("Id").ValueGeneratedOnAdd();
            f.HasKey("Id");
            f.HasIndex(x => x.RespostaId).IsUnique();
        });

        modelo.Entity<TempoDeResposta>(t =>
        {
            t.ToTable("tempos_de_resposta");
            t.Property<int>("Id").ValueGeneratedOnAdd();
            t.HasKey("Id");
            t.HasIndex(x => x.Data);
        });
    }
}
