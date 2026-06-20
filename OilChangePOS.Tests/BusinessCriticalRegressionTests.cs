using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public sealed class BusinessCriticalRegressionTests
{
    [Fact]
    public async Task CompleteSaleAsync_RejectsDuplicateSkuOverdrawAndDoesNotWriteInvoice()
    {
        using var db = new TestDatabase();
        var ids = await db.SeedBranchSaleAsync(onHandAtBranch: 10m);
        var service = new SalesService(db.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSaleAsync(new CompleteSaleRequest(
                CustomerId: null,
                DiscountAmount: 0m,
                UserId: ids.BranchUserId,
                WarehouseId: ids.BranchWarehouseId,
                Items:
                [
                    new SaleItemRequest(ids.ProductId, 6m),
                    new SaleItemRequest(ids.ProductId, 6m)
                ])));

        await using var check = db.CreateContext();
        Assert.Empty(await check.Invoices.ToListAsync());
        Assert.Equal(10m, await WarehouseOnHandAsync(check, ids.ProductId, ids.BranchWarehouseId));
    }

    [Fact]
    public async Task CompleteSaleAsync_RejectsInactiveUser()
    {
        using var db = new TestDatabase();
        var ids = await db.SeedBranchSaleAsync(onHandAtBranch: 10m, branchUserActive: false);
        var service = new SalesService(db.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSaleAsync(new CompleteSaleRequest(
                CustomerId: null,
                DiscountAmount: 0m,
                UserId: ids.BranchUserId,
                WarehouseId: ids.BranchWarehouseId,
                Items: [new SaleItemRequest(ids.ProductId, 1m)])));

        await using var check = db.CreateContext();
        Assert.Empty(await check.Invoices.ToListAsync());
    }

    [Fact]
    public async Task RunStockAuditAsync_RejectsBranchLineForOtherWarehouse()
    {
        using var db = new TestDatabase();
        var ids = await db.SeedBranchSaleAsync(onHandAtBranch: 10m);
        var otherBranchId = await db.AddBranchAsync("Branch 2");
        var service = new InventoryService(db.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunStockAuditAsync(
                ids.BranchUserId,
                ids.BranchWarehouseId,
                [new AuditLineRequest(ids.ProductId, 5m, otherBranchId)],
                "cross-branch attempt"));

        await using var check = db.CreateContext();
        Assert.Empty(await check.StockAudits.ToListAsync());
        Assert.Empty(await check.StockMovements.Where(x => x.MovementType == StockMovementType.Adjust).ToListAsync());
    }

    [Fact]
    public async Task RunStockAuditAsync_RejectsDuplicateProductForSameWarehouse()
    {
        using var db = new TestDatabase();
        var ids = await db.SeedBranchSaleAsync(onHandAtBranch: 10m);
        var service = new InventoryService(db.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunStockAuditAsync(
                ids.AdminUserId,
                ids.BranchWarehouseId,
                [
                    new AuditLineRequest(ids.ProductId, 5m, 0),
                    new AuditLineRequest(ids.ProductId, 5m, ids.BranchWarehouseId)
                ],
                "duplicate line"));

        await using var check = db.CreateContext();
        Assert.Empty(await check.StockAudits.ToListAsync());
        Assert.Equal(10m, await WarehouseOnHandAsync(check, ids.ProductId, ids.BranchWarehouseId));
    }

    [Fact]
    public async Task FulfillAsync_RollsBackStatusWhenTransferFails()
    {
        using var db = new TestDatabase();
        var ids = await db.SeedBranchSaleAsync(onHandAtBranch: 0m, onHandAtMain: 2m);
        var requestId = await db.AddBranchStockRequestAsync(ids.BranchWarehouseId, ids.ProductId, quantity: 5m, ids.BranchUserId);
        var service = new BranchStockRequestService(db.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FulfillAsync(ids.AdminUserId, requestId));

        await using var check = db.CreateContext();
        var request = await check.BranchStockRequests.SingleAsync(x => x.Id == requestId);
        Assert.Equal(BranchStockRequestStatus.Pending, request.Status);
        Assert.Null(request.FulfillmentStockMovementId);
        Assert.Equal(2m, await WarehouseOnHandAsync(check, ids.ProductId, ids.MainWarehouseId));
    }

    [Fact]
    public async Task UpdateUserAsync_RollsBackWhenSoleAdminWouldBeDemoted()
    {
        using var db = new TestDatabase();
        var ids = await db.SeedBranchSaleAsync(onHandAtBranch: 0m);
        var service = new UserManagementService(db.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateUserAsync(
                ids.AdminUserId,
                ids.AdminUserId,
                UserRole.Manager,
                isActive: true,
                homeBranchWarehouseId: ids.BranchWarehouseId));

        await using var check = db.CreateContext();
        var admin = await check.Users.SingleAsync(x => x.Id == ids.AdminUserId);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(admin.IsActive);
        Assert.Null(admin.HomeBranchWarehouseId);
    }

    [Fact]
    public async Task TransferStockBulkAsync_RejectsConflictingDuplicateBranchPrices()
    {
        using var db = new TestDatabase();
        var ids = await db.SeedBranchSaleAsync(onHandAtBranch: 0m, onHandAtMain: 20m);
        var service = new TransferService(db.Factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransferStockBulkAsync(new TransferStockBulkRequest(
                ids.MainWarehouseId,
                ids.BranchWarehouseId,
                "bulk",
                ids.AdminUserId,
                [
                    new TransferStockBulkLineRequest(ids.ProductId, 1m, 10m),
                    new TransferStockBulkLineRequest(ids.ProductId, 1m, 12m)
                ])));

        await using var check = db.CreateContext();
        Assert.Empty(await check.BranchProductPrices.ToListAsync());
        Assert.Equal(20m, await WarehouseOnHandAsync(check, ids.ProductId, ids.MainWarehouseId));
    }

    private static async Task<decimal> WarehouseOnHandAsync(OilChangePosDbContext db, int productId, int warehouseId)
    {
        var inbound = await db.StockMovements
            .Where(x => x.ProductId == productId && x.ToWarehouseId == warehouseId)
            .SumAsync(x => (decimal?)x.Quantity) ?? 0m;
        var outbound = await db.StockMovements
            .Where(x => x.ProductId == productId && x.FromWarehouseId == warehouseId)
            .SumAsync(x => (decimal?)x.Quantity) ?? 0m;
        return inbound - outbound;
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly DbContextOptions<OilChangePosDbContext> _options;

        public TestDatabase()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<OilChangePosDbContext>()
                .UseSqlite(_connection)
                .Options;
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public IDbContextFactory<OilChangePosDbContext> Factory => new TestDbContextFactory(_options);

        public OilChangePosDbContext CreateContext() => new(_options);

        public async Task<SeedIds> SeedBranchSaleAsync(
            decimal onHandAtBranch,
            decimal onHandAtMain = 0m,
            bool branchUserActive = true)
        {
            await using var db = CreateContext();
            var main = new Warehouse { Name = "Main", Type = WarehouseType.Main, IsActive = true };
            var branch = new Warehouse { Name = "Branch 1", Type = WarehouseType.Branch, IsActive = true };
            var admin = new AppUser { Username = "admin", PasswordHash = "hash", Role = UserRole.Admin, IsActive = true };
            var branchUser = new AppUser
            {
                Username = "branch",
                PasswordHash = "hash",
                Role = UserRole.Manager,
                IsActive = branchUserActive,
                HomeBranchWarehouse = branch
            };
            var company = new Company { Name = "Co", IsActive = true };
            var product = new Product
            {
                Company = company,
                Name = "Oil",
                ProductCategory = "Oil",
                PackageSize = "4L",
                UnitPrice = 25m,
                IsActive = true
            };

            db.AddRange(main, branch, admin, branchUser, company, product);
            await db.SaveChangesAsync();

            if (onHandAtBranch > 0)
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    MovementType = StockMovementType.Transfer,
                    Quantity = onHandAtBranch,
                    ToWarehouseId = branch.Id,
                    Notes = "seed branch stock"
                });
            }

            if (onHandAtMain > 0)
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    MovementType = StockMovementType.Purchase,
                    Quantity = onHandAtMain,
                    ToWarehouseId = main.Id,
                    Notes = "seed main stock"
                });
            }

            await db.SaveChangesAsync();
            return new SeedIds(admin.Id, branchUser.Id, main.Id, branch.Id, product.Id);
        }

        public async Task<int> AddBranchAsync(string name)
        {
            await using var db = CreateContext();
            var branch = new Warehouse { Name = name, Type = WarehouseType.Branch, IsActive = true };
            db.Warehouses.Add(branch);
            await db.SaveChangesAsync();
            return branch.Id;
        }

        public async Task<int> AddBranchStockRequestAsync(int branchWarehouseId, int productId, decimal quantity, int requestedByUserId)
        {
            await using var db = CreateContext();
            var request = new BranchStockRequest
            {
                BranchWarehouseId = branchWarehouseId,
                ProductId = productId,
                Quantity = quantity,
                Notes = "seed request",
                Status = BranchStockRequestStatus.Pending,
                RequestedByUserId = requestedByUserId,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.BranchStockRequests.Add(request);
            await db.SaveChangesAsync();
            return request.Id;
        }

        public void Dispose() => _connection.Dispose();
    }

    private sealed record SeedIds(int AdminUserId, int BranchUserId, int MainWarehouseId, int BranchWarehouseId, int ProductId);

    private sealed class TestDbContextFactory(DbContextOptions<OilChangePosDbContext> options) : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext() => new(options);
    }
}
