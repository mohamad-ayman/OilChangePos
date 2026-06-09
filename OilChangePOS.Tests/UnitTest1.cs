using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public class RegressionTests
{
    [Fact]
    public async Task TransferStockBulkAsync_RejectsDuplicateSkuWithConflictingBranchPricesBeforeWriting()
    {
        var service = new TransferService(new ThrowingDbContextFactory());
        var request = new TransferStockBulkRequest(
            FromWarehouseId: 1,
            ToWarehouseId: 2,
            Notes: "",
            UserId: 10,
            Lines:
            [
                new TransferStockBulkLineRequest(ProductId: 5, Quantity: 2m, BranchSalePriceForDestination: 100m),
                new TransferStockBulkLineRequest(ProductId: 5, Quantity: 3m, BranchSalePriceForDestination: 250m)
            ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TransferStockBulkAsync(request));

        Assert.Contains("أسعار بيع فرع مختلفة", ex.Message);
    }

    [Fact]
    public void EfModelAndSnapshotIncludeInvoiceAndExpenseBooleanColumns()
    {
        var options = new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new OilChangePosDbContext(options);

        AssertHasProperty(db.Model, typeof(Invoice), nameof(Invoice.ContainsEstimatedCost));
        AssertHasProperty(db.Model, typeof(Expense), nameof(Expense.VisibleInBranchExpenseList));

        var snapshotType = typeof(OilChangePosDbContext).Assembly.GetType(
            "OilChangePOS.Data.Migrations.OilChangePosDbContextModelSnapshot",
            throwOnError: true)!;
        var snapshot = (ModelSnapshot)Activator.CreateInstance(snapshotType, nonPublic: true)!;

        AssertHasProperty(snapshot.Model, typeof(Invoice), nameof(Invoice.ContainsEstimatedCost));
        AssertHasProperty(snapshot.Model, typeof(Expense), nameof(Expense.VisibleInBranchExpenseList));
    }

    private static void AssertHasProperty(Microsoft.EntityFrameworkCore.Metadata.IModel model, Type entityType, string propertyName)
    {
        var entity = model.FindEntityType(entityType);
        Assert.NotNull(entity);
        Assert.NotNull(entity.FindProperty(propertyName));
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext()
        {
            throw new InvalidOperationException("The transfer validation should run before a DbContext is created.");
        }

        public ValueTask<OilChangePosDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("The transfer validation should run before a DbContext is created.");
        }
    }
}
