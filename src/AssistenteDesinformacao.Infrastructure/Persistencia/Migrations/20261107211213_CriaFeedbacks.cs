using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AssistenteDesinformacao.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CriaFeedbacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RespostaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Util = table.Column<bool>(type: "boolean", nullable: false),
                    Data = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedbacks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feedbacks_RespostaId",
                table: "feedbacks",
                column: "RespostaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "feedbacks");
        }
    }
}
