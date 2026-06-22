using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public class ServiceOrderService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IServiceOrderService
{
    public async Task<int> CreateOilChangeServiceAsync(OilChangeRequest request, CancellationToken cancellationToken = default)
    {
        var details = NormalizeServiceDetails(request.Details);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, request.UserId, cancellationToken);
        var saleWarehouse = await RbacRules.RequireWarehouseAsync(db, request.WarehouseId, cancellationToken);
        RbacRules.EnsurePosSaleWarehouse(actor, saleWarehouse);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var warehouseId = request.WarehouseId;
        var productIds = details.Select(x => x.ProductId).ToList();
        var products = await db.Products.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var priceOverrides = await BranchSalePricing.LoadOverridesAsync(db, warehouseId, productIds, cancellationToken);
        var mainWarehouse = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken);
        var mainWarehouseId = mainWarehouse?.Id ?? 0;
        decimal subtotal = 0;

        foreach (var detail in details)
        {
            if (!products.TryGetValue(detail.ProductId, out var p))
                throw new InvalidOperationException($"الصنف {detail.ProductId} غير موجود.");

            var stock = await WarehouseStock.GetOnHandAsync(db, detail.ProductId, warehouseId, cancellationToken);
            if (stock < detail.Quantity) throw new InvalidOperationException($"رصيد غير كافٍ للصنف {detail.ProductId}");
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

        foreach (var detail in details)
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

    private static List<SaleItemRequest> NormalizeServiceDetails(List<SaleItemRequest>? details)
    {
        if (details is null || details.Count == 0)
            throw new InvalidOperationException("يجب إضافة صنف واحد على الأقل للخدمة.");

        var merged = new Dictionary<int, decimal>();
        foreach (var detail in details)
        {
            if (detail.ProductId <= 0)
                throw new InvalidOperationException("معرّف الصنف غير صالح.");
            if (detail.Quantity <= 0)
                throw new InvalidOperationException("كمية الخدمة يجب أن تكون أكبر من صفر.");

            merged[detail.ProductId] = merged.TryGetValue(detail.ProductId, out var quantity)
                ? quantity + detail.Quantity
                : detail.Quantity;
        }

        return merged.Select(x => new SaleItemRequest(x.Key, x.Value)).ToList();
    }
}
