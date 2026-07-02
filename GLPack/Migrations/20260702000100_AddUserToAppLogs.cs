using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GLPack.Migrations
{
    /// <inheritdoc />
    public partial class AddUserToAppLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "AppLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserEmail",
                table: "AppLogs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AppLogs");

            migrationBuilder.DropColumn(
                name: "UserEmail",
                table: "AppLogs");
        }
    }
}
