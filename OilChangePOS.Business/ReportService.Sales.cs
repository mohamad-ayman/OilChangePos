using Microsoft.EntityFrameworkCore;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public partial class ReportService
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
        var operatingExpenses = await SumOperatingExpensesAsync(db, from, to, warehouseId, cancellationToken);
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
}
