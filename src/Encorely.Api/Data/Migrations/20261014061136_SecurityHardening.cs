using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Encorely.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "session_id",
                table: "device_push_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "banned_emails",
                columns: table => new
                {
                    email_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_banned_emails", x => x.email_hash);
                });

            migrationBuilder.CreateTable(
                name: "message_franking_records",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    franking_tag = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_franking_records", x => x.message_id);
                    table.ForeignKey(
                        name: "fk_message_franking_records_users_recipient_id",
                        column: x => x.recipient_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_message_franking_records_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_push_tokens_session_id",
                table: "device_push_tokens",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_franking_records_created_at",
                table: "message_franking_records",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_message_franking_records_match_id",
                table: "message_franking_records",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_franking_records_recipient_id",
                table: "message_franking_records",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "ix_message_franking_records_sender_id",
                table: "message_franking_records",
                column: "sender_id");

            // Datos previos: cuentas ya suspendidas conservan el veto de re-registro y los mensajes con franking
            // que aún no fueron entregados no pierden su compromiso. sha256(bytea) es nativo desde PostgreSQL 11.
            migrationBuilder.Sql("""
                INSERT INTO banned_emails (email_hash, created_at)
                SELECT sha256(convert_to(email, 'UTF8')), now() FROM users WHERE status = 'Banned'
                ON CONFLICT DO NOTHING;
                """);
            migrationBuilder.Sql("""
                INSERT INTO message_franking_records (message_id, match_id, sender_id, recipient_id, franking_tag, created_at)
                SELECT c.id, c.match_id, c.sender_id,
                       CASE WHEN m.user_a_id = c.sender_id THEN m.user_b_id ELSE m.user_a_id END,
                       c.franking_tag, c.created_at
                FROM chat_messages c JOIN matches m ON m.id = c.match_id
                WHERE c.franking_tag IS NOT NULL
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "banned_emails");

            migrationBuilder.DropTable(
                name: "message_franking_records");

            migrationBuilder.DropIndex(
                name: "ix_device_push_tokens_session_id",
                table: "device_push_tokens");

            migrationBuilder.DropColumn(
                name: "session_id",
                table: "device_push_tokens");
        }
    }
}
