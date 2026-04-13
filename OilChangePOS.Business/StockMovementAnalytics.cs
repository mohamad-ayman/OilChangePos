using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;

namespace OilChangePOS.Business;

/// <summary>Aggregates <see cref="OilChangePOS.Domain.StockMovement"/> rows — same logic as <see cref="WarehouseStock.GetOnHandAsync"/> but batched.</summary>
internal static class StockMovementAnalytics
{
    /// <summary>Net on-hand quantity per (productId, warehouseId). Omits pairs with zero net.</summary>
    internal static async Task<Dictionary<(int ProductId, int WarehouseId), decimal>> GetNetQuantitiesAsync(
        OilChangePosDbContext db,
        CancellationToken cancellationToken = default)
    {
        var inbound = await db.StockMovements.AsNoTracking()
            .Where(m => m.ToWarehouseId != null && m.Quantity != 0)
            .GroupBy(m => new { m.ProductId, WarehouseId = m.ToWarehouseId!.Value })
            .Select(g => new { g.Key.ProductId, g.Key.WarehouseId, Qty = g.Sum(m => m.Quantity) })
            .ToListAsync(cancellationToken);

        var outbound = await db.StockMovements.AsNoTracking()
            .Where(m => m.FromWarehouseId != null && m.Quantity != 0)
            .GroupBy(m => new { m.ProductId, WarehouseId = m.FromWarehouseId!.Value })
            .Select(g => new { g.Key.ProductId, g.Key.WarehouseId, Qty = g.Sum(m => m.Quantity) })
            .ToListAsync(cancellationToken);

        var dict = new Dictionary<(int, int), decimal>();
        foreach (var x in inbound)
        {
            var k = (x.ProductId, x.WarehouseId);
            dict[k] = dict.GetValueOrDefault(k, 0m) + x.Qty;
        }

        foreach (var x in outbound)
        {
            var k = (x.ProductId, x.WarehouseId);
            dict[k] = dict.GetValueOrDefault(k, 0m) - x.Qty;
        }

        foreach (var key in dict.Keys.ToList())
        {
            if (dict[key] == 0)
                dict.Remove(key);
        }

        return dict;
    }
}
