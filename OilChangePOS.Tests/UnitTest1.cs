using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public class CriticalRegressionTests
{
    [Fact]
    public async Task BranchStockAuditRejectsLineWarehouseOutsideActorHomeBranch()
    {
        await using var testDb = await TestDatabase.CreateAsync();
        var seed = await SeedCoreAsync(testDb.Factory);

        var service = new InventoryService(testDb.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunStockAuditAsync(
                seed.BranchUserId,
                seed.BranchWarehouseId,
                [new AuditLineRequest(seed.ProductId, 7m, seed.OtherBranchWarehouseId)],
                "unauthorized line override"));

        await using var db = await testDb.Factory.CreateDbContextAsync();
        Assert.Empty(await db.StockMovements.Where(m => m.MovementType == StockMovementType.Adjust).ToListAsync());
    }

    [Fact]
    public async Task SaleDuplicateLinesAreValidatedAgainstAggregateStock()
    {
        await using var testDb = await TestDatabase.CreateAsync();
        var seed = await SeedCoreAsync(testDb.Factory);
        await SeedBranchStockAsync(testDb.Factory, seed.ProductId, seed.BranchWarehouseId, 5m);

        var service = new SalesService(testDb.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSaleAsync(new CompleteSaleRequest(
                CustomerId: null,
                DiscountAmount: 0m,
                UserId: seed.BranchUserId,
                WarehouseId: seed.BranchWarehouseId,
                Items:
                [
                    new SaleItemRequest(seed.ProductId, 3m),
                    new SaleItemRequest(seed.ProductId, 3m)
                ])));

        await using var db = await testDb.Factory.CreateDbContextAsync();
        Assert.Empty(await db.Invoices.ToListAsync());
        Assert.Equal(5m, await CurrentStockAsync(db, seed.ProductId, seed.BranchWarehouseId));
    }

    [Fact]
    public async Task SaleRejectsDiscountsThatWouldCreateNegativeInvoiceTotals()
    {
        await using var testDb = await TestDatabase.CreateAsync();
        var seed = await SeedCoreAsync(testDb.Factory);
        await SeedBranchStockAsync(testDb.Factory, seed.ProductId, seed.BranchWarehouseId, 5m);

        var service = new SalesService(testDb.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSaleAsync(new CompleteSaleRequest(
                CustomerId: null,
                DiscountAmount: 999m,
                UserId: seed.BranchUserId,
                WarehouseId: seed.BranchWarehouseId,
                Items: [new SaleItemRequest(seed.ProductId, 1m)])));

        await using var db = await testDb.Factory.CreateDbContextAsync();
        Assert.Empty(await db.Invoices.ToListAsync());
    }

    [Fact]
    public async Task LastActiveAdminCannotBeDemotedAndRollbackKeepsAdmin()
    {
        await using var testDb = await TestDatabase.CreateAsync();
        var seed = await SeedCoreAsync(testDb.Factory);

        var service = new UserManagementService(testDb.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateUserAsync(
                seed.AdminUserId,
                seed.AdminUserId,
                UserRole.Manager,
                isActive: true,
                homeBranchWarehouseId: seed.BranchWarehouseId));

        await using var db = await testDb.Factory.CreateDbContextAsync();
        var admin = await db.Users.SingleAsync(u => u.Id == seed.AdminUserId);
        Assert.True(admin.IsActive);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Null(admin.HomeBranchWarehouseId);
    }

    [Fact]
    public async Task BulkTransferRejectsConflictingDuplicateBranchPrices()
    {
        await using var testDb = await TestDatabase.CreateAsync();
        var seed = await SeedCoreAsync(testDb.Factory);

        var service = new TransferService(testDb.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransferStockBulkAsync(new TransferStockBulkRequest(
                seed.MainWarehouseId,
                seed.BranchWarehouseId,
                "conflicting duplicate prices",
                seed.AdminUserId,
                [
                    new TransferStockBulkLineRequest(seed.ProductId, 1m, 10m),
                    new TransferStockBulkLineRequest(seed.ProductId, 1m, 20m)
                ])));
    }

    private static async Task<SeedIds> SeedCoreAsync(IDbContextFactory<OilChangePosDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        var company = new Company { Name = "Acme" };
        var product = new Product
        {
            Company = company,
            Name = "Oil",
            ProductCategory = "Oil",
            PackageSize = "4L",
            UnitPrice = 100m,
            IsActive = true
        };
        var mainWarehouse = new Warehouse { Name = "Main", Type = WarehouseType.Main, IsActive = true };
        var branchWarehouse = new Warehouse { Name = "Branch A", Type = WarehouseType.Branch, IsActive = true };
        var otherBranchWarehouse = new Warehouse { Name = "Branch B", Type = WarehouseType.Branch, IsActive = true };
        db.AddRange(company, product, mainWarehouse, branchWarehouse, otherBranchWarehouse);
        await db.SaveChangesAsync();

        var admin = new AppUser
        {
            Username = "admin",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true
        };
        var branchUser = new AppUser
        {
            Username = "branch",
            PasswordHash = "hash",
            Role = UserRole.Cashier,
            IsActive = true,
            HomeBranchWarehouseId = branchWarehouse.Id
        };
        db.Users.AddRange(admin, branchUser);
        await db.SaveChangesAsync();

        return new SeedIds(
            product.Id,
            admin.Id,
            branchUser.Id,
            mainWarehouse.Id,
            branchWarehouse.Id,
            otherBranchWarehouse.Id);
    }

    private static async Task SeedBranchStockAsync(
        IDbContextFactory<OilChangePosDbContext> factory,
        int productId,
        int warehouseId,
        decimal quantity)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.StockMovements.Add(new StockMovement
        {
            ProductId = productId,
            MovementType = StockMovementType.Purchase,
            Quantity = quantity,
            ToWarehouseId = warehouseId,
            Notes = "seed"
        });
        await db.SaveChangesAsync();
    }

    private static async Task<decimal> CurrentStockAsync(OilChangePosDbContext db, int productId, int warehouseId)
    {
        var inQty = await db.StockMovements
            .Where(x => x.ProductId == productId && x.ToWarehouseId == warehouseId)
            .SumAsync(x => (decimal?)x.Quantity) ?? 0m;
        var outQty = await db.StockMovements
            .Where(x => x.ProductId == productId && x.FromWarehouseId == warehouseId)
            .SumAsync(x => (decimal?)x.Quantity) ?? 0m;
        return inQty - outQty;
    }

    private sealed record SeedIds(
        int ProductId,
        int AdminUserId,
        int BranchUserId,
        int MainWarehouseId,
        int BranchWarehouseId,
        int OtherBranchWarehouseId);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, IDbContextFactory<OilChangePosDbContext> factory)
        {
            _connection = connection;
            Factory = factory;
        }

        public IDbContextFactory<OilChangePosDbContext> Factory { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<OilChangePosDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var db = new OilChangePosDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, new TestDbContextFactory(options));
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<OilChangePosDbContext> options)
        : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext() => new(options);
    }
}
