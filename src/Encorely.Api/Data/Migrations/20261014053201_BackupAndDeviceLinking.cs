using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Encorely.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackupAndDeviceLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_link_request",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_device_ephemeral_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sender_ephemeral_key = table.Column<byte[]>(type: "bytea", nullable: true),
                    nonce = table.Column<byte[]>(type: "bytea", nullable: true),
                    ciphertext = table.Column<byte[]>(type: "bytea", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_link_request", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_link_request_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "encrypted_backup",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    ciphertext = table.Column<byte[]>(type: "bytea", nullable: false),
                    nonce = table.Column<byte[]>(type: "bytea", nullable: false),
                    salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    kdf_algorithm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kdf_ops_limit = table.Column<long>(type: "bigint", nullable: false),
                    kdf_mem_limit = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encrypted_backup", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_encrypted_backup_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_link_request_user_id_expires_at",
                table: "device_link_request",
                columns: new[] { "user_id", "expires_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_link_request");

            migrationBuilder.DropTable(
                name: "encrypted_backup");
        }
    }
}
