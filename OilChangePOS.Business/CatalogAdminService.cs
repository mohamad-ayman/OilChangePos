using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public sealed class CatalogAdminService(IDbContextFactory<OilChangePosDbContext> dbFactory) : ICatalogAdminService
{
    public async Task<IReadOnlyList<CatalogCompanyListRowDto>> ListCompaniesForCatalogAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Companies.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CatalogCompanyListRowDto
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = c.IsActive,
                ProductCount = db.Products.Count(p => p.CompanyId == c.Id)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogProductListRowDto>> ListProductsForCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Products.AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderBy(p => p.Name)
            .Select(p => new CatalogProductListRowDto
            {
                Id = p.Id,
                CompanyId = p.CompanyId,
                Name = p.Name,
                ProductCategory = p.ProductCategory,
                PackageSize = p.PackageSize,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task SaveCatalogCompanyAsync(bool createNew, int? existingCompanyId, string name, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (createNew)
        {
            if (await db.Companies.AnyAsync(c => c.Name == name, cancellationToken))
                throw new InvalidOperationException("هذه الشركة موجودة بالفعل.");
            db.Companies.Add(new Company { Name = name, IsActive = isActive });
        }
        else
        {
            if (existingCompanyId is not { } id)
                throw new InvalidOperationException("اختر شركة من الجدول أو استخدم «إضافة شركة».");
            var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                          ?? throw new InvalidOperationException("شركة غير موجودة.");
            if (!string.Equals(company.Name, name, StringComparison.Ordinal) &&
                await db.Companies.AnyAsync(c => c.Name == name && c.Id != company.Id, cancellationToken))
                throw new InvalidOperationException("اسم الشركة مستخدم من شركة أخرى.");
            company.Name = name;
            company.IsActive = isActive;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveCatalogProductAsync(bool createNew, int companyId, int? existingProductId, string name, string category, string package, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (createNew)
        {
            if (await db.Products.AnyAsync(p =>
                    p.CompanyId == companyId && p.Name == name && p.ProductCategory == category && p.PackageSize == package, cancellationToken))
                throw new InvalidOperationException("هذا الصنف موجود بالفعل لهذه الشركة.");
            db.Products.Add(new Product
            {
                CompanyId = companyId,
                Name = name,
                ProductCategory = category,
                PackageSize = package,
                UnitPrice = 0,
                IsActive = isActive
            });
        }
        else
        {
            if (existingProductId is not { } pid)
                throw new InvalidOperationException("اختر صنفاً من الجدول أو استخدم «إضافة صنف».");
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == pid, cancellationToken)
                          ?? throw new InvalidOperationException("صنف غير صالح.");
            if (product.CompanyId != companyId)
                throw new InvalidOperationException("صنف غير صالح.");
            if (await db.Products.AnyAsync(p =>
                    p.CompanyId == companyId && p.Name == name && p.ProductCategory == category && p.PackageSize == package &&
                    p.Id != product.Id, cancellationToken))
                throw new InvalidOperationException("هناك صنف آخر بنفس الاسم والنوع والعبوة.");
            product.Name = name;
            product.ProductCategory = category;
            product.PackageSize = package;
            product.IsActive = isActive;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyComboItemDto>> ListActiveCompaniesForComboAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CompanyComboItemDto { Id = c.Id, Name = c.Name })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> PosTabProductExistsAsync(int companyId, string name, string category, string package, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Products.AnyAsync(x =>
            x.CompanyId == companyId && x.Name == name && x.ProductCategory == category && x.PackageSize == package, cancellationToken);
    }

    public async Task<int> CreatePosTabProductAsync(int companyId, string name, string category, string package, decimal unitPrice, CancellationToken cancellationToken = default)
    {
        if (await PosTabProductExistsAsync(companyId, name, category, package, cancellationToken))
            throw new InvalidOperationException("الصنف موجود مسبقاً لهذه الشركة والنوع والعبوة.");
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var product = new Product
        {
            CompanyId = companyId,
            Name = name,
            ProductCategory = category,
            PackageSize = package,
            UnitPrice = unitPrice,
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}
