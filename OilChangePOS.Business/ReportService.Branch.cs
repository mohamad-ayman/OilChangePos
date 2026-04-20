using Microsoft.EntityFrameworkCore;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public partial class ReportService
{
    public async Task<List<TransferLedgerRowDto>> GetBranchTransferLedgerAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var (fromUtc, toUtcEx) = LocalDateRangeToUtcExclusive(fromLocalDate, toLocalDate);
        var transfers = await db.StockMovements.AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.MovementType == StockMovementType.Transfer
                        && x.MovementDateUtc >= fromUtc
                        && x.MovementDateUtc < toUtcEx
                        && (x.FromWarehouseId == warehouseId || x.ToWarehouseId == warehouseId))
            .OrderByDescending(x => x.MovementDateUtc)
            .Take(2000)
            .ToListAsync(cancellationToken);
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
            join co in db.Companies.AsNoTracking() on pr.CompanyId equals co.Id into coJoin
            from co in coJoin.DefaultIfEmpty()
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
                ProductDisplayNames.CatalogDisplayName(co == null ? null : co.Name, pr.Name, pr.PackageSize),
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
            .ThenInclude(p => p!.Company)
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
                ProductDisplayNames.CatalogDisplayName(p.Product?.Company?.Name, p.Product?.Name ?? $"#{p.ProductId}", p.Product?.PackageSize),
                p.Quantity,
                amt,
                "شراء للمستودع المحدد",
                p.Notes,
                p.CreatedByUser?.Username ?? "—"));
        }

        var transfersIn = await db.StockMovements.AsNoTracking()
            .Include(x => x.Product)
            .ThenInclude(p => p!.Company)
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
                ProductDisplayNames.CatalogDisplayName(m.Product?.Company?.Name, m.Product?.Name ?? $"#{m.ProductId}", m.Product?.PackageSize),
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
}
