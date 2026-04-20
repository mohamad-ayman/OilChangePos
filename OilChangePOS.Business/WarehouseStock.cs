using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;

namespace OilChangePOS.Business;

internal static class WarehouseStock
{
    internal static async Task<decimal> GetOnHandAsync(OilChangePosDbContext db, int productId, int warehouseId, CancellationToken cancellationToken = default)
    {
        var inQty = await db.StockMovements.AsNoTracking()
            .Where(x => x.ProductId == productId
                        && x.ToWarehouseId == warehouseId
                        && x.Quantity > 0)
            .SumAsync(x => x.Quantity, cancellationToken);
        var outQty = await db.StockMovements.AsNoTracking()
            .Where(x => x.ProductId == productId
                        && x.FromWarehouseId == warehouseId
                        && x.Quantity > 0)
            .SumAsync(x => x.Quantity, cancellationToken);
        return inQty - outQty;
    }
}
