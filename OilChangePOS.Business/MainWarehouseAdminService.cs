using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public sealed class MainWarehouseAdminService(
    IDbContextFactory<OilChangePosDbContext> dbFactory,
    IInventoryService inventoryService,
    IWarehouseService warehouseService) : IMainWarehouseAdminService
{
    public async Task<IReadOnlyList<MainWarehouseCatalogEntryDto>> GetCatalogEntriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var plist = await db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.Company)
            .OrderBy(x => x.Company!.Name)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return plist.Select(p => new MainWarehouseCatalogEntryDto
        {
            Id = p.Id,
            CompanyId = p.CompanyId,
            CompanyName = p.Company?.Name ?? string.Empty,
            Name = p.Name,
            ProductCategory = p.ProductCategory,
            PackageSize = p.PackageSize,
            IsPlaceholder = false
        }).ToList();
    }

    public async Task<IReadOnlyList<MainWarehouseGridRowDto>> GetGridRowsAsync(CancellationToken cancellationToken = default)
    {
        var main = await warehouseService.GetMainAsync(cancellationToken);
        if (main is null)
            return [];

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var purchases = await db.Purchases
            .AsNoTracking()
            .Include(x => x.Product)
            .ThenInclude(p => p!.Company)
            .Where(x => x.WarehouseId == main.Id)
            .OrderByDescending(x => x.PurchaseDate)
            .ToListAsync(cancellationToken);

        var rows = new List<MainWarehouseGridRowDto>();
        var productIdsWithPurchaseLine = purchases.Select(p => p.ProductId).ToHashSet();
        var onHandByProduct = new Dictionary<int, decimal>();
        foreach (var purchase in purchases)
        {
            if (purchase.Product is null) continue;
            if (!onHandByProduct.ContainsKey(purchase.ProductId))
                onHandByProduct[purchase.ProductId] = await inventoryService.GetCurrentStockAsync(purchase.ProductId, main.Id, cancellationToken);
            var onHand = onHandByProduct[purchase.ProductId];
            rows.Add(new MainWarehouseGridRowDto
            {
                ProductId = purchase.ProductId,
                PurchaseId = purchase.Id,
                CompanyName = purchase.Product.Company?.Name ?? string.Empty,
                InventoryName = purchase.Product.Name,
                ProductionDate = purchase.ProductionDate,
                PurchasedQuantity = purchase.Quantity,
                OnHandAtMain = onHand,
                PurchasePrice = purchase.PurchasePrice,
                PurchaseDate = purchase.PurchaseDate,
                PackageSize = purchase.Product.PackageSize,
                ProductCategory = purchase.Product.ProductCategory
            });
        }

        var products = await db.Products
            .AsNoTracking()
            .Include(x => x.Company)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        foreach (var p in products)
        {
            if (productIdsWithPurchaseLine.Contains(p.Id))
                continue;
            var qty = await inventoryService.GetCurrentStockAsync(p.Id, main.Id, cancellationToken);
            if (qty <= 0)
                continue;
            rows.Add(new MainWarehouseGridRowDto
            {
                ProductId = p.Id,
                PurchaseId = null,
                CompanyName = p.Company?.Name ?? string.Empty,
                InventoryName = p.Name,
                ProductionDate = DateTime.Today,
                PurchasedQuantity = qty,
                OnHandAtMain = qty,
                PurchasePrice = 0,
                PurchaseDate = DateTime.Today,
                PackageSize = p.PackageSize,
                ProductCategory = p.ProductCategory
            });
        }

        rows.Sort(static (a, b) =>
        {
            var cmp = b.PurchaseDate.CompareTo(a.PurchaseDate);
            if (cmp != 0)
                return cmp;
            var idA = a.PurchaseId ?? 0;
            var idB = b.PurchaseId ?? 0;
            var idCmp = idB.CompareTo(idA);
            if (idCmp != 0)
                return idCmp;
            return string.Compare(a.InventoryName, b.InventoryName, StringComparison.OrdinalIgnoreCase);
        });

        foreach (var g in rows.Where(r => r.PurchaseId.HasValue).GroupBy(r => r.ProductId))
        {
            var ordered = g.OrderBy(r => r.PurchaseDate).ThenBy(r => r.PurchaseId!.Value).ToList();
            var total = ordered.Count;
            for (var i = 0; i < total; i++)
            {
                ordered[i].BatchNumber = i + 1;
                ordered[i].BatchTotal = total;
                ordered[i].BatchLabel = total > 1 ? $"{i + 1}/{total}" : string.Empty;
            }
        }

        foreach (var group in rows.Where(r => r.ProductId != 0).GroupBy(r => r.ProductId))
        {
            var keeper = group
                .OrderByDescending(r => r.PurchaseDate)
                .ThenByDescending(r => r.PurchaseId ?? int.MinValue)
                .First();
            foreach (var r in group)
            {
                if (!ReferenceEquals(r, keeper))
                    r.OnHandAtMain = null;
            }
        }

        return rows;
    }

    public async Task UpdatePurchaseLineAsync(UpdateMainWarehousePurchaseRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var purchase = await db.Purchases.FirstOrDefaultAsync(x => x.Id == request.PurchaseId, cancellationToken)
                       ?? throw new InvalidOperationException("عملية الشراء غير موجودة.");
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == purchase.ProductId, cancellationToken)
                      ?? throw new InvalidOperationException("الصنف غير موجود.");
        if (product.Id != request.ProductId)
            throw new InvalidOperationException("تعارض معرف الصنف.");
        product.Name = request.ProductName;
        product.CompanyId = request.CompanyId;
        product.ProductCategory = request.ProductCategory;
        product.PackageSize = request.PackageSize;
        purchase.Quantity = request.Quantity;
        purchase.PurchasePrice = request.PurchasePrice;
        purchase.ProductionDate = request.ProductionDate.Date;
        purchase.PurchaseDate = request.PurchaseDate.Date;

        var movement = await db.StockMovements.FirstOrDefaultAsync(x =>
            x.ReferenceId == purchase.Id &&
            x.ProductId == purchase.ProductId &&
            x.MovementType == StockMovementType.Purchase, cancellationToken);
        movement ??= await db.StockMovements.FirstOrDefaultAsync(x =>
            x.ReferenceId == purchase.Id && x.ProductId == purchase.ProductId, cancellationToken);
        if (movement is not null)
        {
            movement.MovementType = StockMovementType.Purchase;
            movement.Quantity = purchase.Quantity;
            movement.ToWarehouseId = purchase.WarehouseId;
            movement.FromWarehouseId = null;
            movement.Notes = "تعديل يدوي من شاشة المستودع الرئيسي";
        }
        else
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = purchase.ProductId,
                MovementType = StockMovementType.Purchase,
                Quantity = purchase.Quantity,
                ToWarehouseId = purchase.WarehouseId,
                ReferenceId = purchase.Id,
                Notes = "تعديل يدوي من شاشة المستودع الرئيسي (إنشاء حركة)"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletePurchaseLineAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var purchase = await db.Purchases.FirstOrDefaultAsync(x => x.Id == purchaseId, cancellationToken)
                       ?? throw new InvalidOperationException("عملية الشراء غير موجودة.");
        var movements = await db.StockMovements
            .Where(x => x.ReferenceId == purchase.Id && x.ProductId == purchase.ProductId)
            .ToListAsync(cancellationToken);
        db.StockMovements.RemoveRange(movements);
        db.Purchases.Remove(purchase);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> ImportExcelLinesAsync(int userId, int mainWarehouseId, IReadOnlyList<MainWarehouseExcelImportLineDto> lines, CancellationToken cancellationToken = default)
    {
        var imported = 0;
        foreach (var line in lines)
        {
            if (line.Quantity <= 0) continue;
            var name = line.ProductName.Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var category = string.IsNullOrWhiteSpace(line.Category) ? "Oil" : line.Category.Trim();
            var pack = string.IsNullOrWhiteSpace(line.PackageSize) ? "Unit" : line.PackageSize.Trim();
            var coName = string.IsNullOrWhiteSpace(line.CompanyName) ? "عام" : line.CompanyName.Trim();

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var companyRow = await db.Companies.FirstOrDefaultAsync(c => c.Name == coName, cancellationToken);
            if (companyRow is null)
            {
                companyRow = new Company { Name = coName, IsActive = true };
                db.Companies.Add(companyRow);
                await db.SaveChangesAsync(cancellationToken);
            }

            var product = await db.Products.FirstOrDefaultAsync(x =>
                x.CompanyId == companyRow.Id && x.Name == name && x.ProductCategory == category && x.PackageSize == pack, cancellationToken);
            if (product is null)
            {
                product = new Product
                {
                    CompanyId = companyRow.Id,
                    Name = name,
                    ProductCategory = category,
                    PackageSize = pack,
                    UnitPrice = 0,
                    IsActive = true
                };
                db.Products.Add(product);
                await db.SaveChangesAsync(cancellationToken);
            }

            await inventoryService.AddStockAsync(new PurchaseStockRequest(
                product.Id,
                line.Quantity,
                line.PurchasePrice,
                line.ProductionDate,
                line.PurchaseDate,
                mainWarehouseId,
                "استيراد من Excel",
                userId), cancellationToken);
            imported++;
        }

        return imported;
    }
}
