using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OilChangePOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppUserHomeBranchWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HomeBranchWarehouseId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_HomeBranchWarehouseId",
                table: "Users",
                column: "HomeBranchWarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Warehouses_HomeBranchWarehouseId",
                table: "Users",
                column: "HomeBranchWarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Warehouses_HomeBranchWarehouseId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_HomeBranchWarehouseId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "HomeBranchWarehouseId",
                table: "Users");
        }
    }
}
