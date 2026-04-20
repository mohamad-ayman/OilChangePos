using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public class TransferService(IDbContextFactory<OilChangePosDbContext> dbFactory) : ITransferService
{
    private const int MaxBulkTransferLines = 300;

    public async Task<int> TransferStockAsync(TransferStockRequest request, CancellationToken cancellationToken = default)
    {
        if (request.FromWarehouseId == request.ToWarehouseId)
            throw new InvalidOperationException("المستودع المصدر والوجهة يجب أن يكونا مختلفين.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (actor.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم تحويل المخزون.");
        var fromWh = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == request.FromWarehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع المصدر غير موجود.");
        var toWh = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == request.ToWarehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع الوجهة غير موجود.");
        if (toWh.Type == WarehouseType.Branch && fromWh.Type != WarehouseType.Main)
            throw new InvalidOperationException("التحويل للفرع مسموح فقط من المستودع الرئيسي.");
        if (fromWh.Type == WarehouseType.Branch && toWh.Type == WarehouseType.Branch)
            throw new InvalidOperationException("التحويل بين الفروع غير مسموح. حوّل من المستودع الرئيسي لكل فرع.");

        var useFefo = fromWh.Type == WarehouseType.Main && toWh.Type == WarehouseType.Branch;
        if (useFefo)
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var id = await TransferStockWithinDbAsync(db, request, fromWh, toWh, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return id;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return await TransferStockWithinDbAsync(db, request, fromWh, toWh, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> TransferStockBulkAsync(TransferStockBulkRequest bulk, CancellationToken cancellationToken = default)
    {
        if (bulk.Lines.Count == 0)
            throw new InvalidOperationException("أضف سطراً واحداً على الأقل للتحويل المجمّع.");
        if (bulk.Lines.Count > MaxBulkTransferLines)
            throw new InvalidOperationException($"لا يمكن تجاوز {MaxBulkTransferLines} سطراً في تحويل واحد.");

        var merged = NormalizeAndMergeBulkLines(bulk.Lines);

        if (bulk.FromWarehouseId == bulk.ToWarehouseId)
            throw new InvalidOperationException("المستودع المصدر والوجهة يجب أن يكونا مختلفين.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == bulk.UserId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (actor.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم تحويل المخزون.");
        var fromWh = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == bulk.FromWarehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع المصدر غير موجود.");
        var toWh = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == bulk.ToWarehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع الوجهة غير موجود.");
        if (toWh.Type == WarehouseType.Branch && fromWh.Type != WarehouseType.Main)
            throw new InvalidOperationException("التحويل للفرع مسموح فقط من المستودع الرئيسي.");
        if (fromWh.Type == WarehouseType.Branch && toWh.Type == WarehouseType.Branch)
            throw new InvalidOperationException("التحويل بين الفروع غير مسموح. حوّل من المستودع الرئيسي لكل فرع.");

        var notes = string.IsNullOrWhiteSpace(bulk.Notes) ? "تحويل مجمّع (ويب)" : bulk.Notes.Trim();

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var ids = new List<int>(merged.Count);
            foreach (var line in merged)
            {
                var req = new TransferStockRequest(
                    line.ProductId,
                    line.Quantity,
                    bulk.FromWarehouseId,
                    bulk.ToWarehouseId,
                    notes,
                    bulk.UserId,
                    line.BranchSalePriceForDestination);
                var id = await TransferStockWithinDbAsync(db, req, fromWh, toWh, cancellationToken);
                ids.Add(id);
            }

            await tx.CommitAsync(cancellationToken);
            return ids;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static List<TransferStockBulkLineRequest> NormalizeAndMergeBulkLines(List<TransferStockBulkLineRequest> lines)
    {
        foreach (var l in lines)
        {
            if (l.ProductId <= 0)
                throw new InvalidOperationException("معرّف صنف غير صالح في التحويل المجمّع.");
            if (l.Quantity <= 0)
                throw new InvalidOperationException("كمية التحويل يجب أن تكون أكبر من صفر لكل سطر.");
        }

        var map = new Dictionary<int, TransferStockBulkLineRequest>();
        foreach (var l in lines)
        {
            if (!map.TryGetValue(l.ProductId, out var cur))
                map[l.ProductId] = l;
            else
            {
                map[l.ProductId] = cur with
                {
                    Quantity = cur.Quantity + l.Quantity,
                    BranchSalePriceForDestination = l.BranchSalePriceForDestination ?? cur.BranchSalePriceForDestination
                };
            }
        }

        return map.Values.ToList();
    }

    /// <summary>Writes movements (and optional branch price) for one SKU. Uses <see cref="DbContext.SaveChangesAsync"/>; caller supplies a transaction when multiple steps must be atomic.</summary>
    private static async Task<int> TransferStockWithinDbAsync(
        OilChangePosDbContext db,
        TransferStockRequest request,
        Warehouse fromWh,
        Warehouse toWh,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
            throw new InvalidOperationException("كمية التحويل يجب أن تكون أكبر من صفر.");

        if (request.BranchSalePriceForDestination is { } branchPx)
        {
            if (branchPx < 0)
                throw new InvalidOperationException("سعر البيع للفرع لا يمكن أن يكون سالباً.");
            if (fromWh.Type != WarehouseType.Main || toWh.Type != WarehouseType.Branch)
                throw new InvalidOperationException("تحديث سعر بيع الفرع متاح فقط عند التحويل من المستودع الرئيسي إلى فرع.");
        }

        var available = await WarehouseStock.GetOnHandAsync(db, request.ProductId, request.FromWarehouseId, cancellationToken);
        if (available < request.Quantity)
            throw new InvalidOperationException($"لا يمكن التحويل أكثر من الرصيد المتاح. المتاح={available}، المطلوب={request.Quantity}");

        var useFefo = fromWh.Type == WarehouseType.Main && toWh.Type == WarehouseType.Branch;
        if (!useFefo)
        {
            var single = new StockMovement
            {
                ProductId = request.ProductId,
                MovementType = StockMovementType.Transfer,
                Quantity = request.Quantity,
                FromWarehouseId = request.FromWarehouseId,
                ToWarehouseId = request.ToWarehouseId,
                Notes = request.Notes
            };
            db.StockMovements.Add(single);
            await ApplyDestinationBranchSalePriceIfNeededAsync(db, request, fromWh, toWh, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return single.Id;
        }

        var still = request.Quantity;
        var onHandLeft = available;
        var firstId = 0;
        var purchases = await db.Purchases.AsNoTracking()
            .Where(p => p.ProductId == request.ProductId && p.WarehouseId == request.FromWarehouseId)
            .OrderBy(p => p.ProductionDate).ThenBy(p => p.PurchaseDate).ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var p in purchases)
        {
            if (still <= 0)
                break;
            var allocatedOut = await PurchaseBatchLedger.SumAllocatedOutFromPurchaseAsync(
                db, request.FromWarehouseId, p.Id, cancellationToken);
            var bookRemaining = p.Quantity - allocatedOut;
            if (bookRemaining <= 0)
                continue;
            var cap = Math.Min(bookRemaining, onHandLeft);
            if (cap <= 0)
                continue;
            var take = Math.Min(still, cap);
            if (take <= 0)
                continue;
            var movement = new StockMovement
            {
                ProductId = request.ProductId,
                MovementType = StockMovementType.Transfer,
                Quantity = take,
                FromWarehouseId = request.FromWarehouseId,
                ToWarehouseId = request.ToWarehouseId,
                SourcePurchaseId = p.Id,
                Notes = request.Notes
            };
            db.StockMovements.Add(movement);
            await db.SaveChangesAsync(cancellationToken);
            if (firstId == 0)
                firstId = movement.Id;
            still -= take;
            onHandLeft -= take;
        }

        if (still > 0)
        {
            var movement = new StockMovement
            {
                ProductId = request.ProductId,
                MovementType = StockMovementType.Transfer,
                Quantity = still,
                FromWarehouseId = request.FromWarehouseId,
                ToWarehouseId = request.ToWarehouseId,
                SourcePurchaseId = null,
                Notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? "تحويل متبقي (دفعة غير مسنَدة)"
                    : $"{request.Notes} — متبقي (دفعة غير مسنَدة)"
            };
            db.StockMovements.Add(movement);
            await db.SaveChangesAsync(cancellationToken);
            if (firstId == 0)
                firstId = movement.Id;
        }

        await ApplyDestinationBranchSalePriceIfNeededAsync(db, request, fromWh, toWh, cancellationToken);
        if (request.BranchSalePriceForDestination.HasValue)
            await db.SaveChangesAsync(cancellationToken);

        return firstId;
    }

    /// <summary>Mutates <paramref name="db"/> (tracked entities only). Caller saves when a branch price was applied.</summary>
    private static async Task ApplyDestinationBranchSalePriceIfNeededAsync(
        OilChangePosDbContext db,
        TransferStockRequest request,
        Warehouse fromWh,
        Warehouse toWh,
        CancellationToken cancellationToken)
    {
        if (request.BranchSalePriceForDestination is not { } price)
            return;
        if (fromWh.Type != WarehouseType.Main || toWh.Type != WarehouseType.Branch)
            return;

        _ = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken)
            ?? throw new InvalidOperationException($"الصنف {request.ProductId} غير موجود.");

        var row = await db.BranchProductPrices.FirstOrDefaultAsync(
            x => x.WarehouseId == request.ToWarehouseId && x.ProductId == request.ProductId, cancellationToken);
        if (row is null)
        {
            db.BranchProductPrices.Add(new BranchProductPrice
            {
                WarehouseId = request.ToWarehouseId,
                ProductId = request.ProductId,
                SalePrice = price
            });
        }
        else
            row.SalePrice = price;
    }
}
