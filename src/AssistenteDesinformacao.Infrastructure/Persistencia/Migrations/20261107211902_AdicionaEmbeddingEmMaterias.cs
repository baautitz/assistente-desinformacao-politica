using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace AssistenteDesinformacao.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaEmbeddingEmMaterias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "materias",
                type: "vector",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "materias");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
