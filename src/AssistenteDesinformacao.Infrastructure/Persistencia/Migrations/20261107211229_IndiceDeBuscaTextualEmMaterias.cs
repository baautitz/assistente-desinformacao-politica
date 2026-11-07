using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AssistenteDesinformacao.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class IndiceDeBuscaTextualEmMaterias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mesma expressão usada em BuscaDeMateriasNoPostgres, para o Postgres aproveitar o índice.
            migrationBuilder.Sql(
                "CREATE INDEX ix_materias_busca_textual ON materias " +
                "USING GIN (to_tsvector('portuguese', \"Titulo\" || ' ' || \"Resumo\"));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX ix_materias_busca_textual;");
        }
    }
}
