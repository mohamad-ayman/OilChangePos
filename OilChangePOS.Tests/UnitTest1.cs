using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public class DuplicateSaleLineStockTests
{
    [Fact]
    public async Task CompleteSaleAsync_rejects_duplicate_lines_when_combined_quantity_exceeds_stock()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using (var db = CreateContext(dbName))
        {
            SeedSaleData(db);
            await db.SaveChangesAsync();
        }

        var service = new SalesService(new TestDbContextFactory(dbName));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSaleAsync(new CompleteSaleRequest(
                CustomerId: null,
                DiscountAmount: 0m,
                UserId: 1,
                WarehouseId: 1,
                Items:
                [
                    new SaleItemRequest(1, 3m),
                    new SaleItemRequest(1, 3m)
                ])));

        Assert.Contains("رصيد", ex.Message);
        await using var verifyDb = CreateContext(dbName);
        Assert.Empty(await verifyDb.Invoices.ToListAsync());
        Assert.Equal(5m, await CurrentStockAsync(verifyDb, productId: 1, warehouseId: 1));
    }

    [Fact]
    public async Task CreateOilChangeServiceAsync_rejects_duplicate_details_when_combined_quantity_exceeds_stock()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using (var db = CreateContext(dbName))
        {
            SeedSaleData(db);
            db.Customers.Add(new Customer { Id = 1, FullName = "Customer", PhoneNumber = "555" });
            db.Cars.Add(new Car { Id = 1, CustomerId = 1, PlateNumber = "ABC123", Make = "Make", Model = "Model" });
            await db.SaveChangesAsync();
        }

        var service = new ServiceOrderService(new TestDbContextFactory(dbName));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOilChangeServiceAsync(new OilChangeRequest(
                CustomerId: 1,
                CarId: 1,
                OdometerKm: 100,
                UserId: 1,
                WarehouseId: 1,
                Details:
                [
                    new SaleItemRequest(1, 3m),
                    new SaleItemRequest(1, 3m)
                ])));

        Assert.Contains("رصيد", ex.Message);
        await using var verifyDb = CreateContext(dbName);
        Assert.Empty(await verifyDb.ServiceOrders.ToListAsync());
        Assert.Equal(5m, await CurrentStockAsync(verifyDb, productId: 1, warehouseId: 1));
    }

    [Fact]
    public async Task TransferStockBulkAsync_rejects_duplicate_lines_with_conflicting_branch_prices()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using (var db = CreateContext(dbName))
        {
            SeedSaleData(db);
            db.Warehouses.Add(new Warehouse { Id = 2, Name = "Branch", Type = WarehouseType.Branch, IsActive = true });
            await db.SaveChangesAsync();
        }

        var service = new TransferService(new TestDbContextFactory(dbName));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransferStockBulkAsync(new TransferStockBulkRequest(
                FromWarehouseId: 1,
                ToWarehouseId: 2,
                Notes: "bulk",
                UserId: 1,
                Lines:
                [
                    new TransferStockBulkLineRequest(1, 1m, 10m),
                    new TransferStockBulkLineRequest(1, 1m, 20m)
                ])));

        Assert.Contains("أسعار بيع مختلفة", ex.Message);
        await using var verifyDb = CreateContext(dbName);
        Assert.Empty(await verifyDb.BranchProductPrices.ToListAsync());
        Assert.Equal(5m, await CurrentStockAsync(verifyDb, productId: 1, warehouseId: 1));
    }

    private static OilChangePosDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OilChangePosDbContext(options);
    }

    private static void SeedSaleData(OilChangePosDbContext db)
    {
        db.Companies.Add(new Company { Id = 1, Name = "Company" });
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
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", Type = WarehouseType.Main, IsActive = true });
        db.Users.Add(new AppUser
        {
            Id = 1,
            Username = "admin",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true
        });
        db.Purchases.Add(new Purchase
        {
            Id = 1,
            ProductId = 1,
            Quantity = 5m,
            PurchasePrice = 6m,
            ProductionDate = new DateTime(2026, 1, 1),
            PurchaseDate = new DateTime(2026, 1, 2),
            WarehouseId = 1,
            CreatedByUserId = 1,
            Notes = "seed"
        });
        db.StockMovements.Add(new StockMovement
        {
            ProductId = 1,
            MovementType = StockMovementType.Purchase,
            Quantity = 5m,
            ToWarehouseId = 1,
            SourcePurchaseId = 1,
            Notes = "seed"
        });
    }

    private static async Task<decimal> CurrentStockAsync(OilChangePosDbContext db, int productId, int warehouseId)
    {
        var incoming = await db.StockMovements
            .Where(x => x.ProductId == productId && x.ToWarehouseId == warehouseId && x.Quantity > 0)
            .SumAsync(x => x.Quantity);
        var outgoing = await db.StockMovements
            .Where(x => x.ProductId == productId && x.FromWarehouseId == warehouseId && x.Quantity > 0)
            .SumAsync(x => x.Quantity);
        return incoming - outgoing;
    }

    private sealed class TestDbContextFactory(string dbName) : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext() => CreateContext(dbName);
    }
}
