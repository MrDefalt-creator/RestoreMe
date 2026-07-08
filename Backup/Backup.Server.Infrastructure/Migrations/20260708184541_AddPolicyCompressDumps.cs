using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backup.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPolicyCompressDumps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing policies keep compression on (opt-out design): backfill true.
            migrationBuilder.AddColumn<bool>(
                name: "CompressDumps",
                table: "BackupPolicies",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompressDumps",
                table: "BackupPolicies");
        }
    }
}
