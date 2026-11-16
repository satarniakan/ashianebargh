using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ashianebargh.App.Migrations
{
    /// <inheritdoc />
    public partial class BindOtpToPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_PhoneNumber",
                table: "OtpCodes");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "OtpCodes",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "OtpCodes",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "OtpCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_PhoneNumber_Purpose_Code",
                table: "OtpCodes",
                columns: new[] { "PhoneNumber", "Purpose", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OtpCodes_PhoneNumber_Purpose_Code",
                table: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "OtpCodes");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "OtpCodes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "OtpCodes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_PhoneNumber",
                table: "OtpCodes",
                column: "PhoneNumber");
        }
    }
}
