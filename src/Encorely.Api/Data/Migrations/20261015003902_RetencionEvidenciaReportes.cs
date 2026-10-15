using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Encorely.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RetencionEvidenciaReportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "evidence_purged_at",
                table: "reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_reports_resolved_at",
                table: "reports",
                column: "resolved_at",
                filter: "evidence_purged_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reports_resolved_at",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "evidence_purged_at",
                table: "reports");
        }
    }
}
