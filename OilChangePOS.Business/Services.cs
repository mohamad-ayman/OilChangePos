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
        var products = await db.Products.Where(x => x.IsActive).ToListAsync(cancellationToken);
        var result = new List<LowStockItemDto>();
        foreach (var p in products)
        {
            var stock = await WarehouseStock.GetOnHandAsync(db, p.Id, warehouseId, cancellationToken);
            if (stock <= LowStockThreshold)
            {
                result.Add(new LowStockItemDto(p.Id, p.Name, stock, LowStockThreshold));
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

    public async Task<StockAuditResultDto> RunStockAuditAsync(int userId, int warehouseId, List<AuditLineRequest> lines, string notes, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (actor.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم تنفيذ جرد المخزون.");
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
        _ = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        if (warehouse.Type != WarehouseType.Branch)
            throw new InvalidOperationException("سعر البيع للفرع يُحدّد لمستودعات الفروع فقط.");
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
        _ = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        if (warehouse.Type != WarehouseType.Branch)
            throw new InvalidOperationException("سعر البيع للفرع يُحدّد لمستودعات الفروع فقط.");

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
                var allocatedOut = await db.StockMovements.AsNoTracking()
                    .Where(m => m.MovementType == StockMovementType.Transfer
                                && m.FromWarehouseId == request.FromWarehouseId
                                && m.SourcePurchaseId == p.Id)
                    .SumAsync(m => m.Quantity, cancellationToken);
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

            await tx.CommitAsync(cancellationToken);
            return firstId;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

public class SalesService(IDbContextFactory<OilChangePosDbContext> dbFactory) : ISalesService
{
    public async Task<int> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.Items.Any()) throw new InvalidOperationException("يجب أن تحتوي الفاتورة على صنف واحد على الأقل.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        var priceOverrides = await BranchSalePricing.LoadOverridesAsync(db, request.WarehouseId, productIds, cancellationToken);

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

            db.StockMovements.Add(new StockMovement
            {
                ProductId = item.ProductId,
                MovementType = StockMovementType.Sale,
                Quantity = item.Quantity,
                FromWarehouseId = request.WarehouseId,
                ReferenceId = invoice.Id,
                Notes = "بيع نقطة البيع"
            });
        }

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
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var warehouseId = request.WarehouseId;
        var productIds = request.Details.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var priceOverrides = await BranchSalePricing.LoadOverridesAsync(db, warehouseId, productIds, cancellationToken);
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

            db.StockMovements.Add(new StockMovement
            {
                ProductId = detail.ProductId,
                MovementType = StockMovementType.Sale,
                Quantity = detail.Quantity,
                FromWarehouseId = warehouseId,
                ReferenceId = service.Id,
                Notes = "خدمة تغيير الزيت"
            });
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

        var productLookup = await db.Products.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var topDtos = topProducts
            .Select(x => new TopSellingProductDto(productLookup.GetValueOrDefault(x.ProductId, $"Product #{x.ProductId}"), x.Quantity, x.Amount))
            .ToList();

        var inventory = await GetInventorySnapshotCoreAsync(db, warehouseId, cancellationToken);
        var lowStock = await inventoryService.GetLowStockAsync(warehouseId, cancellationToken);
        var gross = invoices.Sum(x => x.Subtotal);
        var discounts = invoices.Sum(x => x.DiscountAmount);
        var net = invoices.Sum(x => x.Total);

        var lineItems = invoiceIds.Count == 0
            ? []
            : await db.InvoiceItems.AsNoTracking().Where(x => invoiceIds.Contains(x.InvoiceId)).ToListAsync(cancellationToken);

        Dictionary<int, decimal> avgPurchaseCostByProduct = [];
        if (mainId != 0)
        {
            var costRows = await db.Purchases.AsNoTracking()
                .Where(x => x.WarehouseId == mainId)
                .GroupBy(x => x.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Units = g.Sum(x => x.Quantity),
                    CostSum = g.Sum(x => x.Quantity * x.PurchasePrice)
                })
                .ToListAsync(cancellationToken);
            avgPurchaseCostByProduct = costRows.ToDictionary(x => x.ProductId, x => x.Units > 0 ? x.CostSum / x.Units : 0m);
        }

        var estimatedCogs = lineItems.Sum(x => x.Quantity * avgPurchaseCostByProduct.GetValueOrDefault(x.ProductId, 0m));
        var estimatedGrossProfit = net - estimatedCogs;

        var soldQtyByProduct = lineItems
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var activeProducts = await db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var slowMoving = new List<SlowMovingProductDto>();
        foreach (var p in activeProducts)
        {
            var onHand = await inventoryService.GetCurrentStockAsync(p.Id, warehouseId, cancellationToken);
            var sold = soldQtyByProduct.GetValueOrDefault(p.Id, 0m);
            if (onHand >= 1 && sold < 1)
                slowMoving.Add(new SlowMovingProductDto(p.Name, onHand, sold));
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

        var transferDtos = transfers.Select(m =>
        {
            var fromName = m.FromWarehouseId is int f ? whNames.GetValueOrDefault(f, "?") : "—";
            var toName = m.ToWarehouseId is int t ? whNames.GetValueOrDefault(t, "?") : "—";
            var pname = m.Product?.Name ?? productLookup.GetValueOrDefault(m.ProductId, $"#{m.ProductId}");
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
            transferDtos);
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

        var list = new List<InvoiceProfitDto>();
        foreach (var inv in invoices)
        {
            var net = inv.Total;
            var invLines = lines.Where(l => l.InvoiceId == inv.Id).ToList();
            var cogs = invLines.Sum(l => l.Quantity * avgCost.GetValueOrDefault(l.ProductId, 0m));
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
                margin));
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

        var avgCost = await LoadAvgPurchaseCostByProductAsync(db, mainId, cancellationToken);
        var productIds = grouped.Select(x => x.ProductId).ToList();
        var names = await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return grouped
            .Select(x =>
            {
                var cogs = x.Qty * avgCost.GetValueOrDefault(x.ProductId, 0m);
                return new ProductProfitDto(x.ProductId, names.GetValueOrDefault(x.ProductId, $"#{x.ProductId}"), x.Qty, x.Revenue, cogs, x.Revenue - cogs);
            })
            .OrderByDescending(x => x.EstimatedGrossProfit)
            .ToList();
    }

    public async Task<ProfitRollupDto> GetProfitRollupAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        var products = await GetProductProfitBreakdownAsync(fromLocalDate, toLocalDate, warehouseId, cancellationToken);
        var rev = products.Sum(x => x.Revenue);
        var cogs = products.Sum(x => x.EstimatedCogs);
        return new ProfitRollupDto(rev, cogs, rev - cogs);
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
        StockMovementType.Adjust => "تسوية",
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
        var productLookup = await db.Products.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return transfers.Select(m =>
        {
            var fn = m.FromWarehouseId is int f ? whNames.GetValueOrDefault(f, "—") : "—";
            var tn = m.ToWarehouseId is int t ? whNames.GetValueOrDefault(t, "—") : "—";
            var pname = m.Product?.Name ?? productLookup.GetValueOrDefault(m.ProductId, $"#{m.ProductId}");
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

        var names = await db.Products.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return top.Select(x => new TopSellingProductDto(names.GetValueOrDefault(x.ProductId, $"#{x.ProductId}"), x.Quantity, x.Amount)).ToList();
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

        var active = await db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var list = new List<SlowMovingProductDto>();
        foreach (var p in active)
        {
            var onHand = await inventoryService.GetCurrentStockAsync(p.Id, warehouseId, cancellationToken);
            var sold = soldByProduct.GetValueOrDefault(p.Id, 0m);
            if (onHand >= 1 && sold < 1)
                list.Add(new SlowMovingProductDto(p.Name, onHand, sold));
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
                pr.Name,
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
                p.Product?.Name ?? $"#{p.ProductId}",
                p.Quantity,
                amt,
                "شراء للمستودع المحدد",
                p.Notes,
                p.CreatedByUser?.Username ?? "—"));
        }

        var transfersIn = await db.StockMovements.AsNoTracking()
            .Include(x => x.Product)
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
                m.Product?.Name ?? $"#{m.ProductId}",
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
        var products = await db.Products.Where(x => x.IsActive).ToListAsync(cancellationToken);
        var ids = products.Select(p => p.Id).ToList();
        var overrides = await BranchSalePricing.LoadOverridesAsync(db, warehouseId, ids, cancellationToken);
        var list = new List<InventorySnapshotDto>();
        foreach (var product in products)
        {
            var stock = await WarehouseStock.GetOnHandAsync(db, product.Id, warehouseId, cancellationToken);
            var unit = BranchSalePricing.EffectiveSalePrice(product.UnitPrice, overrides, product.Id);
            list.Add(new InventorySnapshotDto(product.Id, product.Name, stock, unit, stock * unit));
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

    public static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
