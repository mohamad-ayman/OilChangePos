using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

/// <summary>
/// Aggregated reporting facade. Implementation is split into partial files by concern:
/// <list type="bullet">
/// <item><description><c>ReportService.Sales.cs</c> — daily sales, dashboard, periodic summaries, top sellers.</description></item>
/// <item><description><c>ReportService.Profit.cs</c> — invoice/product profit, profit rollup.</description></item>
/// <item><description><c>ReportService.Inventory.cs</c> — snapshots, stock-from-movements, slow-moving, history, transfers.</description></item>
/// <item><description><c>ReportService.Branch.cs</c> — branch ledgers (sales lines, incoming, by seller, transfers).</description></item>
/// <item><description><c>ReportService.Cash.cs</c> — daily cash flow and expense register.</description></item>
/// </list>
/// This file holds the constructor and shared private helpers used by every partial.
/// </summary>
public partial class ReportService(IDbContextFactory<OilChangePosDbContext> dbFactory, IInventoryService inventoryService) : IReportService
{
    // ---- COGS computation (POS invoice batch ledger) ----

    private sealed class PosInvoiceCogsBatchComputation
    {
        public Dictionary<int, decimal> CogsByInvoiceId { get; } = new();
        public Dictionary<int, bool> ContainsEstimatedByInvoiceId { get; } = new();
        public Dictionary<int, decimal> CogsByProductId { get; } = new();
        public Dictionary<int, bool> ContainsEstimatedByProductId { get; } = new();
        public bool AnyContainsEstimated { get; set; }
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

    // ---- Operating expenses ----

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

    // ---- Catalog / product label helpers ----

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

    // ---- Date / label conversions ----

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
}
