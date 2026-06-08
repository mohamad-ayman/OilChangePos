using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public class TransferServiceTests
{
    [Fact]
    public async Task TransferStockBulkAsync_rejects_duplicate_sku_with_conflicting_branch_prices()
    {
        var service = new TransferService(new TestDbContextFactory(CreateOptions()));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransferStockBulkAsync(new TransferStockBulkRequest(
                FromWarehouseId: 1,
                ToWarehouseId: 2,
                Notes: "bulk",
                UserId: 1,
                Lines:
                [
                    new TransferStockBulkLineRequest(ProductId: 10, Quantity: 1, BranchSalePriceForDestination: 100m),
                    new TransferStockBulkLineRequest(ProductId: 10, Quantity: 1, BranchSalePriceForDestination: 120m)
                ])));

        Assert.Contains("أسعار بيع مختلفة", ex.Message);
    }

    [Fact]
    public async Task TransferStockBulkAsync_merges_duplicate_sku_when_branch_prices_match()
    {
        var options = CreateOptions();
        await SeedTransferDataAsync(options);

        var service = new TransferService(new TestDbContextFactory(options));

        var ids = await service.TransferStockBulkAsync(new TransferStockBulkRequest(
            FromWarehouseId: 1,
            ToWarehouseId: 2,
            Notes: "bulk",
            UserId: 1,
            Lines:
            [
                new TransferStockBulkLineRequest(ProductId: 10, Quantity: 4, BranchSalePriceForDestination: 100m),
                new TransferStockBulkLineRequest(ProductId: 10, Quantity: 6, BranchSalePriceForDestination: 100m)
            ]));

        await using var db = new OilChangePosDbContext(options);
        var transfer = await db.StockMovements.SingleAsync(x => x.MovementType == StockMovementType.Transfer);
        var branchPrice = await db.BranchProductPrices.SingleAsync();

        Assert.Single(ids);
        Assert.Equal(10m, transfer.Quantity);
        Assert.Equal(100m, branchPrice.SalePrice);
    }

    private static DbContextOptions<OilChangePosDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static async Task SeedTransferDataAsync(DbContextOptions<OilChangePosDbContext> options)
    {
        await using var db = new OilChangePosDbContext(options);
        db.Users.Add(new AppUser
        {
            Id = 1,
            Username = "admin",
            PasswordHash = "hash",
            Role = UserRole.Admin
        });
        db.Warehouses.AddRange(
            new Warehouse { Id = 1, Name = "Main", Type = WarehouseType.Main, IsActive = true },
            new Warehouse { Id = 2, Name = "Branch", Type = WarehouseType.Branch, IsActive = true });
        db.Companies.Add(new Company { Id = 1, Name = "Company", IsActive = true });
        db.Products.Add(new Product
        {
            Id = 10,
            CompanyId = 1,
            Name = "Oil",
            ProductCategory = "Oil",
            PackageSize = "1L",
            UnitPrice = 50m,
            IsActive = true
        });
        db.Purchases.Add(new Purchase
        {
            Id = 100,
            ProductId = 10,
            Quantity = 10m,
            PurchasePrice = 30m,
            ProductionDate = new DateTime(2026, 1, 1),
            PurchaseDate = new DateTime(2026, 1, 2),
            WarehouseId = 1,
            CreatedByUserId = 1
        });
        db.StockMovements.Add(new StockMovement
        {
            ProductId = 10,
            MovementType = StockMovementType.Purchase,
            Quantity = 10m,
            ToWarehouseId = 1,
            SourcePurchaseId = 100
        });
        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<OilChangePosDbContext> options)
        : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext() => new(options);
    }
}
