using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

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
