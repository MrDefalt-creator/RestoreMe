using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionStrategies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetentionMaxCount",
                table: "BackupPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RetentionMaxTotalBytes",
                table: "BackupPolicies",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetentionMaxCount",
                table: "BackupPolicies");

            migrationBuilder.DropColumn(
                name: "RetentionMaxTotalBytes",
                table: "BackupPolicies");
        }
    }
}
