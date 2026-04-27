using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLogicalDatabaseBackupPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "BackupPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BackupPolicyDatabaseSettings",
                columns: table => new
                {
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Engine = table.Column<int>(type: "integer", nullable: false),
                    AuthMode = table.Column<int>(type: "integer", nullable: false),
                    Host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Port = table.Column<int>(type: "integer", nullable: true),
                    DatabaseName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Username = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupPolicyDatabaseSettings", x => x.PolicyId);
                    table.ForeignKey(
                        name: "FK_BackupPolicyDatabaseSettings_BackupPolicies_PolicyId",
                        column: x => x.PolicyId,
                        principalTable: "BackupPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupPolicies_Type",
                table: "BackupPolicies",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_BackupPolicyDatabaseSettings_Engine_DatabaseName",
                table: "BackupPolicyDatabaseSettings",
                columns: new[] { "Engine", "DatabaseName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupPolicyDatabaseSettings");

            migrationBuilder.DropIndex(
                name: "IX_BackupPolicies_Type",
                table: "BackupPolicies");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "BackupPolicies");
        }
    }
}
