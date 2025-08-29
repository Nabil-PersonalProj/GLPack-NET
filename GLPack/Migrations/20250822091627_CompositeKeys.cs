using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GLPack.Migrations
{
    /// <inheritdoc />
    public partial class CompositeKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transaction_item_account_AccountId",
                table: "transaction_item");

            migrationBuilder.DropForeignKey(
                name: "FK_transaction_item_transaction_TransactionId",
                table: "transaction_item");

            migrationBuilder.DropIndex(
                name: "IX_transaction_item_AccountId",
                table: "transaction_item");

            migrationBuilder.DropIndex(
                name: "IX_transaction_item_TransactionId",
                table: "transaction_item");

            migrationBuilder.RenameColumn(
                name: "TransactionId",
                table: "transaction_item",
                newName: "transaction_no");

            migrationBuilder.RenameColumn(
                name: "AccountId",
                table: "transaction_item",
                newName: "company_id");

            migrationBuilder.AddColumn<string>(
                name: "account_code",
                table: "transaction_item",
                type: "character varying(50)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_transaction_CompanyId_TransactionNo",
                table: "transaction",
                columns: new[] { "CompanyId", "TransactionNo" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_account_CompanyId_Code",
                table: "account",
                columns: new[] { "CompanyId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_transaction_item_company_id_account_code",
                table: "transaction_item",
                columns: new[] { "company_id", "account_code" });

            migrationBuilder.CreateIndex(
                name: "IX_transaction_item_company_id_transaction_no",
                table: "transaction_item",
                columns: new[] { "company_id", "transaction_no" });

            migrationBuilder.AddForeignKey(
                name: "FK_transaction_item_account_company_id_account_code",
                table: "transaction_item",
                columns: new[] { "company_id", "account_code" },
                principalTable: "account",
                principalColumns: new[] { "CompanyId", "Code" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transaction_item_transaction_company_id_transaction_no",
                table: "transaction_item",
                columns: new[] { "company_id", "transaction_no" },
                principalTable: "transaction",
                principalColumns: new[] { "CompanyId", "TransactionNo" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transaction_item_account_company_id_account_code",
                table: "transaction_item");

            migrationBuilder.DropForeignKey(
                name: "FK_transaction_item_transaction_company_id_transaction_no",
                table: "transaction_item");

            migrationBuilder.DropIndex(
                name: "IX_transaction_item_company_id_account_code",
                table: "transaction_item");

            migrationBuilder.DropIndex(
                name: "IX_transaction_item_company_id_transaction_no",
                table: "transaction_item");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_transaction_CompanyId_TransactionNo",
                table: "transaction");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_account_CompanyId_Code",
                table: "account");

            migrationBuilder.DropColumn(
                name: "account_code",
                table: "transaction_item");

            migrationBuilder.RenameColumn(
                name: "transaction_no",
                table: "transaction_item",
                newName: "TransactionId");

            migrationBuilder.RenameColumn(
                name: "company_id",
                table: "transaction_item",
                newName: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_item_AccountId",
                table: "transaction_item",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_transaction_item_TransactionId",
                table: "transaction_item",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_transaction_item_account_AccountId",
                table: "transaction_item",
                column: "AccountId",
                principalTable: "account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_transaction_item_transaction_TransactionId",
                table: "transaction_item",
                column: "TransactionId",
                principalTable: "transaction",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
