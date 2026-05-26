using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SelectiveAgentDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BackupJobs_Agents_AgentId",
                table: "BackupJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_RestoreJobs_BackupArtifacts_ArtifactId",
                table: "RestoreJobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "ArtifactId",
                table: "RestoreJobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "RestoreJobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "AgentNameSnapshot",
                table: "RestoreJobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactFileNameSnapshot",
                table: "RestoreJobs",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactObjectKeySnapshot",
                table: "RestoreJobs",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyId",
                table: "BackupJobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "BackupJobs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "AgentNameSnapshot",
                table: "BackupJobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyNameSnapshot",
                table: "BackupJobs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BackupJobs_Agents_AgentId",
                table: "BackupJobs",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RestoreJobs_BackupArtifacts_ArtifactId",
                table: "RestoreJobs",
                column: "ArtifactId",
                principalTable: "BackupArtifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BackupJobs_Agents_AgentId",
                table: "BackupJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_RestoreJobs_BackupArtifacts_ArtifactId",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "AgentNameSnapshot",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "ArtifactFileNameSnapshot",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "ArtifactObjectKeySnapshot",
                table: "RestoreJobs");

            migrationBuilder.DropColumn(
                name: "AgentNameSnapshot",
                table: "BackupJobs");

            migrationBuilder.DropColumn(
                name: "PolicyNameSnapshot",
                table: "BackupJobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "ArtifactId",
                table: "RestoreJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "RestoreJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PolicyId",
                table: "BackupJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "BackupJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BackupJobs_Agents_AgentId",
                table: "BackupJobs",
                column: "AgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RestoreJobs_BackupArtifacts_ArtifactId",
                table: "RestoreJobs",
                column: "ArtifactId",
                principalTable: "BackupArtifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
