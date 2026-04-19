using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OilChangePOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceContainsEstimatedCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContainsEstimatedCost",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContainsEstimatedCost",
                table: "Invoices");
        }
    }
}
