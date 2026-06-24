using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business.Tests;

public sealed class CriticalInventoryRegressionTests
{
    [Fact]
    public async Task RunStockAuditAsync_rejects_branch_line_for_another_warehouse()
    {
        using var factory = new SqliteDbContextFactory();
        var seed = await SeedBasicCatalogAsync(factory);
        await using (var db = factory.CreateDbContext())
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = seed.ProductId,
                MovementType = StockMovementType.Purchase,
                Quantity = 10m,
                ToWarehouseId = seed.OtherBranchId,
                Notes = "seed"
            });
            await db.SaveChangesAsync();
        }

        var service = new InventoryService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunStockAuditAsync(
                seed.BranchUserId,
                seed.BranchId,
                [new AuditLineRequest(seed.ProductId, 0m, seed.OtherBranchId)],
                "cross branch adjustment"));

        await using var verify = factory.CreateDbContext();
        Assert.Empty(await verify.StockAudits.ToListAsync());
        Assert.Equal(10m, await StockForWarehouseAsync(verify, seed.ProductId, seed.OtherBranchId));
    }

    [Fact]
    public async Task CompleteSaleAsync_rejects_duplicate_lines_that_exceed_on_hand()
    {
        using var factory = new SqliteDbContextFactory();
        var seed = await SeedBasicCatalogAsync(factory);
        await using (var db = factory.CreateDbContext())
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = seed.ProductId,
                MovementType = StockMovementType.Purchase,
                Quantity = 5m,
                ToWarehouseId = seed.BranchId,
                Notes = "seed"
            });
            await db.SaveChangesAsync();
        }

        var service = new SalesService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSaleAsync(new CompleteSaleRequest(
                null,
                0m,
                seed.BranchUserId,
                seed.BranchId,
                [
                    new SaleItemRequest(seed.ProductId, 3m),
                    new SaleItemRequest(seed.ProductId, 3m)
                ])));

        await using var verify = factory.CreateDbContext();
        Assert.Empty(await verify.Invoices.ToListAsync());
        Assert.Equal(5m, await StockForWarehouseAsync(verify, seed.ProductId, seed.BranchId));
    }

    [Fact]
    public async Task CreateOilChangeServiceAsync_rejects_duplicate_details_that_exceed_on_hand()
    {
        using var factory = new SqliteDbContextFactory();
        var seed = await SeedBasicCatalogAsync(factory);
        await using (var db = factory.CreateDbContext())
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = seed.ProductId,
                MovementType = StockMovementType.Purchase,
                Quantity = 5m,
                ToWarehouseId = seed.BranchId,
                Notes = "seed"
            });
            await db.SaveChangesAsync();
        }

        var service = new ServiceOrderService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOilChangeServiceAsync(new OilChangeRequest(
                seed.CustomerId,
                seed.CarId,
                12345,
                seed.BranchUserId,
                seed.BranchId,
                [
                    new SaleItemRequest(seed.ProductId, 3m),
                    new SaleItemRequest(seed.ProductId, 3m)
                ])));

        await using var verify = factory.CreateDbContext();
        Assert.Empty(await verify.ServiceOrders.ToListAsync());
        Assert.Equal(5m, await StockForWarehouseAsync(verify, seed.ProductId, seed.BranchId));
    }

    [Fact]
    public async Task TransferStockBulkAsync_rejects_conflicting_duplicate_destination_prices()
    {
        using var factory = new SqliteDbContextFactory();
        var seed = await SeedBasicCatalogAsync(factory);
        await using (var db = factory.CreateDbContext())
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = seed.ProductId,
                MovementType = StockMovementType.Purchase,
                Quantity = 10m,
                ToWarehouseId = seed.MainWarehouseId,
                Notes = "seed"
            });
            await db.SaveChangesAsync();
        }

        var service = new TransferService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransferStockBulkAsync(new TransferStockBulkRequest(
                seed.MainWarehouseId,
                seed.BranchId,
                "bulk",
                seed.AdminUserId,
                [
                    new TransferStockBulkLineRequest(seed.ProductId, 1m, 10m),
                    new TransferStockBulkLineRequest(seed.ProductId, 1m, 12m)
                ])));

        await using var verify = factory.CreateDbContext();
        Assert.Empty(await verify.BranchProductPrices.ToListAsync());
        Assert.Empty(await verify.StockMovements.Where(m => m.MovementType == StockMovementType.Transfer).ToListAsync());
        Assert.Equal(10m, await StockForWarehouseAsync(verify, seed.ProductId, seed.MainWarehouseId));
    }

    private static async Task<SeedIds> SeedBasicCatalogAsync(SqliteDbContextFactory factory)
    {
        await using var db = factory.CreateDbContext();

        var company = new Company { Name = "Acme", IsActive = true };
        var product = new Product
        {
            Company = company,
            Name = "Oil",
            ProductCategory = "Oil",
            PackageSize = "4L",
            UnitPrice = 20m,
            IsActive = true
        };
        var main = new Warehouse { Name = "Main", Type = WarehouseType.Main, IsActive = true };
        var branch = new Warehouse { Name = "Branch A", Type = WarehouseType.Branch, IsActive = true };
        var otherBranch = new Warehouse { Name = "Branch B", Type = WarehouseType.Branch, IsActive = true };
        var admin = new AppUser { Username = "admin", PasswordHash = "x", Role = UserRole.Admin, IsActive = true };
        var branchUser = new AppUser
        {
            Username = "branch",
            PasswordHash = "x",
            Role = UserRole.Manager,
            IsActive = true,
            HomeBranchWarehouse = branch
        };
        var customer = new Customer { FullName = "Customer", PhoneNumber = "0500000000" };
        var car = new Car { Customer = customer, PlateNumber = "ABC123", Make = "Make", Model = "Model" };

        db.AddRange(company, product, main, branch, otherBranch, admin, branchUser, customer, car);
        await db.SaveChangesAsync();

        return new SeedIds(
            product.Id,
            main.Id,
            branch.Id,
            otherBranch.Id,
            admin.Id,
            branchUser.Id,
            customer.Id,
            car.Id);
    }

    private static async Task<decimal> StockForWarehouseAsync(OilChangePosDbContext db, int productId, int warehouseId)
    {
        var inQty = await db.StockMovements
            .Where(m => m.ProductId == productId && m.ToWarehouseId == warehouseId && m.Quantity > 0)
            .SumAsync(m => (decimal?)m.Quantity) ?? 0m;
        var outQty = await db.StockMovements
            .Where(m => m.ProductId == productId && m.FromWarehouseId == warehouseId && m.Quantity > 0)
            .SumAsync(m => (decimal?)m.Quantity) ?? 0m;

        return inQty - outQty;
    }

    private sealed record SeedIds(
        int ProductId,
        int MainWarehouseId,
        int BranchId,
        int OtherBranchId,
        int AdminUserId,
        int BranchUserId,
        int CustomerId,
        int CarId);

    private sealed class SqliteDbContextFactory : IDbContextFactory<OilChangePosDbContext>, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<OilChangePosDbContext> _options;

        public SqliteDbContextFactory()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _options = new DbContextOptionsBuilder<OilChangePosDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var db = CreateDbContext();
            db.Database.EnsureCreated();
        }

        public OilChangePosDbContext CreateDbContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
