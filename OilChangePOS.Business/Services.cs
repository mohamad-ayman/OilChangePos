using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

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

internal static class ProductDisplayNames
{
    /// <summary>POS/catalog style: <c>شركة — اسم الصنف</c> (e.g. Mobil — 5w30). Omits company segment when missing.</summary>
    internal static string CatalogLine(string? companyName, string productName)
    {
        var pn = (productName ?? string.Empty).Trim();
        if (pn.Length == 0) return "—";
        var cn = string.IsNullOrWhiteSpace(companyName) ? null : companyName.Trim();
        return cn is null or "" ? pn : $"{cn} — {pn}";
    }

    /// <summary>Shelf line: <c>شركة — صنف — تعبئة</c> when <paramref name="packageSize"/> is set (e.g. Mobil — 5W30 — 4L).</summary>
    internal static string CatalogDisplayName(string? companyName, string? productName, string? packageSize = null)
    {
        var baseLine = CatalogLine(companyName, productName ?? string.Empty);
        var pack = string.IsNullOrWhiteSpace(packageSize) ? null : packageSize.Trim();
        if (pack is null) return baseLine;
        if (baseLine == "—") return pack;
        return $"{baseLine} — {pack}";
    }
}

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

public class InventoryService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IInventoryService
{
    private const decimal LowStockThreshold = 5m;

    public async Task<decimal> GetCurrentStockAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await WarehouseStock.GetOnHandAsync(db, productId, warehouseId, cancellationToken);
    }

    public async Task<List<LowStockItemDto>> GetLowStockAsync(int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var products = await db.Products.Where(x => x.IsActive).Include(x => x.Company).ToListAsync(cancellationToken);
        var result = new List<LowStockItemDto>();
        foreach (var p in products)
        {
            var stock = await WarehouseStock.GetOnHandAsync(db, p.Id, warehouseId, cancellationToken);
            if (stock <= LowStockThreshold)
            {
                var label = ProductDisplayNames.CatalogDisplayName(p.Company?.Name, p.Name, p.PackageSize);
                result.Add(new LowStockItemDto(p.Id, label, stock, LowStockThreshold));
            }
        }
        return result.OrderBy(x => x.CurrentStock).ToList();
    }

    public async Task<int> AddStockAsync(PurchaseStockRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0) throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر.");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (actor.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم إضافة مخزون في المستودع الرئيسي.");
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == request.WarehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        if (warehouse.Type != WarehouseType.Main)
            throw new InvalidOperationException("الشراء مسموح فقط في المستودع الرئيسي.");

        var purchase = new Purchase
        {
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            PurchasePrice = request.PurchasePrice,
            ProductionDate = request.ProductionDate,
            PurchaseDate = request.PurchaseDate,
            WarehouseId = request.WarehouseId,
            CreatedByUserId = request.UserId,
            Notes = request.Notes
        };
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(cancellationToken);

        var movement = new StockMovement
        {
            ProductId = request.ProductId,
            MovementType = StockMovementType.Purchase,
            Quantity = request.Quantity,
            ToWarehouseId = request.WarehouseId,
            ReferenceId = purchase.Id,
            Notes = $"شراء: {request.Notes}"
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        return movement.Id;
    }

    public async Task<PurchaseReceiptBatchResult> AddPurchaseReceiptBatchAsync(
        int userId,
        int warehouseId,
        string supplierName,
        string? receiptMemo,
        IReadOnlyList<PurchaseReceiptLineInput> lines,
        CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
            throw new InvalidOperationException("أضف سطراً واحداً على الأقل في فاتورة الاستلام.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (actor.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم تسجيل مشتريات في المستودع الرئيسي.");

        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        if (warehouse.Type != WarehouseType.Main)
            throw new InvalidOperationException("استلام المشتريات الجماعي مسموح فقط في المستودع الرئيسي.");

        foreach (var line in lines)
        {
            if (line.ProductId <= 0)
                throw new InvalidOperationException("كل سطر يجب أن يحدد صنفاً صالحاً.");
            if (line.Quantity <= 0)
                throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر في كل السطور.");
            if (line.UnitPurchasePrice < 0)
                throw new InvalidOperationException("سعر الشراء لا يمكن أن يكون سالباً.");
        }

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var existingIds = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        if (existingIds.Count != productIds.Count)
            throw new InvalidOperationException("أحد الأصناف المحددة غير موجود في النظام.");

        var supplierPart = string.IsNullOrWhiteSpace(supplierName) ? "—" : supplierName.Trim();
        var memoPart = string.IsNullOrWhiteSpace(receiptMemo) ? string.Empty : $" | {receiptMemo.Trim()}";
        var headerNote = $"مورد: {supplierPart}{memoPart} | استلام جماعي";

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var purchases = new List<Purchase>();
            foreach (var line in lines)
            {
                var lineMemo = string.IsNullOrWhiteSpace(line.LineNote) ? string.Empty : $" | {line.LineNote.Trim()}";
                var purchase = new Purchase
                {
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    PurchasePrice = line.UnitPurchasePrice,
                    ProductionDate = line.ProductionDate.Date,
                    PurchaseDate = line.PurchaseDate.Date,
                    WarehouseId = warehouseId,
                    CreatedByUserId = userId,
                    Notes = headerNote + lineMemo
                };
                purchases.Add(purchase);
                db.Purchases.Add(purchase);
            }

            await db.SaveChangesAsync(cancellationToken);

            foreach (var purchase in purchases)
            {
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = purchase.ProductId,
                    MovementType = StockMovementType.Purchase,
                    Quantity = purchase.Quantity,
                    ToWarehouseId = warehouseId,
                    ReferenceId = purchase.Id,
                    Notes = $"شراء: {purchase.Notes}"
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return new PurchaseReceiptBatchResult(purchases.Count, purchases.ConvertAll(p => p.Id));
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<StockAuditResultDto> RunStockAuditAsync(int userId, int warehouseId, List<AuditLineRequest> lines, string notes, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, userId, cancellationToken);
        var warehouse = await RbacRules.RequireWarehouseAsync(db, warehouseId, cancellationToken);
        if (actor.Role.IsAdmin())
        {
            // admin may audit any warehouse
        }
        else if (actor.Role.IsBranchStaff())
            RbacRules.EnsureBranchStockAudit(actor, warehouse);
        else
            throw new InvalidOperationException("لا يُسمح بتنفيذ جرد المخزون لهذا الدور.");
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var audit = new StockAudit
        {
            CreatedByUserId = userId,
            Notes = notes,
            WarehouseId = warehouseId,
            Status = StockAuditStatus.Submitted
        };
        db.StockAudits.Add(audit);
        await db.SaveChangesAsync(cancellationToken);

        var adjusted = 0;
        foreach (var line in lines)
        {
            var targetWarehouseId = line.WarehouseId == 0 ? warehouseId : line.WarehouseId;
            var reasonCode = StockAuditReasonCodes.Normalize(line.ReasonCode);
            var systemQty = await WarehouseStock.GetOnHandAsync(db, line.ProductId, targetWarehouseId, cancellationToken);
            var auditLine = new StockAuditLine
            {
                StockAuditId = audit.Id,
                ProductId = line.ProductId,
                SystemQuantity = systemQty,
                ActualQuantity = line.ActualQuantity,
                ReasonCode = reasonCode
            };
            db.StockAuditLines.Add(auditLine);
            var diff = line.ActualQuantity - systemQty;
            if (diff != 0)
            {
                adjusted++;
                var reasonLabel = StockAuditReasonCodes.GetDisplay(reasonCode);
                var movementNote = $"جرد #{audit.Id} · {reasonLabel}" + (string.IsNullOrWhiteSpace(notes) ? "" : $" · {notes}");
                if (movementNote.Length > 500)
                    movementNote = movementNote[..500];
                db.StockMovements.Add(new StockMovement
                {
                    ProductId = line.ProductId,
                    MovementType = StockMovementType.Adjust,
                    Quantity = Math.Abs(diff),
                    FromWarehouseId = diff < 0 ? targetWarehouseId : null,
                    ToWarehouseId = diff > 0 ? targetWarehouseId : null,
                    ReferenceId = audit.Id,
                    Notes = movementNote
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new StockAuditResultDto(audit.Id, adjusted);
    }

    public async Task<List<StockAuditHistoryRowDto>> GetStockAuditHistoryAsync(int? warehouseId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.StockAuditLines
            .AsNoTracking()
            .Include(l => l.StockAudit)
            .ThenInclude(a => a!.Warehouse)
            .Include(l => l.StockAudit)
            .ThenInclude(a => a!.CreatedByUser)
            .Include(l => l.Product)
            .Where(l => l.StockAudit != null
                        && l.StockAudit.AuditDateUtc >= fromUtc
                        && l.StockAudit.AuditDateUtc < toUtcExclusive
                        && (!warehouseId.HasValue || l.StockAudit.WarehouseId == warehouseId.Value));

        var list = await query
            .OrderByDescending(l => l.StockAudit!.AuditDateUtc)
            .ThenBy(l => l.Product!.Name)
            .ToListAsync(cancellationToken);

        return list.ConvertAll(l =>
        {
            var audit = l.StockAudit!;
            return new StockAuditHistoryRowDto(
                audit.Id,
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(audit.AuditDateUtc, DateTimeKind.Utc), TimeZoneInfo.Local),
                audit.Warehouse?.Name ?? (audit.WarehouseId is { } wid ? $"مستودع #{wid}" : "قديم / غير معروف"),
                l.Product?.Name ?? $"صنف #{l.ProductId}",
                l.SystemQuantity,
                l.ActualQuantity,
                l.ActualQuantity - l.SystemQuantity,
                StockAuditReasonCodes.GetDisplay(l.ReasonCode),
                audit.Notes,
                audit.CreatedByUser?.Username ?? $"مستخدم #{audit.CreatedByUserId}");
        });
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetBranchSalePriceOverridesAsync(
        int warehouseId, IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
            return new Dictionary<int, decimal>();
        var ids = productIds.Distinct().ToList();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await BranchSalePricing.LoadOverridesAsync(db, warehouseId, ids, cancellationToken);
    }

    public async Task<decimal> GetEffectiveSalePriceAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException($"الصنف {productId} غير موجود.");
        var ovr = await db.BranchProductPrices.AsNoTracking()
            .Where(x => x.WarehouseId == warehouseId && x.ProductId == productId)
            .Select(x => (decimal?)x.SalePrice)
            .FirstOrDefaultAsync(cancellationToken);
        return ovr ?? product.UnitPrice;
    }

    public async Task<List<BranchPriceRowDto>> GetBranchPricesAsync(int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BranchProductPrices.AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .Where(x => x.WarehouseId == warehouseId)
            .OrderBy(x => x.Product!.Name)
            .Select(x => new BranchPriceRowDto(x.ProductId, x.Product!.Name, x.WarehouseId, x.Warehouse!.Name, x.SalePrice))
            .ToListAsync(cancellationToken);
    }

    public async Task SetBranchSalePriceAsync(int userId, int warehouseId, int productId, decimal salePrice, CancellationToken cancellationToken = default)
    {
        if (salePrice < 0)
            throw new InvalidOperationException("سعر البيع لا يمكن أن يكون سالباً.");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, userId, cancellationToken);
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        RbacRules.EnsureBranchInventoryOrPricing(actor, warehouse);
        _ = await db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new InvalidOperationException($"الصنف {productId} غير موجود.");

        var row = await db.BranchProductPrices.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.ProductId == productId, cancellationToken);
        if (row is null)
        {
            db.BranchProductPrices.Add(new BranchProductPrice
            {
                WarehouseId = warehouseId,
                ProductId = productId,
                SalePrice = salePrice
            });
        }
        else
            row.SalePrice = salePrice;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteBranchSalePriceAsync(int userId, int warehouseId, int productId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, userId, cancellationToken);
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        RbacRules.EnsureBranchInventoryOrPricing(actor, warehouse);

        var row = await db.BranchProductPrices.FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.ProductId == productId, cancellationToken);
        if (row is null)
            return;
        db.BranchProductPrices.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public class TransferService(IDbContextFactory<OilChangePosDbContext> dbFactory) : ITransferService
{
    public async Task<int> TransferStockAsync(TransferStockRequest request, CancellationToken cancellationToken = default)
    {
        if (request.FromWarehouseId == request.ToWarehouseId)
            throw new InvalidOperationException("المستودع المصدر والوجهة يجب أن يكونا مختلفين.");
        if (request.Quantity <= 0)
            throw new InvalidOperationException("كمية التحويل يجب أن تكون أكبر من صفر.");

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

        var available = await WarehouseStock.GetOnHandAsync(db, request.ProductId, request.FromWarehouseId, cancellationToken);
        if (available < request.Quantity)
            throw new InvalidOperationException($"لا يمكن التحويل أكثر من الرصيد المتاح. المتاح={available}، المطلوب={request.Quantity}");

        if (request.BranchSalePriceForDestination is { } branchPx)
        {
            if (branchPx < 0)
                throw new InvalidOperationException("سعر البيع للفرع لا يمكن أن يكون سالباً.");
            if (fromWh.Type != WarehouseType.Main || toWh.Type != WarehouseType.Branch)
                throw new InvalidOperationException("تحديث سعر بيع الفرع متاح فقط عند التحويل من المستودع الرئيسي إلى فرع.");
        }

        // Main → branch: consume oldest production batches first (FEFO) and record SourcePurchaseId per slice.
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

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
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

            await tx.CommitAsync(cancellationToken);
            return firstId;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
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

public class SalesService(IDbContextFactory<OilChangePosDbContext> dbFactory) : ISalesService
{
    public async Task<int> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Items.Any()) throw new InvalidOperationException("يجب أن تحتوي الفاتورة على صنف واحد على الأقل.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, request.UserId, cancellationToken);
        var saleWarehouse = await RbacRules.RequireWarehouseAsync(db, request.WarehouseId, cancellationToken);
        RbacRules.EnsurePosSaleWarehouse(actor, saleWarehouse);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        var priceOverrides = await BranchSalePricing.LoadOverridesAsync(db, request.WarehouseId, productIds, cancellationToken);
        var mainWarehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainWarehouseId = mainWarehouse?.Id ?? 0;

        var subtotal = 0m;
        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new InvalidOperationException($"الصنف {item.ProductId} غير موجود.");

            var currentStock = await WarehouseStock.GetOnHandAsync(db, item.ProductId, request.WarehouseId, cancellationToken);
            if (currentStock < item.Quantity)
                throw new InvalidOperationException($"رصيد غير كافٍ لـ '{product.Name}'. المتاح={currentStock}، المطلوب={item.Quantity}");

            var unit = BranchSalePricing.EffectiveSalePrice(product.UnitPrice, priceOverrides, item.ProductId);
            subtotal += item.Quantity * unit;
        }

        var invoice = new Invoice
        {
            InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            DiscountAmount = request.DiscountAmount,
            Subtotal = subtotal,
            Total = subtotal - request.DiscountAmount,
            CreatedByUserId = request.UserId
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);

        var anyEstimatedCogs = false;
        foreach (var item in request.Items)
        {
            var product = products[item.ProductId];
            var unit = BranchSalePricing.EffectiveSalePrice(product.UnitPrice, priceOverrides, item.ProductId);
            var lineTotal = item.Quantity * unit;
            db.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = unit,
                LineTotal = lineTotal
            });

            anyEstimatedCogs |= await PurchaseBatchLedger.AllocateSaleLineAsync(
                db,
                item.ProductId,
                request.WarehouseId,
                saleWarehouse.Type,
                mainWarehouseId,
                item.Quantity,
                invoice.Id,
                "بيع نقطة البيع",
                cancellationToken);
        }

        invoice.ContainsEstimatedCost = anyEstimatedCogs;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return invoice.Id;
    }
}

public class ServiceOrderService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IServiceOrderService
{
    public async Task<int> CreateOilChangeServiceAsync(OilChangeRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, request.UserId, cancellationToken);
        var saleWarehouse = await RbacRules.RequireWarehouseAsync(db, request.WarehouseId, cancellationToken);
        RbacRules.EnsurePosSaleWarehouse(actor, saleWarehouse);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var warehouseId = request.WarehouseId;
        var productIds = request.Details.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var priceOverrides = await BranchSalePricing.LoadOverridesAsync(db, warehouseId, productIds, cancellationToken);
        var mainWarehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainWarehouseId = mainWarehouse?.Id ?? 0;
        decimal subtotal = 0;

        foreach (var detail in request.Details)
        {
            var stock = await WarehouseStock.GetOnHandAsync(db, detail.ProductId, warehouseId, cancellationToken);
            if (stock < detail.Quantity) throw new InvalidOperationException($"رصيد غير كافٍ للصنف {detail.ProductId}");
            var p = products[detail.ProductId];
            var unit = BranchSalePricing.EffectiveSalePrice(p.UnitPrice, priceOverrides, detail.ProductId);
            subtotal += detail.Quantity * unit;
        }

        var service = new ServiceOrder
        {
            ServiceNumber = $"SRV-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            CustomerId = request.CustomerId,
            CarId = request.CarId,
            OdometerKm = request.OdometerKm,
            Subtotal = subtotal,
            Total = subtotal,
            CreatedByUserId = request.UserId
        };
        db.ServiceOrders.Add(service);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var detail in request.Details)
        {
            var product = products[detail.ProductId];
            var unit = BranchSalePricing.EffectiveSalePrice(product.UnitPrice, priceOverrides, detail.ProductId);
            db.ServiceDetails.Add(new ServiceDetail
            {
                ServiceOrderId = service.Id,
                ProductId = detail.ProductId,
                Quantity = detail.Quantity,
                UnitPrice = unit,
                LineTotal = detail.Quantity * unit
            });

            _ = await PurchaseBatchLedger.AllocateSaleLineAsync(
                db,
                detail.ProductId,
                warehouseId,
                saleWarehouse.Type,
                mainWarehouseId,
                detail.Quantity,
                service.Id,
                "خدمة تغيير الزيت",
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return service.Id;
    }
}

public class CustomerService(IDbContextFactory<OilChangePosDbContext> dbFactory) : ICustomerService
{
    public async Task<List<CustomerListDto>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Customers.AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new CustomerListDto(x.Id, $"{x.FullName} · {x.PhoneNumber}"))
            .ToListAsync(cancellationToken);
    }
}

public class ReportService(IDbContextFactory<OilChangePosDbContext> dbFactory, IInventoryService inventoryService) : IReportService
{
    private static async Task<Dictionary<int, string>> LoadProductCatalogDisplayLinesAsync(
        OilChangePosDbContext db, IEnumerable<int> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];
        var rows = await (
            from p in db.Products.AsNoTracking()
            where ids.Contains(p.Id)
            join c in db.Companies.AsNoTracking() on p.CompanyId equals c.Id into cj
            from c in cj.DefaultIfEmpty()
            select new { p.Id, p.Name, p.PackageSize, CompanyName = c != null ? c.Name : (string?)null }
        ).ToListAsync(cancellationToken);
        return rows.ToDictionary(x => x.Id, x => ProductDisplayNames.CatalogDisplayName(x.CompanyName, x.Name, x.PackageSize));
    }

    /// <summary>POS invoice sale rows (batch + optional estimated slice); avoids mixing with service-order movements that share <see cref="StockMovement.ReferenceId"/> space.</summary>
    private static bool IsPosInvoiceSaleBatchMovement(StockMovement m)
    {
        if (m.MovementType != StockMovementType.Sale || m.ReferenceId is not int || m.FromWarehouseId is null)
            return false;
        var n = m.Notes ?? string.Empty;
        return n == "بيع نقطة البيع"
               || n.StartsWith("بيع نقطة البيع —", StringComparison.Ordinal)
               || n == "بيع — تكلفة مقدّرة (دفعة غير مسنَدة)";
    }

    private static int? InvoiceEffectiveWarehouseId(Invoice inv, int mainWarehouseId) =>
        inv.WarehouseId ?? (mainWarehouseId != 0 ? mainWarehouseId : null);

    private sealed class PosInvoiceCogsBatchComputation
    {
        public Dictionary<int, decimal> CogsByInvoiceId { get; } = new();
        public Dictionary<int, bool> ContainsEstimatedByInvoiceId { get; } = new();
        public Dictionary<int, decimal> CogsByProductId { get; } = new();
        public Dictionary<int, bool> ContainsEstimatedByProductId { get; } = new();
        public bool AnyContainsEstimated { get; set; }
    }

    private static async Task<PosInvoiceCogsBatchComputation> ComputePosInvoiceSaleCogsAsync(
        OilChangePosDbContext db,
        List<Invoice> invoices,
        List<InvoiceItem> allLines,
        Dictionary<int, decimal> avgCostByProduct,
        int mainWarehouseId,
        CancellationToken cancellationToken)
    {
        var result = new PosInvoiceCogsBatchComputation();
        if (invoices.Count == 0)
            return result;

        var invoiceIds = invoices.Select(x => x.Id).ToList();
        var movements = await db.StockMovements.AsNoTracking()
            .Where(m =>
                m.MovementType == StockMovementType.Sale &&
                m.ReferenceId != null &&
                invoiceIds.Contains(m.ReferenceId.Value))
            .ToListAsync(cancellationToken);

        var posMoves = movements.Where(IsPosInvoiceSaleBatchMovement).ToList();

        var purchaseIds = posMoves
            .Where(m => m.SourcePurchaseId != null)
            .Select(m => m.SourcePurchaseId!.Value)
            .Distinct()
            .ToList();
        var purchasePrices = purchaseIds.Count == 0
            ? new Dictionary<int, decimal>()
            : await db.Purchases.AsNoTracking()
                .Where(p => purchaseIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.PurchasePrice, cancellationToken);

        foreach (var inv in invoices)
        {
            var effWh = InvoiceEffectiveWarehouseId(inv, mainWarehouseId);
            var invMoves = posMoves
                .Where(m => m.ReferenceId == inv.Id && (effWh == null || m.FromWarehouseId == effWh))
                .ToList();

            decimal cogs = 0;
            var anyEst = inv.ContainsEstimatedCost;

            if (invMoves.Count > 0)
            {
                foreach (var m in invMoves)
                {
                    var lineEst = false;
                    decimal unitCost;
                    if (m.SourcePurchaseId is int pid)
                    {
                        if (purchasePrices.TryGetValue(pid, out var pp))
                            unitCost = pp;
                        else
                        {
                            unitCost = avgCostByProduct.GetValueOrDefault(m.ProductId, 0m);
                            lineEst = true;
                        }
                    }
                    else
                    {
                        unitCost = avgCostByProduct.GetValueOrDefault(m.ProductId, 0m);
                        lineEst = true;
                    }

                    var lineCogs = m.Quantity * unitCost;
                    cogs += lineCogs;
                    if (lineEst)
                        anyEst = true;

                    result.CogsByProductId[m.ProductId] = result.CogsByProductId.GetValueOrDefault(m.ProductId, 0m) + lineCogs;
                    if (lineEst)
                        result.ContainsEstimatedByProductId[m.ProductId] = true;
                }
            }
            else
            {
                anyEst = true;
                foreach (var l in allLines.Where(x => x.InvoiceId == inv.Id))
                {
                    var lineCogs = l.Quantity * avgCostByProduct.GetValueOrDefault(l.ProductId, 0m);
                    cogs += lineCogs;
                    result.CogsByProductId[l.ProductId] = result.CogsByProductId.GetValueOrDefault(l.ProductId, 0m) + lineCogs;
                    result.ContainsEstimatedByProductId[l.ProductId] = true;
                }
            }

            result.CogsByInvoiceId[inv.Id] = cogs;
            result.ContainsEstimatedByInvoiceId[inv.Id] = anyEst;
            if (anyEst)
                result.AnyContainsEstimated = true;
        }

        return result;
    }

    /// <summary>
    /// Sums manual operating expenses in the UTC half-open range. When <paramref name="warehouseId"/> is set,
    /// only expenses tagged to that warehouse (accurate branch P&amp;L); when null, company-wide total.
    /// </summary>
    private static async Task<decimal> SumOperatingExpensesAsync(
        OilChangePosDbContext db,
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        int? warehouseId,
        CancellationToken cancellationToken)
    {
        var q = db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDateUtc >= fromUtcInclusive && x.ExpenseDateUtc < toUtcExclusive);
        if (warehouseId.HasValue)
            q = q.Where(x => x.WarehouseId == warehouseId.Value);
        return await q.SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
    }

    public async Task<DailySalesDto> GetDailySalesReportAsync(DateTime dateUtc, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var start = dateUtc.Date;
        var end = start.AddDays(1);
        var invoices = await db.Invoices.Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end).ToListAsync(cancellationToken);
        return new DailySalesDto(start, invoices.Count, invoices.Sum(x => x.Total), invoices.Sum(x => x.DiscountAmount));
    }

    public async Task<DailySalesDto> GetDailySalesReportForWarehouseAsync(DateTime dateUtc, int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var start = dateUtc.Date;
        var end = start.AddDays(1);
        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;
        var q = db.Invoices.AsNoTracking().Where(x => x.CreatedAtUtc >= start && x.CreatedAtUtc < end);
        if (mainId != 0)
            q = q.Where(x => x.WarehouseId == warehouseId || (x.WarehouseId == null && warehouseId == mainId));
        else
            q = q.Where(x => x.WarehouseId == warehouseId);
        var invoices = await q.ToListAsync(cancellationToken);
        return new DailySalesDto(start, invoices.Count, invoices.Sum(x => x.Total), invoices.Sum(x => x.DiscountAmount));
    }

    public async Task<List<InventorySnapshotDto>> GetInventorySnapshotAsync(int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await GetInventorySnapshotCoreAsync(db, warehouseId, cancellationToken);
    }

    public async Task<SalesDashboardDto> GetSalesDashboardAsync(DateTime fromUtc, DateTime toUtc, int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var from = fromUtc.Date;
        var to = toUtc.Date.AddDays(1);

        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;

        var invoicesQuery = db.Invoices.AsNoTracking().Where(x => x.CreatedAtUtc >= from && x.CreatedAtUtc < to);
        if (mainId != 0)
            invoicesQuery = invoicesQuery.Where(x => x.WarehouseId == warehouseId || (x.WarehouseId == null && warehouseId == mainId));
        else
            invoicesQuery = invoicesQuery.Where(x => x.WarehouseId == warehouseId);

        var invoices = await invoicesQuery.ToListAsync(cancellationToken);
        var invoiceIds = invoices.Select(x => x.Id).ToList();

        var topProducts = await db.InvoiceItems
            .AsNoTracking()
            .Where(x => invoiceIds.Contains(x.InvoiceId))
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Amount = g.Sum(x => x.LineTotal)
            })
            .OrderByDescending(x => x.Quantity)
            .Take(10)
            .ToListAsync(cancellationToken);

        var displayByProduct = await LoadProductCatalogDisplayLinesAsync(db, topProducts.Select(x => x.ProductId), cancellationToken);
        var topDtos = topProducts
            .Select(x => new TopSellingProductDto(displayByProduct.GetValueOrDefault(x.ProductId, $"Product #{x.ProductId}"), x.Quantity, x.Amount))
            .ToList();

        var inventory = await GetInventorySnapshotCoreAsync(db, warehouseId, cancellationToken);
        var lowStock = await inventoryService.GetLowStockAsync(warehouseId, cancellationToken);
        var gross = invoices.Sum(x => x.Subtotal);
        var discounts = invoices.Sum(x => x.DiscountAmount);
        var net = invoices.Sum(x => x.Total);

        var lineItems = invoiceIds.Count == 0
            ? []
            : await db.InvoiceItems.AsNoTracking().Where(x => invoiceIds.Contains(x.InvoiceId)).ToListAsync(cancellationToken);

        var avgPurchaseCostByProduct = await LoadAvgPurchaseCostByProductAsync(db, mainId, cancellationToken);
        var batchCogs = await ComputePosInvoiceSaleCogsAsync(db, invoices, lineItems, avgPurchaseCostByProduct, mainId, cancellationToken);
        var estimatedCogs = invoices.Sum(inv => batchCogs.CogsByInvoiceId.GetValueOrDefault(inv.Id, 0m));
        var estimatedGrossProfit = net - estimatedCogs;
        var operatingExpenses = await db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDateUtc >= from && x.ExpenseDateUtc < to && x.WarehouseId == warehouseId)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var netProfitAfterExpenses = estimatedGrossProfit - operatingExpenses;

        var soldQtyByProduct = lineItems
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var activeProducts = await db.Products.AsNoTracking().Where(x => x.IsActive).Include(x => x.Company).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var slowMoving = new List<SlowMovingProductDto>();
        foreach (var p in activeProducts)
        {
            var onHand = await inventoryService.GetCurrentStockAsync(p.Id, warehouseId, cancellationToken);
            var sold = soldQtyByProduct.GetValueOrDefault(p.Id, 0m);
            if (onHand >= 1 && sold < 1)
                slowMoving.Add(new SlowMovingProductDto(ProductDisplayNames.CatalogDisplayName(p.Company?.Name, p.Name, p.PackageSize), onHand, sold));
        }

        slowMoving = slowMoving.OrderByDescending(x => x.OnHandAtWarehouse).Take(25).ToList();

        var whNames = await db.Warehouses.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var transfers = await db.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.MovementType == StockMovementType.Transfer
                        && x.MovementDateUtc >= from
                        && x.MovementDateUtc < to
                        && (x.FromWarehouseId == warehouseId || x.ToWarehouseId == warehouseId))
            .OrderByDescending(x => x.MovementDateUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        var transferDisplay = await LoadProductCatalogDisplayLinesAsync(db, transfers.Select(m => m.ProductId), cancellationToken);
        var transferDtos = transfers.Select(m =>
        {
            var fromName = m.FromWarehouseId is int f ? whNames.GetValueOrDefault(f, "?") : "—";
            var toName = m.ToWarehouseId is int t ? whNames.GetValueOrDefault(t, "?") : "—";
            var pname = transferDisplay.GetValueOrDefault(m.ProductId, m.Product?.Name ?? $"#{m.ProductId}");
            return new TransferLedgerRowDto(m.MovementDateUtc, pname, m.Quantity, fromName, toName, m.Notes);
        }).ToList();

        return new SalesDashboardDto(
            from,
            to.AddDays(-1),
            invoices.Count,
            gross,
            discounts,
            net,
            invoices.Count == 0 ? 0 : net / invoices.Count,
            inventory.Sum(x => x.StockValue),
            lowStock.Count,
            topDtos,
            estimatedCogs,
            estimatedGrossProfit,
            slowMoving,
            transferDtos,
            batchCogs.AnyContainsEstimated,
            operatingExpenses,
            netProfitAfterExpenses);
    }

    public async Task<List<SalesByWarehouseSummaryDto>> GetSalesSummariesByWarehouseAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var from = fromUtc.Date;
        var to = toUtc.Date.AddDays(1);
        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;

        int? EffectiveWarehouseId(int? warehouseId)
        {
            if (warehouseId.HasValue) return warehouseId.Value;
            return mainId != 0 ? mainId : null;
        }

        var invoices = await db.Invoices.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= from && x.CreatedAtUtc < to)
            .ToListAsync(cancellationToken);

        var sums = new Dictionary<int, (int Count, decimal Net, decimal Gross, decimal Disc)>();
        foreach (var inv in invoices)
        {
            var wid = EffectiveWarehouseId(inv.WarehouseId);
            if (!wid.HasValue) continue;
            if (!sums.TryGetValue(wid.Value, out var cur))
                cur = (0, 0m, 0m, 0m);
            cur.Count++;
            cur.Net += inv.Total;
            cur.Gross += inv.Subtotal;
            cur.Disc += inv.DiscountAmount;
            sums[wid.Value] = cur;
        }

        var warehouses = await db.Warehouses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Type).ThenBy(x => x.Name).ToListAsync(cancellationToken);
        var list = new List<SalesByWarehouseSummaryDto>();
        foreach (var w in warehouses)
        {
            sums.TryGetValue(w.Id, out var s);
            var typeLabel = ArabicWarehouseSiteType(w.Type);
            list.Add(new SalesByWarehouseSummaryDto(w.Id, w.Name, typeLabel, s.Count, s.Net, s.Gross, s.Disc));
        }

        return list;
    }

    public async Task<SalesPeriodSummaryDto> GetSalesPeriodSummaryAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;

        var q = db.Invoices.AsNoTracking().Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcEx);
        if (warehouseId.HasValue)
        {
            var wh = warehouseId.Value;
            if (mainId != 0)
                q = q.Where(x => x.WarehouseId == wh || (x.WarehouseId == null && wh == mainId));
            else
                q = q.Where(x => x.WarehouseId == wh);
        }

        var invoices = await q.ToListAsync(cancellationToken);
        var gross = invoices.Sum(x => x.Subtotal);
        var disc = invoices.Sum(x => x.DiscountAmount);
        var net = invoices.Sum(x => x.Total);
        var cnt = invoices.Count;
        return new SalesPeriodSummaryDto(fromLocalDate.Date, toLocalDate.Date, cnt, gross, disc, net,
            cnt == 0 ? 0m : net / cnt);
    }

    public async Task<List<InvoiceProfitDto>> GetInvoiceProfitBreakdownAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;

        var q = db.Invoices.AsNoTracking()
            .Include(x => x.Warehouse)
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcEx);
        if (warehouseId.HasValue)
        {
            var wh = warehouseId.Value;
            if (mainId != 0)
                q = q.Where(x => x.WarehouseId == wh || (x.WarehouseId == null && wh == mainId));
            else
                q = q.Where(x => x.WarehouseId == wh);
        }

        var invoices = await q.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);
        if (invoices.Count == 0) return [];

        var ids = invoices.Select(x => x.Id).ToList();
        var lines = await db.InvoiceItems.AsNoTracking().Where(x => ids.Contains(x.InvoiceId)).ToListAsync(cancellationToken);
        var avgCost = await LoadAvgPurchaseCostByProductAsync(db, mainId, cancellationToken);
        var whName = invoices.ToDictionary(x => x.Id, x => x.Warehouse?.Name);
        var batchCogs = await ComputePosInvoiceSaleCogsAsync(db, invoices, lines, avgCost, mainId, cancellationToken);

        var list = new List<InvoiceProfitDto>();
        foreach (var inv in invoices)
        {
            var net = inv.Total;
            var cogs = batchCogs.CogsByInvoiceId.GetValueOrDefault(inv.Id, 0m);
            var profit = net - cogs;
            var margin = net > 0 ? Math.Round(100m * profit / net, 2) : 0m;
            list.Add(new InvoiceProfitDto(
                inv.Id,
                inv.InvoiceNumber,
                inv.CreatedAtUtc,
                whName.GetValueOrDefault(inv.Id),
                net,
                cogs,
                profit,
                margin,
                batchCogs.ContainsEstimatedByInvoiceId.GetValueOrDefault(inv.Id, false)));
        }

        return list;
    }

    public async Task<List<ProductProfitDto>> GetProductProfitBreakdownAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;

        var q = db.Invoices.AsNoTracking().Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcEx);
        if (warehouseId.HasValue)
        {
            var wh = warehouseId.Value;
            if (mainId != 0)
                q = q.Where(x => x.WarehouseId == wh || (x.WarehouseId == null && wh == mainId));
            else
                q = q.Where(x => x.WarehouseId == wh);
        }

        var invoiceIds = await q.Select(x => x.Id).ToListAsync(cancellationToken);
        if (invoiceIds.Count == 0) return [];

        var grouped = await db.InvoiceItems.AsNoTracking()
            .Where(x => invoiceIds.Contains(x.InvoiceId))
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.LineTotal) })
            .ToListAsync(cancellationToken);

        var invoices = await db.Invoices.AsNoTracking().Where(x => invoiceIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var lines = await db.InvoiceItems.AsNoTracking().Where(x => invoiceIds.Contains(x.InvoiceId)).ToListAsync(cancellationToken);
        var avgCost = await LoadAvgPurchaseCostByProductAsync(db, mainId, cancellationToken);
        var batchCogs = await ComputePosInvoiceSaleCogsAsync(db, invoices, lines, avgCost, mainId, cancellationToken);
        var productIds = grouped.Select(x => x.ProductId).ToList();
        var names = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return grouped
            .Select(x =>
            {
                var cogs = batchCogs.CogsByProductId.GetValueOrDefault(x.ProductId, 0m);
                var est = batchCogs.ContainsEstimatedByProductId.GetValueOrDefault(x.ProductId, false);
                return new ProductProfitDto(x.ProductId, names.GetValueOrDefault(x.ProductId, $"#{x.ProductId}"), x.Qty, x.Revenue, cogs, x.Revenue - cogs, est);
            })
            .OrderByDescending(x => x.EstimatedGrossProfit)
            .ToList();
    }

    public async Task<ProfitRollupDto> GetProfitRollupAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        var products = await GetProductProfitBreakdownAsync(fromLocalDate, toLocalDate, warehouseId, cancellationToken);
        var rev = products.Sum(x => x.Revenue);
        var cogs = products.Sum(x => x.EstimatedCogs);
        var gross = rev - cogs;
        var anyEst = products.Exists(x => x.ContainsEstimatedCost);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var operatingExpenses = await SumOperatingExpensesAsync(db, fromUtc, toUtcEx, warehouseId, cancellationToken);
        return new ProfitRollupDto(rev, cogs, gross, anyEst, operatingExpenses, gross - operatingExpenses);
    }

    public async Task<List<WarehouseStockMovementRowDto>> GetCurrentStockFromMovementsAsync(int? warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var net = await StockMovementAnalytics.GetNetQuantitiesAsync(db, cancellationToken);
        var products = await db.Products.AsNoTracking().Include(p => p.Company).Where(x => x.IsActive).ToListAsync(cancellationToken);
        var whs = await db.Warehouses.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var productById = products.ToDictionary(x => x.Id);
        var whById = whs.ToDictionary(x => x.Id);

        var warehouseIds = net.Keys.Select(k => k.WarehouseId).Distinct().ToList();
        var productIds = net.Keys.Select(k => k.ProductId).Distinct().ToList();
        var overrideRows = await db.BranchProductPrices.AsNoTracking()
            .Where(x => warehouseIds.Contains(x.WarehouseId) && productIds.Contains(x.ProductId))
            .Select(x => new { x.WarehouseId, x.ProductId, x.SalePrice })
            .ToListAsync(cancellationToken);
        var overrideLookup = overrideRows.ToDictionary(x => (x.WarehouseId, x.ProductId), x => x.SalePrice);

        var list = new List<WarehouseStockMovementRowDto>();
        foreach (var ((pid, wid), qty) in net)
        {
            if (warehouseId.HasValue && wid != warehouseId.Value) continue;
            if (!productById.TryGetValue(pid, out var p) || !whById.TryGetValue(wid, out var w)) continue;
            var cn = p.Company?.Name ?? string.Empty;
            var pname = string.IsNullOrWhiteSpace(cn) ? p.Name : $"{cn} — {p.Name}";
            var site = ArabicWarehouseSiteType(w.Type);
            var retail = overrideLookup.TryGetValue((wid, pid), out var o) ? o : p.UnitPrice;
            list.Add(new WarehouseStockMovementRowDto(pid, pname, p.ProductCategory, p.PackageSize, wid, w.Name, site, qty, retail, qty * retail));
        }

        return list.OrderBy(x => x.ProductName).ThenBy(x => x.WarehouseName).ToList();
    }

    private static string ArabicWarehouseSiteType(WarehouseType t) =>
        t == WarehouseType.Main ? "مستودع رئيسي" : "فرع";

    private static string ArabicStockMovementType(StockMovementType t) => t switch
    {
        StockMovementType.Purchase => "شراء",
        StockMovementType.Sale => "بيع",
        StockMovementType.Transfer => "تحويل",
        StockMovementType.Adjust => "تسوية جرد",
        _ => t.ToString()
    };

    public async Task<List<StockMovementHistoryRowDto>> GetStockMovementHistoryAsync(int productId, DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var q = db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == productId && m.MovementDateUtc >= fromUtc && m.MovementDateUtc < toUtcEx);
        if (warehouseId.HasValue)
        {
            var wh = warehouseId.Value;
            q = q.Where(m => m.FromWarehouseId == wh || m.ToWarehouseId == wh);
        }

        var rows = await q
            .Include(m => m.FromWarehouse)
            .Include(m => m.ToWarehouse)
            .OrderByDescending(m => m.MovementDateUtc)
            .Take(5000)
            .ToListAsync(cancellationToken);
        return rows.Select(m => new StockMovementHistoryRowDto(
            m.MovementDateUtc,
            ArabicStockMovementType(m.MovementType),
            m.Quantity,
            m.FromWarehouseId,
            m.FromWarehouse?.Name,
            m.ToWarehouseId,
            m.ToWarehouse?.Name,
            m.Notes)).ToList();
    }

    public async Task<List<TransferLedgerRowDto>> GetTransfersReportAsync(DateTime fromLocalDate, DateTime toLocalDate, int? fromWarehouseId, int? toWarehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var q = db.StockMovements.AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.MovementType == StockMovementType.Transfer && x.MovementDateUtc >= fromUtc && x.MovementDateUtc < toUtcEx);
        if (fromWarehouseId.HasValue)
            q = q.Where(x => x.FromWarehouseId == fromWarehouseId.Value);
        if (toWarehouseId.HasValue)
            q = q.Where(x => x.ToWarehouseId == toWarehouseId.Value);

        var transfers = await q.OrderByDescending(x => x.MovementDateUtc).Take(5000).ToListAsync(cancellationToken);
        var whNames = await db.Warehouses.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var displayByProduct = await LoadProductCatalogDisplayLinesAsync(db, transfers.Select(m => m.ProductId), cancellationToken);

        return transfers.Select(m =>
        {
            var fn = m.FromWarehouseId is int f ? whNames.GetValueOrDefault(f, "—") : "—";
            var tn = m.ToWarehouseId is int t ? whNames.GetValueOrDefault(t, "—") : "—";
            var pname = displayByProduct.GetValueOrDefault(m.ProductId, m.Product?.Name ?? $"#{m.ProductId}");
            return new TransferLedgerRowDto(m.MovementDateUtc, pname, m.Quantity, fn, tn, m.Notes);
        }).ToList();
    }

    public async Task<List<TransferLedgerRowDto>> GetBranchTransferLedgerAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var transfers = await db.StockMovements.AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.MovementType == StockMovementType.Transfer
                        && x.MovementDateUtc >= fromUtc
                        && x.MovementDateUtc < toUtcEx
                        && (x.FromWarehouseId == warehouseId || x.ToWarehouseId == warehouseId))
            .OrderByDescending(x => x.MovementDateUtc)
            .Take(2000)
            .ToListAsync(cancellationToken);
        var whNames = await db.Warehouses.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var displayByProduct = await LoadProductCatalogDisplayLinesAsync(db, transfers.Select(m => m.ProductId), cancellationToken);
        return transfers.Select(m =>
        {
            var fn = m.FromWarehouseId is int f ? whNames.GetValueOrDefault(f, "—") : "—";
            var tn = m.ToWarehouseId is int t ? whNames.GetValueOrDefault(t, "—") : "—";
            var pname = displayByProduct.GetValueOrDefault(m.ProductId, m.Product?.Name ?? $"#{m.ProductId}");
            return new TransferLedgerRowDto(m.MovementDateUtc, pname, m.Quantity, fn, tn, m.Notes);
        }).ToList();
    }

    public async Task<List<TopSellingProductDto>> GetTopSellingProductsAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;

        var q = db.Invoices.AsNoTracking().Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcEx);
        if (warehouseId.HasValue)
        {
            var wh = warehouseId.Value;
            if (mainId != 0)
                q = q.Where(x => x.WarehouseId == wh || (x.WarehouseId == null && wh == mainId));
            else
                q = q.Where(x => x.WarehouseId == wh);
        }

        var invoiceIds = await q.Select(x => x.Id).ToListAsync(cancellationToken);
        if (invoiceIds.Count == 0) return [];

        var top = await db.InvoiceItems.AsNoTracking()
            .Where(x => invoiceIds.Contains(x.InvoiceId))
            .GroupBy(x => x.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity), Amount = g.Sum(x => x.LineTotal) })
            .OrderByDescending(x => x.Quantity)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        var displayByProduct = await LoadProductCatalogDisplayLinesAsync(db, top.Select(x => x.ProductId), cancellationToken);
        return top.Select(x => new TopSellingProductDto(displayByProduct.GetValueOrDefault(x.ProductId, $"#{x.ProductId}"), x.Quantity, x.Amount)).ToList();
    }

    public async Task<List<SlowMovingProductDto>> GetSlowMovingProductsAsync(DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;

        var iq = db.Invoices.AsNoTracking().Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcEx);
        if (mainId != 0)
            iq = iq.Where(x => x.WarehouseId == warehouseId || (x.WarehouseId == null && warehouseId == mainId));
        else
            iq = iq.Where(x => x.WarehouseId == warehouseId);

        var invoiceIds = await iq.Select(x => x.Id).ToListAsync(cancellationToken);
        var soldByProduct = invoiceIds.Count == 0
            ? new Dictionary<int, decimal>()
            : await db.InvoiceItems.AsNoTracking()
                .Where(x => invoiceIds.Contains(x.InvoiceId))
                .GroupBy(x => x.ProductId)
                .Select(g => new { g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.Key, x => x.Qty, cancellationToken);

        var active = await db.Products.AsNoTracking().Where(x => x.IsActive).Include(x => x.Company).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var list = new List<SlowMovingProductDto>();
        foreach (var p in active)
        {
            var onHand = await inventoryService.GetCurrentStockAsync(p.Id, warehouseId, cancellationToken);
            var sold = soldByProduct.GetValueOrDefault(p.Id, 0m);
            if (onHand >= 1 && sold < 1)
                list.Add(new SlowMovingProductDto(ProductDisplayNames.CatalogDisplayName(p.Company?.Name, p.Name, p.PackageSize), onHand, sold));
        }

        return list.OrderByDescending(x => x.OnHandAtWarehouse).Take(Math.Clamp(take, 1, 500)).ToList();
    }

    public async Task<List<DailyCashFlowRowDto>> GetDailyCashFlowAsync(DateTime fromLocalDate, DateTime toLocalDate, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);

        var invoices = await db.Invoices.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcEx)
            .Select(x => new { x.Total, x.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var services = await db.ServiceOrders.AsNoTracking()
            .Where(x => x.ServiceDateUtc >= fromUtc && x.ServiceDateUtc < toUtcEx)
            .Select(x => new { x.Total, x.ServiceDateUtc })
            .ToListAsync(cancellationToken);

        var purchases = await db.Purchases.AsNoTracking()
            .Where(x => x.PurchaseDate >= fromLocalDate.Date && x.PurchaseDate < toLocalDate.Date.AddDays(1))
            .Select(x => new { Cash = x.Quantity * x.PurchasePrice, x.PurchaseDate })
            .ToListAsync(cancellationToken);

        var expenseRaw = await db.Expenses.AsNoTracking()
            .Where(x => x.ExpenseDateUtc >= fromUtc && x.ExpenseDateUtc < toUtcEx)
            .Select(x => new { x.Amount, x.ExpenseDateUtc })
            .ToListAsync(cancellationToken);
        var expenseRows = expenseRaw.Select(x => (Day: ToLocalDate(x.ExpenseDateUtc), x.Amount)).ToList();

        var days = new List<DateTime>();
        for (var d = fromLocalDate.Date; d <= toLocalDate.Date; d = d.AddDays(1))
            days.Add(d);

        var salesByDay = invoices
            .GroupBy(x => ToLocalDate(x.CreatedAtUtc))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));
        var svcByDay = services
            .GroupBy(x => ToLocalDate(x.ServiceDateUtc))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Total));
        var purchByDay = purchases
            .GroupBy(x => x.PurchaseDate.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cash));
        var expByDay = expenseRows
            .GroupBy(x => x.Day)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        return days.Select(d =>
        {
            var sales = salesByDay.GetValueOrDefault(d, 0m);
            var svc = svcByDay.GetValueOrDefault(d, 0m);
            var purch = purchByDay.GetValueOrDefault(d, 0m);
            var exp = expByDay.GetValueOrDefault(d, 0m);
            var net = sales + svc - purch - exp;
            return new DailyCashFlowRowDto(d, sales, svc, purch, exp, net);
        }).ToList();
    }

    public async Task<List<ExpenseReportRowDto>> GetExpensesInPeriodAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var q = db.Expenses.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.CreatedByUser)
            .Where(x => x.ExpenseDateUtc >= fromUtc && x.ExpenseDateUtc < toUtcEx);
        if (warehouseId.HasValue)
            q = q.Where(x => x.WarehouseId == null || x.WarehouseId == warehouseId.Value);

        var rows = await q.OrderByDescending(x => x.ExpenseDateUtc).Take(2000).ToListAsync(cancellationToken);
        return rows.Select(x => new ExpenseReportRowDto(
            x.Id,
            x.ExpenseDateUtc,
            x.Amount,
            x.Category,
            x.Description,
            x.Warehouse?.Name,
            x.CreatedByUser?.Username)).ToList();
    }

    public async Task<List<BranchSalesLineRegisterDto>> GetBranchSalesLineRegisterAsync(
        DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;
        var whName = await db.Warehouses.AsNoTracking().Where(w => w.Id == warehouseId).Select(w => w.Name).FirstOrDefaultAsync(cancellationToken) ?? "—";

        var invQ = db.Invoices.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcEx);
        if (mainId != 0)
            invQ = invQ.Where(x => x.WarehouseId == warehouseId || (x.WarehouseId == null && warehouseId == mainId));
        else
            invQ = invQ.Where(x => x.WarehouseId == warehouseId);

        return await (
            from ii in db.InvoiceItems.AsNoTracking()
            join inv in invQ on ii.InvoiceId equals inv.Id
            join pr in db.Products.AsNoTracking() on ii.ProductId equals pr.Id
            join co in db.Companies.AsNoTracking() on pr.CompanyId equals co.Id into coJoin
            from co in coJoin.DefaultIfEmpty()
            join usr in db.Users.AsNoTracking() on inv.CreatedByUserId equals usr.Id
            join cust in db.Customers.AsNoTracking() on inv.CustomerId equals cust.Id into custJoin
            from cust in custJoin.DefaultIfEmpty()
            orderby inv.CreatedAtUtc, inv.Id, ii.Id
            select new BranchSalesLineRegisterDto(
                inv.CreatedAtUtc,
                inv.InvoiceNumber,
                whName,
                cust == null ? "زبون عابر / بدون سجل" : cust.FullName,
                usr.Username,
                ProductDisplayNames.CatalogDisplayName(co == null ? null : co.Name, pr.Name, pr.PackageSize),
                ii.Quantity,
                ii.UnitPrice,
                ii.LineTotal,
                inv.Subtotal,
                inv.DiscountAmount,
                inv.Total)).ToListAsync(cancellationToken);
    }

    public async Task<List<BranchIncomingRegisterDto>> GetBranchIncomingRegisterAsync(
        DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var tz = TimeZoneInfo.Local;
        var list = new List<BranchIncomingRegisterDto>();

        var purchases = await db.Purchases.AsNoTracking()
            .Include(x => x.Product)
            .ThenInclude(p => p!.Company)
            .Include(x => x.CreatedByUser)
            .Where(x => x.WarehouseId == warehouseId
                        && x.PurchaseDate >= fromLocalDate.Date
                        && x.PurchaseDate < toLocalDate.Date.AddDays(1))
            .OrderByDescending(x => x.PurchaseDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var p in purchases)
        {
            var localKind = DateTime.SpecifyKind(p.PurchaseDate, DateTimeKind.Unspecified);
            var entryUtc = TimeZoneInfo.ConvertTimeToUtc(localKind, tz);
            var amt = p.Quantity * p.PurchasePrice;
            list.Add(new BranchIncomingRegisterDto(
                entryUtc,
                "شراء (وارد)",
                ProductDisplayNames.CatalogDisplayName(p.Product?.Company?.Name, p.Product?.Name ?? $"#{p.ProductId}", p.Product?.PackageSize),
                p.Quantity,
                amt,
                "شراء للمستودع المحدد",
                p.Notes,
                p.CreatedByUser?.Username ?? "—"));
        }

        var transfersIn = await db.StockMovements.AsNoTracking()
            .Include(x => x.Product)
            .ThenInclude(p => p!.Company)
            .Include(x => x.FromWarehouse)
            .Where(x => x.MovementType == StockMovementType.Transfer
                        && x.ToWarehouseId == warehouseId
                        && x.MovementDateUtc >= fromUtc
                        && x.MovementDateUtc < toUtcEx)
            .OrderByDescending(x => x.MovementDateUtc)
            .Take(5000)
            .ToListAsync(cancellationToken);

        foreach (var m in transfersIn)
        {
            list.Add(new BranchIncomingRegisterDto(
                m.MovementDateUtc,
                "تحويل وارد",
                ProductDisplayNames.CatalogDisplayName(m.Product?.Company?.Name, m.Product?.Name ?? $"#{m.ProductId}", m.Product?.PackageSize),
                m.Quantity,
                0m,
                m.FromWarehouse != null ? $"من: {m.FromWarehouse.Name}" : "من: —",
                m.Notes,
                "—"));
        }

        return list.OrderByDescending(x => x.EntryDateUtc).ToList();
    }

    public async Task<List<BranchSellerSalesSummaryDto>> GetBranchSalesBySellerAsync(
        DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainId = main?.Id ?? 0;

        var invQ = db.Invoices.AsNoTracking()
            .Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc < toUtcEx);
        if (mainId != 0)
            invQ = invQ.Where(x => x.WarehouseId == warehouseId || (x.WarehouseId == null && warehouseId == mainId));
        else
            invQ = invQ.Where(x => x.WarehouseId == warehouseId);

        var invAgg = await (
            from inv in invQ
            join u in db.Users.AsNoTracking() on inv.CreatedByUserId equals u.Id
            group inv by u.Username into g
            select new
            {
                Seller = g.Key,
                InvoiceCount = g.Count(),
                Gross = g.Sum(x => x.Subtotal),
                Disc = g.Sum(x => x.DiscountAmount),
                Net = g.Sum(x => x.Total)
            }).ToListAsync(cancellationToken);

        var lineAgg = await (
            from ii in db.InvoiceItems.AsNoTracking()
            join inv in invQ on ii.InvoiceId equals inv.Id
            join u in db.Users.AsNoTracking() on inv.CreatedByUserId equals u.Id
            group ii by u.Username into g
            select new
            {
                Seller = g.Key,
                LineCount = g.Count()
            }).ToListAsync(cancellationToken);

        var lineLookup = lineAgg.ToDictionary(x => x.Seller, x => x.LineCount);
        return invAgg
            .Select(x => new BranchSellerSalesSummaryDto(
                x.Seller,
                x.InvoiceCount,
                lineLookup.GetValueOrDefault(x.Seller, 0),
                x.Gross,
                x.Disc,
                x.Net))
            .OrderByDescending(x => x.InvoicesNetTotal)
            .ToList();
    }

    private static DateTime ToLocalDate(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local).Date;

    private static (DateTime FromUtc, DateTime ToUtcExclusive) LocalDateRangeToUtcExclusive(DateTime fromLocalDate, DateTime toLocalDate)
    {
        var tz = TimeZoneInfo.Local;
        var startLocal = DateTime.SpecifyKind(fromLocalDate.Date, DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(toLocalDate.Date.AddDays(1), DateTimeKind.Unspecified);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);
        return (fromUtc, toUtc);
    }

    private static Task<Dictionary<int, decimal>> LoadAvgPurchaseCostByProductAsync(OilChangePosDbContext db, int mainWarehouseId, CancellationToken cancellationToken)
    {
        if (mainWarehouseId == 0)
            return Task.FromResult(new Dictionary<int, decimal>());

        return db.Purchases.AsNoTracking()
            .Where(x => x.WarehouseId == mainWarehouseId)
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Units = g.Sum(x => x.Quantity),
                CostSum = g.Sum(x => x.Quantity * x.PurchasePrice)
            })
            .ToDictionaryAsync(x => x.ProductId, x => x.Units > 0 ? x.CostSum / x.Units : 0m, cancellationToken);
    }

    private static async Task<List<InventorySnapshotDto>> GetInventorySnapshotCoreAsync(OilChangePosDbContext db, int warehouseId, CancellationToken cancellationToken)
    {
        var products = await db.Products.Where(x => x.IsActive).Include(x => x.Company).ToListAsync(cancellationToken);
        var ids = products.Select(p => p.Id).ToList();
        var overrides = await BranchSalePricing.LoadOverridesAsync(db, warehouseId, ids, cancellationToken);
        var list = new List<InventorySnapshotDto>();
        foreach (var product in products)
        {
            var stock = await WarehouseStock.GetOnHandAsync(db, product.Id, warehouseId, cancellationToken);
            var unit = BranchSalePricing.EffectiveSalePrice(product.UnitPrice, overrides, product.Id);
            var label = ProductDisplayNames.CatalogDisplayName(product.Company?.Name, product.Name, product.PackageSize);
            list.Add(new InventorySnapshotDto(product.Id, label, stock, unit, stock * unit));
        }
        return list.OrderBy(x => x.ProductName).ToList();
    }
}

public class ExpenseService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IExpenseService
{
    public async Task<int> RecordExpenseAsync(decimal amount, string category, string description, DateTime expenseDateLocal, int? warehouseId, int userId, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) throw new InvalidOperationException("مبلغ المصروف يجب أن يكون أكبر من صفر.");
        category = (category ?? string.Empty).Trim();
        if (category.Length == 0) throw new InvalidOperationException("التصنيف مطلوب.");
        if (category.Length > 80) throw new InvalidOperationException("التصنيف طويل جداً.");
        description = (description ?? string.Empty).Trim();
        if (description.Length > 500) throw new InvalidOperationException("الوصف طويل جداً.");

        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(expenseDateLocal.Date, DateTimeKind.Unspecified), TimeZoneInfo.Local);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, userId, cancellationToken);
        RbacRules.EnsureExpenseForActor(actor, warehouseId);
        var e = new Expense
        {
            Amount = amount,
            Category = category,
            Description = string.IsNullOrEmpty(description) ? category : description,
            ExpenseDateUtc = utc,
            WarehouseId = warehouseId,
            CreatedByUserId = userId
        };
        db.Expenses.Add(e);
        await db.SaveChangesAsync(cancellationToken);
        return e.Id;
    }
}

public class WarehouseService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IWarehouseService
{
    public async Task<List<WarehouseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warehouses
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Type, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WarehouseDto>> GetBranchesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warehouses
            .Where(x => x.IsActive && x.Type == WarehouseType.Branch)
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Type, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseDto?> GetMainAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warehouses
            .Where(x => x.IsActive && x.Type == WarehouseType.Main)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Type, x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<WarehouseDto>> ListBranchesForAdminAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warehouses
            .Where(x => x.Type == WarehouseType.Branch)
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Type, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateBranchAsync(string name, int adminUserId, CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("اسم الفرع مطلوب.");
        if (name.Length > 100)
            throw new InvalidOperationException("اسم الفرع يجب ألا يتجاوز 100 حرف.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertWarehouseAdminAsync(db, adminUserId, cancellationToken);

        if (await db.Warehouses.AnyAsync(x => x.Name == name, cancellationToken))
            throw new InvalidOperationException("يوجد مستودع بهذا الاسم بالفعل.");

        var w = new Warehouse { Name = name, Type = WarehouseType.Branch, IsActive = true };
        db.Warehouses.Add(w);
        await db.SaveChangesAsync(cancellationToken);
        return w.Id;
    }

    public async Task UpdateBranchAsync(int branchWarehouseId, string name, bool isActive, int adminUserId, CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("اسم الفرع مطلوب.");
        if (name.Length > 100)
            throw new InvalidOperationException("اسم الفرع يجب ألا يتجاوز 100 حرف.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertWarehouseAdminAsync(db, adminUserId, cancellationToken);

        var w = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == branchWarehouseId, cancellationToken)
            ?? throw new InvalidOperationException("الفرع غير موجود.");
        if (w.Type != WarehouseType.Branch)
            throw new InvalidOperationException("يمكن تعديل الفروع فقط من هنا.");

        if (await db.Warehouses.AnyAsync(x => x.Id != branchWarehouseId && x.Name == name, cancellationToken))
            throw new InvalidOperationException("يوجد مستودع آخر يستخدم هذا الاسم.");

        w.Name = name;
        w.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task AssertWarehouseAdminAsync(OilChangePosDbContext db, int userId, CancellationToken cancellationToken)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (u.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم إدارة الفروع.");
    }
}

public class AuthService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IAuthService
{
    public async Task<AppUser?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        username = (username ?? string.Empty).Trim();
        password = password ?? string.Empty;
        if (username.Length == 0 || password.Length == 0) return null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var hash = ComputeHash(password);
        var userLower = username.ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(
            x => x.Username.ToLower() == userLower && x.IsActive,
            cancellationToken);
        if (user is null) return null;
        return string.Equals(user.PasswordHash, hash, StringComparison.OrdinalIgnoreCase) ? user : null;
    }

    public async Task<IReadOnlyList<BranchRoleUserDto>> ListBranchRoleUsersAsync(int adminUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertAuthAdminAsync(db, adminUserId, cancellationToken);
        return await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Manager || x.Role == UserRole.Cashier)
            .OrderBy(x => x.Username)
            .Select(x => new BranchRoleUserDto(x.Id, x.Username, x.HomeBranchWarehouseId))
            .ToListAsync(cancellationToken);
    }

    public async Task SetUserHomeBranchWarehouseAsync(int adminUserId, int targetUserId, int? homeBranchWarehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertAuthAdminAsync(db, adminUserId, cancellationToken);
        var target = await db.Users.FirstOrDefaultAsync(x => x.Id == targetUserId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (target.Role != UserRole.Manager && target.Role != UserRole.Cashier)
            throw new InvalidOperationException("يُحدَّد فرع الدخول لمستخدمي صلاحية الفرع (مدير فرع / كاشير) فقط.");

        if (!homeBranchWarehouseId.HasValue)
            throw new InvalidOperationException("يجب تعيين فرع لمستخدمي الفرع (مدير فرع / كاشير).");

        var wid = homeBranchWarehouseId.Value;
        var wh = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == wid, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        if (wh.Type != WarehouseType.Branch)
            throw new InvalidOperationException("فرع الدخول يجب أن يكون مستودع فرع.");
        if (!wh.IsActive)
            throw new InvalidOperationException("لا يمكن ربط مستخدم بفرع معطّل.");

        target.HomeBranchWarehouseId = homeBranchWarehouseId;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task AssertAuthAdminAsync(OilChangePosDbContext db, int userId, CancellationToken cancellationToken)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (u.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم هذه العملية.");
    }

    public static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
