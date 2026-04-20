using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

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
