using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreJobAdvancedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BytesDone",
                table: "RestoreJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "BytesTotal",
                table: "RestoreJobs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DryRun",
                table: "RestoreJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EtaSeconds",
                table: "RestoreJobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Force",
                table: "RestoreJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LogTail",
                table: "RestoreJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "RestoreJobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetAgentId",
                table: "RestoreJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetName",
                table: "RestoreJobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BytesDone",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "BytesTotal",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "DryRun",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "EtaSeconds",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "Force",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "LogTail",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "TargetAgentId",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "TargetName",
                table: "RestoreJobs");
        }
    }
}
