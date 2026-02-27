using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GLPack.Migrations
{
    /// <inheritdoc />
    public partial class AllowZeroZeroTransactionItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_entry_one_side",
                table: "transaction_item");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entry_one_side",
                table: "transaction_item",
                sql: "((debit = 0 AND credit >= 0) OR (credit = 0 AND debit >= 0))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_entry_one_side",
                table: "transaction_item");

            migrationBuilder.AddCheckConstraint(
                name: "ck_entry_one_side",
                table: "transaction_item",
                sql: "((debit = 0 AND credit > 0) OR (credit = 0 AND debit > 0))");
        }
    }
}
