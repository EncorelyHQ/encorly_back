using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Encorely.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReporteAbiertoUnicoPorPar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes del índice único: de los abiertos repetidos queda el más viejo (su lugar en la cola) y los demás se descartan.
            migrationBuilder.Sql("""
                UPDATE reports r
                SET status = 'Dismissed',
                    resolution_note = 'Duplicado: la misma persona ya tenía un reporte abierto contra esta cuenta.',
                    resolved_at = now()
                WHERE r.status = 'Open' AND r.reporter_id IS NOT NULL
                  AND EXISTS (
                    SELECT 1 FROM reports o
                    WHERE o.status = 'Open' AND o.reporter_id = r.reporter_id AND o.reported_user_id = r.reported_user_id
                      AND (o.created_at, o.id) < (r.created_at, r.id));
                """);

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id_reported_user_id",
                table: "reports",
                columns: new[] { "reporter_id", "reported_user_id" },
                unique: true,
                filter: "status = 'Open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reports_reporter_id_reported_user_id",
                table: "reports");
        }
    }
}
