using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public sealed class SalesLineValidationTests
{
    [Fact]
    public async Task CompleteSaleAsync_RejectsDuplicateLinesThatExceedStockInAggregate()
    {
        var options = CreateOptions();
        await SeedScenarioAsync(options);
        var service = new SalesService(new TestDbContextFactory(options));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSaleAsync(
                new CompleteSaleRequest(
                    CustomerId: null,
                    DiscountAmount: 0m,
                    UserId: UserIds.BranchCashier,
                    WarehouseId: WarehouseIds.Branch,
                    Items:
                    [
                        new SaleItemRequest(ProductIds.Oil, 3m),
                        new SaleItemRequest(ProductIds.Oil, 3m)
                    ])));

        await using var db = new OilChangePosDbContext(options);
        Assert.Empty(await db.Invoices.ToListAsync());
        Assert.DoesNotContain(await db.StockMovements.ToListAsync(), m => m.MovementType == StockMovementType.Sale);
    }

    [Fact]
    public async Task CreateOilChangeServiceAsync_RejectsDuplicateDetailsThatExceedStockInAggregate()
    {
        var options = CreateOptions();
        await SeedScenarioAsync(options);
        var service = new ServiceOrderService(new TestDbContextFactory(options));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOilChangeServiceAsync(
                new OilChangeRequest(
                    CustomerIds.Retail,
                    CarIds.RetailCar,
                    OdometerKm: 12345,
                    UserId: UserIds.BranchCashier,
                    WarehouseId: WarehouseIds.Branch,
                    Details:
                    [
                        new SaleItemRequest(ProductIds.Oil, 3m),
                        new SaleItemRequest(ProductIds.Oil, 3m)
                    ])));

        await using var db = new OilChangePosDbContext(options);
        Assert.Empty(await db.ServiceOrders.ToListAsync());
        Assert.DoesNotContain(await db.StockMovements.ToListAsync(), m => m.MovementType == StockMovementType.Sale);
    }

    [Fact]
    public async Task CompleteSaleAsync_RejectsDiscountAboveSubtotalBeforeWritingInvoice()
    {
        var options = CreateOptions();
        await SeedScenarioAsync(options);
        var service = new SalesService(new TestDbContextFactory(options));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteSaleAsync(
                new CompleteSaleRequest(
                    CustomerId: null,
                    DiscountAmount: 1000m,
                    UserId: UserIds.BranchCashier,
                    WarehouseId: WarehouseIds.Branch,
                    Items: [new SaleItemRequest(ProductIds.Oil, 1m)])));

        await using var db = new OilChangePosDbContext(options);
        Assert.Empty(await db.Invoices.ToListAsync());
        Assert.DoesNotContain(await db.StockMovements.ToListAsync(), m => m.MovementType == StockMovementType.Sale);
    }

    private static DbContextOptions<OilChangePosDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static async Task SeedScenarioAsync(DbContextOptions<OilChangePosDbContext> options)
    {
        await using var db = new OilChangePosDbContext(options);

        db.Warehouses.AddRange(
            new Warehouse { Id = WarehouseIds.Main, Name = "Main", Type = WarehouseType.Main },
            new Warehouse { Id = WarehouseIds.Branch, Name = "Branch", Type = WarehouseType.Branch });

        db.Companies.Add(new Company { Id = CompanyIds.Acme, Name = "Acme" });
        db.Products.Add(new Product
        {
            Id = ProductIds.Oil,
            CompanyId = CompanyIds.Acme,
            Name = "Oil",
            ProductCategory = "Oil",
            PackageSize = "4L",
            UnitPrice = 25m,
            IsActive = true
        });

        db.Users.Add(new AppUser
        {
            Id = UserIds.BranchCashier,
            Username = "branch-cashier",
            PasswordHash = "not-used",
            Role = UserRole.Cashier,
            IsActive = true,
            HomeBranchWarehouseId = WarehouseIds.Branch
        });

        db.Customers.Add(new Customer
        {
            Id = CustomerIds.Retail,
            FullName = "Retail Customer",
            PhoneNumber = "0500000000"
        });
        db.Cars.Add(new Car
        {
            Id = CarIds.RetailCar,
            CustomerId = CustomerIds.Retail,
            PlateNumber = "ABC123",
            Make = "Toyota",
            Model = "Camry"
        });

        db.StockMovements.Add(new StockMovement
        {
            ProductId = ProductIds.Oil,
            MovementType = StockMovementType.Transfer,
            Quantity = 5m,
            ToWarehouseId = WarehouseIds.Branch,
            Notes = "seed stock"
        });

        await db.SaveChangesAsync();
    }

    private static class WarehouseIds
    {
        public const int Main = 1;
        public const int Branch = 2;
    }

    private static class CompanyIds
    {
        public const int Acme = 10;
    }

    private static class ProductIds
    {
        public const int Oil = 100;
    }

    private static class UserIds
    {
        public const int BranchCashier = 1000;
    }

    private static class CustomerIds
    {
        public const int Retail = 2000;
    }

    private static class CarIds
    {
        public const int RetailCar = 3000;
    }
}
