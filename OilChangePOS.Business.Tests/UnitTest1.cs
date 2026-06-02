using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business.Tests;

public class TransferServiceTests
{
    [Fact]
    public async Task TransferStockBulkAsync_rejects_conflicting_duplicate_branch_sale_prices_without_writes()
    {
        var options = CreateOptions();
        await SeedTransferDataAsync(options);
        var service = new TransferService(new TestDbContextFactory(options));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransferStockBulkAsync(new TransferStockBulkRequest(
                FromWarehouseId: 1,
                ToWarehouseId: 2,
                Notes: "bulk",
                UserId: 7,
                Lines:
                [
                    new TransferStockBulkLineRequest(1, 1m, 11m),
                    new TransferStockBulkLineRequest(1, 2m, 12m)
                ])));

        Assert.Contains("أسعار بيع مختلفة", ex.Message);

        await using var db = new OilChangePosDbContext(options);
        Assert.Equal(0, await db.StockMovements.CountAsync(x => x.MovementType == StockMovementType.Transfer));
        Assert.Equal(0, await db.BranchProductPrices.CountAsync());
    }

    private static DbContextOptions<OilChangePosDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task SeedTransferDataAsync(DbContextOptions<OilChangePosDbContext> options)
    {
        await using var db = new OilChangePosDbContext(options);
        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.Products.Add(new Product
        {
            Id = 1,
            CompanyId = 1,
            Name = "Oil",
            ProductCategory = "Engine Oil",
            PackageSize = "4L",
            UnitPrice = 20m
        });
        db.Warehouses.AddRange(
            new Warehouse { Id = 1, Name = "Main", Type = WarehouseType.Main },
            new Warehouse { Id = 2, Name = "Branch", Type = WarehouseType.Branch });
        db.Users.Add(new AppUser
        {
            Id = 7,
            Username = "admin",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true
        });
        db.Purchases.Add(new Purchase
        {
            Id = 1,
            ProductId = 1,
            Quantity = 10m,
            PurchasePrice = 5m,
            ProductionDate = new DateTime(2026, 1, 1),
            PurchaseDate = new DateTime(2026, 1, 2),
            WarehouseId = 1,
            CreatedByUserId = 7,
            Notes = "seed"
        });
        db.StockMovements.Add(new StockMovement
        {
            ProductId = 1,
            MovementType = StockMovementType.Purchase,
            Quantity = 10m,
            ToWarehouseId = 1,
            Notes = "seed"
        });
        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<OilChangePosDbContext> options)
        : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext() => new(options);
    }
}
