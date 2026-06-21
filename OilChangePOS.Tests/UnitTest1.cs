using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public sealed class CriticalRegressionTests
{
    [Fact]
    public async Task CompleteSaleAsync_RejectsDuplicateLinesThatExceedAvailableStock()
    {
        var factory = CreateFactory();
        await SeedCatalogAsync(factory);

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = 1,
                MovementType = StockMovementType.Transfer,
                Quantity = 5m,
                ToWarehouseId = 2,
                Notes = "seed branch stock"
            });
            await db.SaveChangesAsync();
        }

        var service = new SalesService(factory);
        var request = new CompleteSaleRequest(
            CustomerId: null,
            DiscountAmount: 0m,
            UserId: 1,
            WarehouseId: 2,
            Items:
            [
                new SaleItemRequest(1, 3m),
                new SaleItemRequest(1, 3m)
            ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteSaleAsync(request));

        await using var verify = await factory.CreateDbContextAsync();
        Assert.Empty(await verify.Invoices.ToListAsync());
        Assert.Empty(await verify.StockMovements.Where(x => x.MovementType == StockMovementType.Sale).ToListAsync());
    }

    [Fact]
    public async Task RunStockAuditAsync_RejectsBranchLineWarehouseOverrideOutsideHomeBranch()
    {
        var factory = CreateFactory();
        await SeedCatalogAsync(factory, includeOtherBranch: true);

        var service = new InventoryService(factory);
        var lines = new List<AuditLineRequest>
        {
            new(ProductId: 1, ActualQuantity: 0m, WarehouseId: 3)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunStockAuditAsync(userId: 1, warehouseId: 2, lines, notes: string.Empty));

        await using var verify = await factory.CreateDbContextAsync();
        Assert.Empty(await verify.StockAudits.ToListAsync());
        Assert.Empty(await verify.StockMovements.Where(x => x.MovementType == StockMovementType.Adjust).ToListAsync());
    }

    [Fact]
    public async Task TransferStockBulkAsync_RejectsDuplicateProductWithConflictingBranchPrices()
    {
        var factory = CreateFactory();
        await SeedCatalogAsync(factory, includeAdmin: true);

        var service = new TransferService(factory);
        var request = new TransferStockBulkRequest(
            FromWarehouseId: 4,
            ToWarehouseId: 2,
            Notes: string.Empty,
            UserId: 9,
            Lines:
            [
                new TransferStockBulkLineRequest(1, 1m, 100m),
                new TransferStockBulkLineRequest(1, 1m, 90m)
            ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransferStockBulkAsync(request));
    }

    private static TestDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContextFactory(options);
    }

    private static async Task SeedCatalogAsync(
        TestDbContextFactory factory,
        bool includeOtherBranch = false,
        bool includeAdmin = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.Companies.Add(new Company { Id = 1, Name = "Acme", IsActive = true });
        db.Products.Add(new Product
        {
            Id = 1,
            CompanyId = 1,
            Name = "Oil",
            ProductCategory = "Oil",
            PackageSize = "1L",
            UnitPrice = 10m,
            IsActive = true
        });
        db.Warehouses.AddRange(
            new Warehouse { Id = 2, Name = "Branch", Type = WarehouseType.Branch, IsActive = true },
            new Warehouse { Id = 4, Name = "Main", Type = WarehouseType.Main, IsActive = true });
        if (includeOtherBranch)
            db.Warehouses.Add(new Warehouse { Id = 3, Name = "Other Branch", Type = WarehouseType.Branch, IsActive = true });

        db.Users.Add(new AppUser
        {
            Id = 1,
            Username = "cashier",
            PasswordHash = "hash",
            Role = UserRole.Cashier,
            IsActive = true,
            HomeBranchWarehouseId = 2
        });
        if (includeAdmin)
        {
            db.Users.Add(new AppUser
            {
                Id = 9,
                Username = "admin",
                PasswordHash = "hash",
                Role = UserRole.Admin,
                IsActive = true
            });
            db.StockMovements.Add(new StockMovement
            {
                ProductId = 1,
                MovementType = StockMovementType.Purchase,
                Quantity = 10m,
                ToWarehouseId = 4,
                Notes = "seed main stock"
            });
        }

        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<OilChangePosDbContext> options)
        : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext() => new(options);
    }
}
