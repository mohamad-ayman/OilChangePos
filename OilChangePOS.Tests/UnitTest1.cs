using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public class CriticalServiceRegressionTests
{
    [Fact]
    public async Task CompleteSaleAsync_RejectsDuplicateLinesThatExceedMergedStock()
    {
        var factory = await CreateSeededFactoryAsync();
        var service = new SalesService(factory);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSaleAsync(new CompleteSaleRequest(
                CustomerId: null,
                DiscountAmount: 0,
                UserId: 1,
                WarehouseId: 2,
                Items:
                [
                    new SaleItemRequest(1, 3),
                    new SaleItemRequest(1, 3)
                ])));

        Assert.Contains("رصيد غير كاف", ex.Message);
        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.Invoices.ToListAsync());
        Assert.Empty(await db.StockMovements.Where(m => m.MovementType == StockMovementType.Sale).ToListAsync());
    }

    [Fact]
    public async Task CreateOilChangeServiceAsync_RejectsDuplicateDetailsThatExceedMergedStock()
    {
        var factory = await CreateSeededFactoryAsync();
        var service = new ServiceOrderService(factory);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOilChangeServiceAsync(new OilChangeRequest(
                CustomerId: 1,
                CarId: 1,
                OdometerKm: 1000,
                UserId: 1,
                WarehouseId: 2,
                Details:
                [
                    new SaleItemRequest(1, 3),
                    new SaleItemRequest(1, 3)
                ])));

        Assert.Contains("رصيد غير كاف", ex.Message);
        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.ServiceOrders.ToListAsync());
        Assert.Empty(await db.StockMovements.Where(m => m.MovementType == StockMovementType.Sale).ToListAsync());
    }

    [Fact]
    public async Task RunStockAuditAsync_RejectsBranchUserLineWarehouseOverride()
    {
        var factory = await CreateSeededFactoryAsync();
        var service = new InventoryService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunStockAuditAsync(
                userId: 2,
                warehouseId: 2,
                lines: [new AuditLineRequest(1, ActualQuantity: 0, WarehouseId: 3)],
                notes: "cross-branch adjustment"));

        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.StockMovements.Where(m => m.MovementType == StockMovementType.Adjust).ToListAsync());
        Assert.Equal(5, await service.GetCurrentStockAsync(1, 3));
    }

    [Fact]
    public async Task TransferStockBulkAsync_RejectsConflictingDuplicateBranchPrices()
    {
        var factory = await CreateSeededFactoryAsync();
        var service = new TransferService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TransferStockBulkAsync(new TransferStockBulkRequest(
                FromWarehouseId: 1,
                ToWarehouseId: 2,
                Notes: "bulk",
                UserId: 1,
                Lines:
                [
                    new TransferStockBulkLineRequest(1, 1, 11),
                    new TransferStockBulkLineRequest(1, 2, 12)
                ])));

        await using var db = factory.CreateDbContext();
        Assert.Empty(await db.BranchProductPrices.ToListAsync());
        Assert.Empty(await db.StockMovements.Where(m => m.MovementType == StockMovementType.Transfer).ToListAsync());
    }

    private static async Task<TestDbFactory> CreateSeededFactoryAsync()
    {
        var factory = new TestDbFactory();
        await using var db = factory.CreateDbContext();

        db.Companies.Add(new Company { Id = 1, Name = "Acme", IsActive = true });
        db.Products.Add(new Product
        {
            Id = 1,
            CompanyId = 1,
            Name = "Oil 5W30",
            ProductCategory = "Oil",
            PackageSize = "4L",
            UnitPrice = 10,
            IsActive = true
        });
        db.Warehouses.AddRange(
            new Warehouse { Id = 1, Name = "Main", Type = WarehouseType.Main, IsActive = true },
            new Warehouse { Id = 2, Name = "Branch A", Type = WarehouseType.Branch, IsActive = true },
            new Warehouse { Id = 3, Name = "Branch B", Type = WarehouseType.Branch, IsActive = true });
        db.Users.AddRange(
            new AppUser { Id = 1, Username = "admin", PasswordHash = "hash", Role = UserRole.Admin, IsActive = true },
            new AppUser { Id = 2, Username = "manager-a", PasswordHash = "hash", Role = UserRole.Manager, IsActive = true, HomeBranchWarehouseId = 2 });
        db.Customers.Add(new Customer { Id = 1, FullName = "Customer", PhoneNumber = "0500000000" });
        db.Cars.Add(new Car { Id = 1, CustomerId = 1, PlateNumber = "ABC123", Make = "Make", Model = "Model" });

        db.StockMovements.AddRange(
            new StockMovement
            {
                ProductId = 1,
                MovementType = StockMovementType.Purchase,
                Quantity = 5,
                ToWarehouseId = 2,
                Notes = "seed branch A"
            },
            new StockMovement
            {
                ProductId = 1,
                MovementType = StockMovementType.Purchase,
                Quantity = 5,
                ToWarehouseId = 3,
                Notes = "seed branch B"
            });

        await db.SaveChangesAsync();
        return factory;
    }

    private sealed class TestDbFactory : IDbContextFactory<OilChangePosDbContext>
    {
        private readonly DbContextOptions<OilChangePosDbContext> options =
            new DbContextOptionsBuilder<OilChangePosDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

        public OilChangePosDbContext CreateDbContext() => new(options);
    }
}
