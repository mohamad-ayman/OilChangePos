using Microsoft.EntityFrameworkCore;

namespace OilChangePOS.Business;

public partial class ReportService
{
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

    public async Task<List<ExpenseReportRowDto>> GetExpensesInPeriodAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        int? warehouseId,
        bool branchOperatorView,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var q = db.Expenses.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.CreatedByUser)
            .Where(x => x.ExpenseDateUtc >= fromUtc && x.ExpenseDateUtc < toUtcEx);
        // Match SumOperatingExpensesAsync: scoped site = strictly tagged rows only (same total as profit rollup).
        if (warehouseId.HasValue)
            q = q.Where(x => x.WarehouseId == warehouseId.Value);
        if (branchOperatorView)
            q = q.Where(x => x.VisibleInBranchExpenseList);

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
}
