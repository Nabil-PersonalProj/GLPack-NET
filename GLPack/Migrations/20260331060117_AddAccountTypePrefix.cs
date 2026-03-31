using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GLPack.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountTypePrefix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "account_type_prefix",
                columns: table => new
                {
                    Prefix = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AccountType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_type_prefix", x => x.Prefix);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_type_prefix");
        }
    }
}
