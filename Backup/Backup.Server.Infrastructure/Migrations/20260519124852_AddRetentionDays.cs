using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetentionDays",
                table: "BackupPolicies",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetentionDays",
                table: "BackupPolicies");
        }
    }
}
