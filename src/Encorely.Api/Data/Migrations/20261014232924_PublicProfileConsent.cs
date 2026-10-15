using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Encorely.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PublicProfileConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_profile_reactions_to_user_id",
                table: "profile_reactions");

            migrationBuilder.AddColumn<bool>(
                name: "show_profile_to_others",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "target_name",
                table: "profile_reactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_profile_reactions_to_user_id_updated_at",
                table: "profile_reactions",
                columns: new[] { "to_user_id", "updated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_profile_reactions_to_user_id_updated_at",
                table: "profile_reactions");

            migrationBuilder.DropColumn(
                name: "show_profile_to_others",
                table: "users");

            migrationBuilder.DropColumn(
                name: "target_name",
                table: "profile_reactions");

            migrationBuilder.CreateIndex(
                name: "ix_profile_reactions_to_user_id",
                table: "profile_reactions",
                column: "to_user_id");
        }
    }
}
