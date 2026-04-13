using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OilChangePOS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompaniesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table => table.PrimaryKey("PK_Companies", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Name",
                table: "Companies",
                column: "Name",
                unique: true);

            // Idempotent: older DBs may use a different unique index name or none at all.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Products') AND name = N'IX_Products_CompanyName_Name_ProductCategory_PackageSize')
                    DROP INDEX [IX_Products_CompanyName_Name_ProductCategory_PackageSize] ON [dbo].[Products];
                """);

            migrationBuilder.AddColumn<int>(
                name: "CompanyId",
                table: "Products",
                type: "int",
                nullable: true);

            // Seed company rows (no reference to Products.CompanyId binding yet).
            migrationBuilder.Sql(
                """
                INSERT INTO Companies (Name, IsActive)
                SELECT DISTINCT LTRIM(RTRIM([CompanyName])), CAST(1 AS bit)
                FROM Products
                WHERE LTRIM(RTRIM(ISNULL([CompanyName], N''))) <> N''
                  AND NOT EXISTS (
                      SELECT 1 FROM Companies c
                      WHERE c.Name = LTRIM(RTRIM(Products.[CompanyName])));

                IF NOT EXISTS (SELECT 1 FROM Companies WHERE Name = N'عام')
                    INSERT INTO Companies (Name, IsActive) VALUES (N'عام', CAST(1 AS bit));
                """);

            migrationBuilder.Sql(
                """
                DECLARE @GeneralId INT = (SELECT TOP 1 Id FROM Companies WHERE Name = N'عام');

                UPDATE p
                SET CompanyId = c.Id
                FROM Products p
                INNER JOIN Companies c ON c.Name = LTRIM(RTRIM(p.[CompanyName]))
                WHERE p.CompanyId IS NULL
                  AND LTRIM(RTRIM(ISNULL(p.[CompanyName], N''))) <> N'';

                UPDATE Products SET CompanyId = @GeneralId WHERE CompanyId IS NULL;
                """);

            // Drop default constraint first (e.g. DF_Products_CompanyName) or SQL Server returns 5074/4922.
            migrationBuilder.Sql(
                """
                DECLARE @cnDf SYSNAME;
                DECLARE @dropCnSql NVARCHAR(512);
                SELECT @cnDf = dc.name
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Products') AND c.name = N'CompanyName';
                IF @cnDf IS NOT NULL
                BEGIN
                    SET @dropCnSql = N'ALTER TABLE [dbo].[Products] DROP CONSTRAINT ' + QUOTENAME(@cnDf) + N';';
                    EXEC (@dropCnSql);
                END
                ALTER TABLE [dbo].[Products] DROP COLUMN [CompanyName];
                """);

            migrationBuilder.AlterColumn<int>(
                name: "CompanyId",
                table: "Products",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyId_Name_ProductCategory_PackageSize",
                table: "Products",
                columns: new[] { "CompanyId", "Name", "ProductCategory", "PackageSize" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Companies_CompanyId",
                table: "Products",
                column: "CompanyId",
                principalTable: "Companies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Companies_CompanyId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_CompanyId_Name_ProductCategory_PackageSize",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Name",
                table: "Companies");

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Products",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE p
                SET CompanyName = ISNULL(c.Name, N'')
                FROM Products p
                LEFT JOIN Companies c ON c.Id = p.CompanyId;
                """);

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CompanyName_Name_ProductCategory_PackageSize",
                table: "Products",
                columns: new[] { "CompanyName", "Name", "ProductCategory", "PackageSize" },
                unique: true);
        }
    }
}
