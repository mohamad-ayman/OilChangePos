using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public sealed class TransferServiceTests
{
    [Fact]
    public async Task TransferStockBulkAsync_RejectsDuplicateProductLinesWithConflictingBranchPrices()
    {
        var options = CreateOptions();
        await SeedScenarioAsync(options);
        var service = new TransferService(new TestDbContextFactory(options));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransferStockBulkAsync(
                new TransferStockBulkRequest(
                    FromWarehouseId: WarehouseIds.Main,
                    ToWarehouseId: WarehouseIds.Branch,
                    Notes: "bulk conflict",
                    UserId: UserIds.Admin,
                    Lines:
                    [
                        new TransferStockBulkLineRequest(ProductIds.Oil, 1m, 30m),
                        new TransferStockBulkLineRequest(ProductIds.Oil, 2m, 35m)
                    ])));

        await using var db = new OilChangePosDbContext(options);
        Assert.DoesNotContain(await db.StockMovements.ToListAsync(), m => m.MovementType == StockMovementType.Transfer);
        Assert.Empty(await db.BranchProductPrices.ToListAsync());
    }

    [Fact]
    public async Task TransferStockBulkAsync_MergesDuplicateProductLinesWithSameBranchPrice()
    {
        var options = CreateOptions();
        await SeedScenarioAsync(options);
        var service = new TransferService(new TestDbContextFactory(options));

        var movementIds = await service.TransferStockBulkAsync(
            new TransferStockBulkRequest(
                FromWarehouseId: WarehouseIds.Main,
                ToWarehouseId: WarehouseIds.Branch,
                Notes: "bulk merge",
                UserId: UserIds.Admin,
                Lines:
                [
                    new TransferStockBulkLineRequest(ProductIds.Oil, 1m, 30m),
                    new TransferStockBulkLineRequest(ProductIds.Oil, 2m, 30m)
                ]));

        Assert.Single(movementIds);

        await using var db = new OilChangePosDbContext(options);
        var movement = Assert.Single(await db.StockMovements.Where(m => m.MovementType == StockMovementType.Transfer).ToListAsync());
        Assert.Equal(3m, movement.Quantity);
        Assert.Equal(WarehouseIds.Main, movement.FromWarehouseId);
        Assert.Equal(WarehouseIds.Branch, movement.ToWarehouseId);

        var price = Assert.Single(await db.BranchProductPrices.ToListAsync());
        Assert.Equal(30m, price.SalePrice);
        Assert.Equal(ProductIds.Oil, price.ProductId);
        Assert.Equal(WarehouseIds.Branch, price.WarehouseId);
    }

    private static DbContextOptions<OilChangePosDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static async Task SeedScenarioAsync(DbContextOptions<OilChangePosDbContext> options)
    {
        await using var db = new OilChangePosDbContext(options);

        db.Warehouses.AddRange(
            new Warehouse { Id = WarehouseIds.Main, Name = "Main", Type = WarehouseType.Main },
            new Warehouse { Id = WarehouseIds.Branch, Name = "Branch", Type = WarehouseType.Branch });
        db.Companies.Add(new Company { Id = CompanyIds.Acme, Name = "Acme" });
        db.Products.Add(new Product
        {
            Id = ProductIds.Oil,
            CompanyId = CompanyIds.Acme,
            Name = "Oil",
            ProductCategory = "Oil",
            PackageSize = "4L",
            UnitPrice = 25m,
            IsActive = true
        });
        db.Users.Add(new AppUser
        {
            Id = UserIds.Admin,
            Username = "admin",
            PasswordHash = "not-used",
            Role = UserRole.Admin,
            IsActive = true
        });
        db.StockMovements.Add(new StockMovement
        {
            ProductId = ProductIds.Oil,
            MovementType = StockMovementType.Purchase,
            Quantity = 10m,
            ToWarehouseId = WarehouseIds.Main,
            Notes = "seed main stock"
        });

        await db.SaveChangesAsync();
    }

    private static class WarehouseIds
    {
        public const int Main = 1;
        public const int Branch = 2;
    }

    private static class CompanyIds
    {
        public const int Acme = 10;
    }

    private static class ProductIds
    {
        public const int Oil = 100;
    }

    private static class UserIds
    {
        public const int Admin = 1000;
    }
}
