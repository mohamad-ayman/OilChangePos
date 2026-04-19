using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OilChangePOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class BranchStockRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchStockRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BranchWarehouseId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedByUserId = table.Column<int>(type: "int", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FulfillmentStockMovementId = table.Column<int>(type: "int", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchStockRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BranchStockRequests_Warehouses_BranchWarehouseId",
                        column: x => x.BranchWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchStockRequests_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchStockRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BranchStockRequests_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchStockRequests_BranchWarehouseId_Status",
                table: "BranchStockRequests",
                columns: new[] { "BranchWarehouseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchStockRequests_CreatedAtUtc",
                table: "BranchStockRequests",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BranchStockRequests_ProductId",
                table: "BranchStockRequests",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchStockRequests_RequestedByUserId",
                table: "BranchStockRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchStockRequests_ResolvedByUserId",
                table: "BranchStockRequests",
                column: "ResolvedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BranchStockRequests");
        }
    }
}
