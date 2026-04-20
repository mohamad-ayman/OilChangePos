using Microsoft.EntityFrameworkCore;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public partial class ReportService
{
    public async Task<List<InventorySnapshotDto>> GetInventorySnapshotAsync(int warehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await GetInventorySnapshotCoreAsync(db, warehouseId, cancellationToken);
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
}
