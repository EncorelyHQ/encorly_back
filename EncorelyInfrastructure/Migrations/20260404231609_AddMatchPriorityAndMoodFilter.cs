using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EncorelyInfrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMatchPriorityAndMoodFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHighPriority",
                table: "Matches",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHighPriority",
                table: "Matches");
        }
    }
}
