using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public class SalesService(IDbContextFactory<OilChangePosDbContext> dbFactory) : ISalesService
{
    public async Task<int> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default)
    {
        var items = NormalizeSaleItems(request.Items);
        if (request.DiscountAmount < 0)
            throw new InvalidOperationException("الخصم لا يمكن أن يكون سالباً.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, request.UserId, cancellationToken);
        var saleWarehouse = await RbacRules.RequireWarehouseAsync(db, request.WarehouseId, cancellationToken);
        RbacRules.EnsurePosSaleWarehouse(actor, saleWarehouse);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var productIds = items.Select(x => x.ProductId).ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, cancellationToken);
        var priceOverrides = await BranchSalePricing.LoadOverridesAsync(db, request.WarehouseId, productIds, cancellationToken);
        var mainWarehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainWarehouseId = mainWarehouse?.Id ?? 0;

        var subtotal = 0m;
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                throw new InvalidOperationException($"الصنف {item.ProductId} غير موجود.");

            var currentStock = await WarehouseStock.GetOnHandAsync(db, item.ProductId, request.WarehouseId, cancellationToken);
            if (currentStock < item.Quantity)
                throw new InvalidOperationException($"رصيد غير كافٍ لـ '{product.Name}'. المتاح={currentStock}، المطلوب={item.Quantity}");

            var unit = BranchSalePricing.EffectiveSalePrice(product.UnitPrice, priceOverrides, item.ProductId);
            subtotal += item.Quantity * unit;
        }

        if (request.DiscountAmount > subtotal)
            throw new InvalidOperationException("الخصم لا يمكن أن يتجاوز إجمالي الفاتورة.");

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
        foreach (var item in items)
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

    private static List<SaleItemRequest> NormalizeSaleItems(List<SaleItemRequest> items)
    {
        if (items.Count == 0)
            throw new InvalidOperationException("يجب أن تحتوي الفاتورة على صنف واحد على الأقل.");

        foreach (var item in items)
        {
            if (item.ProductId <= 0)
                throw new InvalidOperationException("كل سطر يجب أن يحدد صنفاً صالحاً.");
            if (item.Quantity <= 0)
                throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر.");
        }

        return items
            .GroupBy(x => x.ProductId)
            .Select(g => new SaleItemRequest(g.Key, g.Sum(x => x.Quantity)))
            .ToList();
    }
}
