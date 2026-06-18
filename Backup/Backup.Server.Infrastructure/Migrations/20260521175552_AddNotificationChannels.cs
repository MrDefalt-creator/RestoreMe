using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Settings = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SubscribedEvents = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationChannels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationChannels_IsEnabled",
                table: "NotificationChannels",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationChannels_Name",
                table: "NotificationChannels",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationChannels");
        }
    }
}
