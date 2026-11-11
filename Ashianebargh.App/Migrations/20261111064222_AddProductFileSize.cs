using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ashianebargh.App.Migrations
{
    /// <inheritdoc />
    public partial class AddProductFileSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "ProductFiles",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "ProductFiles");
        }
    }
}
