using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;
using OilChangePOS.Data;

namespace OilChangePOS.Tests;

public class TransferServiceTests
{
    [Fact]
    public async Task TransferStockBulkAsync_rejects_conflicting_duplicate_branch_prices()
    {
        var service = new TransferService(new ThrowingDbContextFactory());
        var request = new TransferStockBulkRequest(
            FromWarehouseId: 1,
            ToWarehouseId: 2,
            Notes: "",
            UserId: 1,
            Lines:
            [
                new TransferStockBulkLineRequest(ProductId: 10, Quantity: 1, BranchSalePriceForDestination: 25m),
                new TransferStockBulkLineRequest(ProductId: 10, Quantity: 2, BranchSalePriceForDestination: 30m)
            ]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.TransferStockBulkAsync(request));

        Assert.Contains("سعرين مختلفين", ex.Message);
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<OilChangePosDbContext>
    {
        public OilChangePosDbContext CreateDbContext() =>
            throw new InvalidOperationException("The database should not be reached for invalid duplicate prices.");
    }
}
