using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;

namespace OilChangePOS.Business;

internal static class BranchSalePricing
{
    internal static async Task<Dictionary<int, decimal>> LoadOverridesAsync(
        OilChangePosDbContext db, int warehouseId, List<int> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return [];
        return await db.BranchProductPrices.AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId && productIds.Contains(x.ProductId))
            .ToDictionaryAsync(x => x.ProductId, x => x.SalePrice, cancellationToken);
    }

    internal static decimal EffectiveSalePrice(decimal catalogUnitPrice, IReadOnlyDictionary<int, decimal> overrides, int productId) =>
        overrides.TryGetValue(productId, out var o) ? o : catalogUnitPrice;
}
