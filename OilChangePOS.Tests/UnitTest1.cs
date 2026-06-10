using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public sealed class InventoryServiceStockAuditTests
{
    [Fact]
    public async Task RunStockAuditAsync_RejectsBranchLineOutsideActorsHomeBranch()
    {
        var options = CreateOptions();
        await SeedAuditScenarioAsync(options);
        var service = new InventoryService(new TestDbContextFactory(options));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunStockAuditAsync(
                UserIds.BranchManager,
                WarehouseIds.HomeBranch,
                [new AuditLineRequest(ProductIds.Oil, 5m, WarehouseIds.OtherBranch)],
                "cross-branch attempt"));

        Assert.Contains("خارج فرعك", ex.Message);

        await using var db = new OilChangePosDbContext(options);
        Assert.Empty(await db.StockAudits.ToListAsync());
        Assert.Empty(await db.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task RunStockAuditAsync_AllowsBranchLineForActorsHomeBranch()
    {
        var options = CreateOptions();
        await SeedAuditScenarioAsync(options);
        var service = new InventoryService(new TestDbContextFactory(options));

        var result = await service.RunStockAuditAsync(
            UserIds.BranchManager,
            WarehouseIds.HomeBranch,
            [new AuditLineRequest(ProductIds.Oil, 5m, 0)],
            "home-branch audit");

        Assert.Equal(1, result.AdjustedProductsCount);

        await using var db = new OilChangePosDbContext(options);
        var movement = Assert.Single(await db.StockMovements.ToListAsync());
        Assert.Equal(StockMovementType.Adjust, movement.MovementType);
        Assert.Equal(WarehouseIds.HomeBranch, movement.ToWarehouseId);
        Assert.Null(movement.FromWarehouseId);
        Assert.Equal(5m, movement.Quantity);
    }

    private static DbContextOptions<OilChangePosDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static async Task SeedAuditScenarioAsync(DbContextOptions<OilChangePosDbContext> options)
    {
        await using var db = new OilChangePosDbContext(options);

        db.Warehouses.AddRange(
            new Warehouse { Id = WarehouseIds.Main, Name = "Main", Type = WarehouseType.Main },
            new Warehouse { Id = WarehouseIds.HomeBranch, Name = "Home Branch", Type = WarehouseType.Branch },
            new Warehouse { Id = WarehouseIds.OtherBranch, Name = "Other Branch", Type = WarehouseType.Branch });

        db.Companies.Add(new Company { Id = 1, Name = "Acme" });
        db.Products.Add(new Product
        {
            Id = ProductIds.Oil,
            CompanyId = 1,
            Name = "Oil",
            ProductCategory = "Oil",
            PackageSize = "4L",
            UnitPrice = 25m,
            IsActive = true
        });

        db.Users.Add(new AppUser
        {
            Id = UserIds.BranchManager,
            Username = "home-branch-manager",
            PasswordHash = "not-used",
            Role = UserRole.Manager,
            IsActive = true,
            HomeBranchWarehouseId = WarehouseIds.HomeBranch
        });

        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<OilChangePosDbContext> options)
        : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext() => new(options);
    }

    private static class WarehouseIds
    {
        public const int Main = 1;
        public const int HomeBranch = 2;
        public const int OtherBranch = 3;
    }

    private static class ProductIds
    {
        public const int Oil = 10;
    }

    private static class UserIds
    {
        public const int BranchManager = 100;
    }
}
