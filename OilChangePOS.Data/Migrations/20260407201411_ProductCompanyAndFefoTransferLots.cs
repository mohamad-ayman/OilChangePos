using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OilChangePOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductCompanyAndFefoTransferLots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Name_ProductCategory_PackageSize",
                table: "Products");

            migrationBuilder.AddColumn<int>(
                name: "SourcePurchaseId",
                table: "StockMovements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Products",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_SourcePurchaseId",
                table: "StockMovements",
                column: "SourcePurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyName_Name_ProductCategory_PackageSize",
                table: "Products",
                columns: new[] { "CompanyName", "Name", "ProductCategory", "PackageSize" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Purchases_SourcePurchaseId",
                table: "StockMovements",
                column: "SourcePurchaseId",
                principalTable: "Purchases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Purchases_SourcePurchaseId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_SourcePurchaseId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_Products_CompanyName_Name_ProductCategory_PackageSize",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SourcePurchaseId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name_ProductCategory_PackageSize",
                table: "Products",
                columns: new[] { "Name", "ProductCategory", "PackageSize" },
                unique: true);
        }
    }
}
