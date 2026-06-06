using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public sealed class InventoryServiceTests
{
    [Fact]
    public async Task RunStockAuditAsync_AllowsBranchUserToAuditHomeBranch()
    {
        var factory = CreateFactory();
        await SeedAuditDataAsync(factory);
        var service = new InventoryService(factory);

        var result = await service.RunStockAuditAsync(
            userId: 10,
            warehouseId: 2,
            lines: [new AuditLineRequest(ProductId: 1, ActualQuantity: 7m, WarehouseId: 0)],
            notes: "home branch count");

        await using var db = factory.CreateDbContext();
        var movement = await db.StockMovements.SingleAsync(x => x.MovementType == StockMovementType.Adjust);
        Assert.Equal(1, result.AdjustedProductsCount);
        Assert.Equal(2, movement.ToWarehouseId);
        Assert.Null(movement.FromWarehouseId);
        Assert.Equal(2m, movement.Quantity);
    }

    [Fact]
    public async Task RunStockAuditAsync_RejectsBranchUserLineTargetingAnotherWarehouseWithoutWrites()
    {
        var factory = CreateFactory();
        await SeedAuditDataAsync(factory);
        var service = new InventoryService(factory);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunStockAuditAsync(
            userId: 10,
            warehouseId: 2,
            lines: [new AuditLineRequest(ProductId: 1, ActualQuantity: 0m, WarehouseId: 1)],
            notes: "cross warehouse tamper"));

        Assert.Contains("خارج فرعك", ex.Message);

        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.StockAudits.ToListAsync());
        Assert.Empty(await db.StockAuditLines.ToListAsync());
        Assert.DoesNotContain(await db.StockMovements.ToListAsync(), x => x.MovementType == StockMovementType.Adjust);
    }

    private static TestDbContextFactory CreateFactory()
    {
        var options = new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TestDbContextFactory(options);
    }

    private static async Task SeedAuditDataAsync(IDbContextFactory<OilChangePosDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();

        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.Products.Add(new Product
        {
            Id = 1,
            CompanyId = 1,
            Name = "Oil",
            ProductCategory = "Oil",
            PackageSize = "4L",
            UnitPrice = 10m,
            IsActive = true
        });
        db.Warehouses.AddRange(
            new Warehouse { Id = 1, Name = "Main", Type = WarehouseType.Main, IsActive = true },
            new Warehouse { Id = 2, Name = "Branch A", Type = WarehouseType.Branch, IsActive = true },
            new Warehouse { Id = 3, Name = "Branch B", Type = WarehouseType.Branch, IsActive = true });
        db.Users.Add(new AppUser
        {
            Id = 10,
            Username = "branch-user",
            PasswordHash = "unused",
            Role = UserRole.Cashier,
            IsActive = true,
            HomeBranchWarehouseId = 2
        });
        db.StockMovements.AddRange(
            new StockMovement
            {
                Id = 1,
                ProductId = 1,
                MovementType = StockMovementType.Purchase,
                Quantity = 10m,
                ToWarehouseId = 1,
                Notes = "main stock"
            },
            new StockMovement
            {
                Id = 2,
                ProductId = 1,
                MovementType = StockMovementType.Transfer,
                Quantity = 5m,
                ToWarehouseId = 2,
                Notes = "branch stock"
            });

        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<OilChangePosDbContext> options)
        : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext() => new(options);
    }
}
