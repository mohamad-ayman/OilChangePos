using Microsoft.EntityFrameworkCore;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public partial class ReportService
{
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
}
