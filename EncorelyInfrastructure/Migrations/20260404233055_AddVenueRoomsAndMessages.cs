using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EncorelyInfrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVenueRoomsAndMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VenueRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueRooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VenueMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    IsModerated = table.Column<bool>(type: "boolean", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenueMessages_VenueRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "VenueRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VenueMessages_RoomId",
                table: "VenueMessages",
                column: "RoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VenueMessages");

            migrationBuilder.DropTable(
                name: "VenueRooms");
        }
    }
}
