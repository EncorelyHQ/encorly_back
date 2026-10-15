using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Encorely.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LoginThrottlePerIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                table: "users");

            migrationBuilder.DropColumn(
                name: "lockout_ends_at",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lockout_ends_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
