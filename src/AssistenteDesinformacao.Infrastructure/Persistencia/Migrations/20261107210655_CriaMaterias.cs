using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AssistenteDesinformacao.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriaMaterias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "materias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Titulo = table.Column<string>(type: "text", nullable: false),
                    Resumo = table.Column<string>(type: "text", nullable: false),
                    Link = table.Column<string>(type: "text", nullable: false),
                    DataPublicacao = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Veiculo = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_materias", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_materias_DataPublicacao",
                table: "materias",
                column: "DataPublicacao");

            migrationBuilder.CreateIndex(
                name: "IX_materias_Link",
                table: "materias",
                column: "Link",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "materias");
        }
    }
}
