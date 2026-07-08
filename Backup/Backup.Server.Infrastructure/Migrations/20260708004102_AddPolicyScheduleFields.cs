using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyScheduleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CronExpression",
                table: "BackupPolicies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleKind",
                table: "BackupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "BackupPolicies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WindowEndMinutes",
                table: "BackupPolicies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WindowStartMinutes",
                table: "BackupPolicies",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CronExpression",
                table: "BackupPolicies");

            migrationBuilder.DropColumn(
                name: "ScheduleKind",
                table: "BackupPolicies");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "BackupPolicies");

            migrationBuilder.DropColumn(
                name: "WindowEndMinutes",
                table: "BackupPolicies");

            migrationBuilder.DropColumn(
                name: "WindowStartMinutes",
                table: "BackupPolicies");
        }
    }
}
