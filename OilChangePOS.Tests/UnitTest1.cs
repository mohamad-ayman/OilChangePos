using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public class CriticalRegressionTests
{
    [Fact]
    public async Task BulkTransferRejectsConflictingDuplicateBranchPricesBeforeWriting()
    {
        var factory = new ThrowingDbContextFactory();
        var service = new TransferService(factory);
        var request = new TransferStockBulkRequest(
            FromWarehouseId: 1,
            ToWarehouseId: 2,
            Notes: "bulk",
            UserId: 10,
            Lines:
            [
                new TransferStockBulkLineRequest(ProductId: 42, Quantity: 1m, BranchSalePriceForDestination: 100m),
                new TransferStockBulkLineRequest(ProductId: 42, Quantity: 2m, BranchSalePriceForDestination: 150m)
            ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransferStockBulkAsync(request));

        Assert.Contains("أكثر من سعر", ex.Message);
        Assert.False(factory.WasUsed);
    }

    [Fact]
    public async Task BulkTransferRejectsMissingLinesWithoutDereferencingNull()
    {
        var factory = new ThrowingDbContextFactory();
        var service = new TransferService(factory);
        var request = new TransferStockBulkRequest(
            FromWarehouseId: 1,
            ToWarehouseId: 2,
            Notes: "bulk",
            UserId: 10,
            Lines: null!);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.TransferStockBulkAsync(request));

        Assert.Contains("سطراً واحداً", ex.Message);
        Assert.False(factory.WasUsed);
    }

    [Fact]
    public void ModelSnapshotIncludesPersistedProfitAndExpenseColumns()
    {
        var snapshotType = typeof(OilChangePosDbContext).Assembly.GetType(
            "OilChangePOS.Data.Migrations.OilChangePosDbContextModelSnapshot",
            throwOnError: true)!;
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(
            Activator.CreateInstance(snapshotType, nonPublic: true));
        var model = snapshot.Model;

        Assert.NotNull(model.FindEntityType(typeof(Invoice))?.FindProperty(nameof(Invoice.ContainsEstimatedCost)));
        Assert.NotNull(model.FindEntityType(typeof(Expense))?.FindProperty(nameof(Expense.VisibleInBranchExpenseList)));
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<OilChangePosDbContext>
    {
        public bool WasUsed { get; private set; }

        public OilChangePosDbContext CreateDbContext()
        {
            WasUsed = true;
            throw new InvalidOperationException("The database should not be opened for request normalization failures.");
        }
    }
}
