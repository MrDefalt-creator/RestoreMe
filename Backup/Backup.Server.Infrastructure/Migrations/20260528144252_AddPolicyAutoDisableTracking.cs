using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyAutoDisableTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AutoDisabledAt",
                table: "BackupPolicies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveFailureCount",
                table: "BackupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastFailureReason",
                table: "BackupPolicies",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoDisabledAt",
                table: "BackupPolicies");

            migrationBuilder.DropColumn(
                name: "ConsecutiveFailureCount",
                table: "BackupPolicies");

            migrationBuilder.DropColumn(
                name: "LastFailureReason",
                table: "BackupPolicies");
        }
    }
}
