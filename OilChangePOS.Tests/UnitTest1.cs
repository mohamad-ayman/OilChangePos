using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public sealed class CriticalInventoryRegressionTests
{
    [Fact]
    public async Task CompleteSaleAsync_RejectsDuplicateLinesThatExceedCombinedStock()
    {
        using var factory = new SqliteDbContextFactory();
        var ids = await SeedBaseDataAsync(factory, branchOneStock: 10m);
        var service = new SalesService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CompleteSaleAsync(
            new CompleteSaleRequest(
                CustomerId: null,
                DiscountAmount: 0m,
                UserId: ids.BranchOneManagerId,
                WarehouseId: ids.BranchOneWarehouseId,
                Items:
                [
                    new SaleItemRequest(ids.ProductId, 6m),
                    new SaleItemRequest(ids.ProductId, 6m)
                ])));

        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.Invoices.ToListAsync());
        Assert.Empty(await db.StockMovements.Where(x => x.MovementType == StockMovementType.Sale).ToListAsync());
    }

    [Fact]
    public async Task RunStockAuditAsync_RejectsBranchUserLineOverrideForAnotherBranch()
    {
        using var factory = new SqliteDbContextFactory();
        var ids = await SeedBaseDataAsync(factory, branchTwoStock: 5m);
        var service = new InventoryService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunStockAuditAsync(
            ids.BranchOneManagerId,
            ids.BranchOneWarehouseId,
            [new AuditLineRequest(ids.ProductId, ActualQuantity: 0m, WarehouseId: ids.BranchTwoWarehouseId)],
            "unauthorized override"));

        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.StockAudits.ToListAsync());
        Assert.Empty(await db.StockMovements.Where(x => x.MovementType == StockMovementType.Adjust).ToListAsync());
    }

    [Fact]
    public async Task TransferStockBulkAsync_RejectsDuplicateSkuWithConflictingBranchPrices()
    {
        using var factory = new SqliteDbContextFactory();
        var ids = await SeedBaseDataAsync(factory, mainStock: 10m);
        var service = new TransferService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransferStockBulkAsync(
            new TransferStockBulkRequest(
                ids.MainWarehouseId,
                ids.BranchOneWarehouseId,
                "bulk",
                ids.AdminUserId,
                [
                    new TransferStockBulkLineRequest(ids.ProductId, 1m, 10m),
                    new TransferStockBulkLineRequest(ids.ProductId, 1m, 12m)
                ])));

        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.BranchProductPrices.ToListAsync());
        Assert.Empty(await db.StockMovements.Where(x => x.MovementType == StockMovementType.Transfer && x.ToWarehouseId == ids.BranchOneWarehouseId).ToListAsync());
    }

    [Fact]
    public async Task FulfillAsync_RollsBackStatusClaimWhenTransferFails()
    {
        using var factory = new SqliteDbContextFactory();
        var ids = await SeedBaseDataAsync(factory, mainStock: 2m, branchRequestQuantity: 5m);
        var service = new BranchStockRequestService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FulfillAsync(ids.AdminUserId, ids.BranchStockRequestId));

        await using var db = factory.CreateDbContext();
        var request = await db.BranchStockRequests.SingleAsync();
        Assert.Equal(BranchStockRequestStatus.Pending, request.Status);
        Assert.Null(request.FulfillmentStockMovementId);
        Assert.Empty(await db.StockMovements.Where(x => x.MovementType == StockMovementType.Transfer && x.ToWarehouseId == ids.BranchOneWarehouseId).ToListAsync());
    }

    private static async Task<TestIds> SeedBaseDataAsync(
        IDbContextFactory<OilChangePosDbContext> factory,
        decimal mainStock = 0m,
        decimal branchOneStock = 0m,
        decimal branchTwoStock = 0m,
        decimal? branchRequestQuantity = null)
    {
        await using var db = factory.CreateDbContext();

        var company = new Company { Name = "TestCo" };
        var product = new Product
        {
            Company = company,
            Name = "Oil",
            ProductCategory = "Oil",
            PackageSize = "4L",
            UnitPrice = 20m,
            IsActive = true
        };
        var main = new Warehouse { Name = "Main", Type = WarehouseType.Main };
        var branchOne = new Warehouse { Name = "Branch 1", Type = WarehouseType.Branch };
        var branchTwo = new Warehouse { Name = "Branch 2", Type = WarehouseType.Branch };
        var admin = new AppUser { Username = "admin", PasswordHash = "x", Role = UserRole.Admin, IsActive = true };
        var manager = new AppUser { Username = "manager", PasswordHash = "x", Role = UserRole.Manager, IsActive = true, HomeBranchWarehouse = branchOne };

        db.AddRange(company, product, main, branchOne, branchTwo, admin, manager);
        await db.SaveChangesAsync();

        BranchStockRequest? stockRequest = null;
        if (branchRequestQuantity is { } requestQuantity)
        {
            stockRequest = new BranchStockRequest
            {
                BranchWarehouseId = branchOne.Id,
                ProductId = product.Id,
                Quantity = requestQuantity,
                Notes = "needed",
                Status = BranchStockRequestStatus.Pending,
                RequestedByUserId = manager.Id
            };
            db.BranchStockRequests.Add(stockRequest);
            await db.SaveChangesAsync();
        }

        if (mainStock > 0m || branchOneStock > 0m || branchTwoStock > 0m)
        {
            var purchase = new Purchase
            {
                ProductId = product.Id,
                Quantity = mainStock + branchOneStock + branchTwoStock,
                PurchasePrice = 5m,
                ProductionDate = DateTime.UtcNow.Date,
                PurchaseDate = DateTime.UtcNow,
                WarehouseId = main.Id,
                CreatedByUserId = admin.Id,
                Notes = "seed"
            };
            db.Purchases.Add(purchase);
            await db.SaveChangesAsync();

            if (mainStock > 0m)
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    MovementType = StockMovementType.Purchase,
                    Quantity = mainStock,
                    ToWarehouseId = main.Id,
                    SourcePurchaseId = purchase.Id,
                    Notes = "seed main"
                });
            }

            if (branchOneStock > 0m)
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    MovementType = StockMovementType.Transfer,
                    Quantity = branchOneStock,
                    FromWarehouseId = main.Id,
                    ToWarehouseId = branchOne.Id,
                    SourcePurchaseId = purchase.Id,
                    Notes = "seed branch one"
                });
            }

            if (branchTwoStock > 0m)
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    MovementType = StockMovementType.Transfer,
                    Quantity = branchTwoStock,
                    FromWarehouseId = main.Id,
                    ToWarehouseId = branchTwo.Id,
                    SourcePurchaseId = purchase.Id,
                    Notes = "seed branch two"
                });
            }

            await db.SaveChangesAsync();
        }

        return new TestIds(
            product.Id,
            main.Id,
            branchOne.Id,
            branchTwo.Id,
            admin.Id,
            manager.Id,
            stockRequest?.Id ?? 0);
    }

    private sealed record TestIds(
        int ProductId,
        int MainWarehouseId,
        int BranchOneWarehouseId,
        int BranchTwoWarehouseId,
        int AdminUserId,
        int BranchOneManagerId,
        int BranchStockRequestId);

    private sealed class SqliteDbContextFactory : IDbContextFactory<OilChangePosDbContext>, IDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        private readonly DbContextOptions<OilChangePosDbContext> options;

        public SqliteDbContextFactory()
        {
            connection.Open();
            options = new DbContextOptionsBuilder<OilChangePosDbContext>()
                .UseSqlite(connection)
                .Options;

            using var db = CreateDbContext();
            db.Database.EnsureCreated();
        }

        public OilChangePosDbContext CreateDbContext() => new(options);

        public void Dispose() => connection.Dispose();
    }
}
