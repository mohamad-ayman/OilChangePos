using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;

namespace OilChangePOS.Business;

public sealed class ProductCatalogService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IProductCatalogService
{
    public async Task<IReadOnlyList<ProductListDto>> GetActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProductListDto(
                p.Id,
                p.Name,
                p.ProductCategory,
                p.PackageSize,
                p.UnitPrice,
                p.Company != null ? p.Company.Name : string.Empty))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetProductSummariesAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var q = db.Products.AsNoTracking();
        if (activeOnly)
            q = q.Where(p => p.IsActive);
        return await q
            .OrderBy(p => p.Name)
            .Select(p => new ProductSummaryDto
            {
                Id = p.Id,
                CompanyId = p.CompanyId,
                CompanyName = p.Company != null ? p.Company.Name : string.Empty,
                Name = p.Name,
                ProductCategory = p.ProductCategory,
                PackageSize = p.PackageSize,
                UnitPrice = p.UnitPrice,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
