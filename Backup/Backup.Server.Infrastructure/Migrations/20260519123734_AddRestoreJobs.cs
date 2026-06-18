using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestoreJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RestoreJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoreJobs_BackupArtifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "BackupArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestoreJobs_AgentId_Status",
                table: "RestoreJobs",
                columns: new[] { "AgentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RestoreJobs_ArtifactId",
                table: "RestoreJobs",
                column: "ArtifactId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestoreJobs");
        }
    }
}
