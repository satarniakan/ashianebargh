using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ashianebargh.App.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditActorAndIp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorId",
                table: "AuditLogs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorId",
                table: "AuditLogs",
                column: "ActorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AuditLogs");
        }
    }
}