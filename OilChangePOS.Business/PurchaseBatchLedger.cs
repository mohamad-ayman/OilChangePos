using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

/// <summary>
/// FEFO batch allocation for stock leaving a warehouse (sales), aligned with main→branch transfers via <see cref="StockMovement.SourcePurchaseId"/>.
/// </summary>
internal static class PurchaseBatchLedger
{
    internal static async Task<decimal> SumAllocatedOutFromPurchaseAsync(
        OilChangePosDbContext db,
        int warehouseId,
        int purchaseId,
        CancellationToken cancellationToken)
    {
        var sum = await db.StockMovements.AsNoTracking()
            .Where(m =>
                m.FromWarehouseId == warehouseId &&
                m.SourcePurchaseId == purchaseId &&
                (m.MovementType == StockMovementType.Transfer || m.MovementType == StockMovementType.Sale))
            .SumAsync(m => (decimal?)m.Quantity, cancellationToken);
        return sum ?? 0m;
    }

    /// <summary>Allocates sale quantity into one or more <see cref="StockMovementType.Sale"/> rows with batch linkage.</summary>
    /// <returns><c>true</c> if any quantity was posted without a purchase batch (estimated cost).</returns>
    internal static async Task<bool> AllocateSaleLineAsync(
        OilChangePosDbContext db,
        int productId,
        int warehouseId,
        WarehouseType warehouseType,
        int mainWarehouseId,
        decimal quantityNeeded,
        int referenceId,
        string notes,
        CancellationToken cancellationToken)
    {
        if (quantityNeeded <= 0)
            return false;

        var still = quantityNeeded;
        var anyEstimated = false;
        var onHandLeft = await WarehouseStock.GetOnHandAsync(db, productId, warehouseId, cancellationToken);
        if (onHandLeft < quantityNeeded)
            throw new InvalidOperationException($"رصيد غير كافٍ للصنف {productId}.");

        if (warehouseType == WarehouseType.Main)
        {
            var purchases = await db.Purchases.AsNoTracking()
                .Where(p => p.ProductId == productId && p.WarehouseId == mainWarehouseId)
                .OrderBy(p => p.ProductionDate).ThenBy(p => p.PurchaseDate).ThenBy(p => p.Id)
                .ToListAsync(cancellationToken);

            foreach (var p in purchases)
            {
                if (still <= 0)
                    break;
                var allocatedOut = await SumAllocatedOutFromPurchaseAsync(db, warehouseId, p.Id, cancellationToken);
                var bookRemaining = p.Quantity - allocatedOut;
                if (bookRemaining <= 0)
                    continue;
                var cap = Math.Min(bookRemaining, onHandLeft);
                if (cap <= 0)
                    continue;
                var take = Math.Min(still, cap);
                if (take <= 0)
                    continue;
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = productId,
                    MovementType = StockMovementType.Sale,
                    Quantity = take,
                    FromWarehouseId = warehouseId,
                    ReferenceId = referenceId,
                    SourcePurchaseId = p.Id,
                    Notes = notes
                });
                still -= take;
                onHandLeft -= take;
            }
        }
        else
        {
            var inboundPurchaseIds = await db.StockMovements.AsNoTracking()
                .Where(m =>
                    m.MovementType == StockMovementType.Transfer &&
                    m.ToWarehouseId == warehouseId &&
                    m.ProductId == productId &&
                    m.SourcePurchaseId != null)
                .Select(m => m.SourcePurchaseId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            var purchases = inboundPurchaseIds.Count == 0
                ? []
                : await db.Purchases.AsNoTracking()
                    .Where(p => inboundPurchaseIds.Contains(p.Id) && p.ProductId == productId)
                    .OrderBy(p => p.ProductionDate).ThenBy(p => p.PurchaseDate).ThenBy(p => p.Id)
                    .ToListAsync(cancellationToken);

            foreach (var p in purchases)
            {
                if (still <= 0)
                    break;
                var inQty = await db.StockMovements.AsNoTracking()
                    .Where(m =>
                        m.MovementType == StockMovementType.Transfer &&
                        m.ToWarehouseId == warehouseId &&
                        m.ProductId == productId &&
                        m.SourcePurchaseId == p.Id)
                    .SumAsync(m => (decimal?)m.Quantity, cancellationToken) ?? 0m;
                var outQty = await db.StockMovements.AsNoTracking()
                    .Where(m =>
                        m.FromWarehouseId == warehouseId &&
                        m.ProductId == productId &&
                        m.SourcePurchaseId == p.Id &&
                        (m.MovementType == StockMovementType.Sale || m.MovementType == StockMovementType.Transfer))
                    .SumAsync(m => (decimal?)m.Quantity, cancellationToken) ?? 0m;
                var remaining = inQty - outQty;
                if (remaining <= 0)
                    continue;
                var cap = Math.Min(remaining, onHandLeft);
                if (cap <= 0)
                    continue;
                var take = Math.Min(still, cap);
                if (take <= 0)
                    continue;
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = productId,
                    MovementType = StockMovementType.Sale,
                    Quantity = take,
                    FromWarehouseId = warehouseId,
                    ReferenceId = referenceId,
                    SourcePurchaseId = p.Id,
                    Notes = notes
                });
                still -= take;
                onHandLeft -= take;
            }
        }

        if (still > 0)
        {
            anyEstimated = true;
            db.StockMovements.Add(new StockMovement
            {
                ProductId = productId,
                MovementType = StockMovementType.Sale,
                Quantity = still,
                FromWarehouseId = warehouseId,
                ReferenceId = referenceId,
                SourcePurchaseId = null,
                Notes = string.IsNullOrWhiteSpace(notes)
                    ? "بيع — تكلفة مقدّرة (دفعة غير مسنَدة)"
                    : $"{notes} — تكلفة مقدّرة (دفعة غير مسنَدة)"
            });
        }

        return anyEstimated;
    }
}
