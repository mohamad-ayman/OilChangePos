using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public class WarehouseService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IWarehouseService
{
    public async Task<List<WarehouseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warehouses
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Type, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<WarehouseDto>> GetBranchesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warehouses
            .Where(x => x.IsActive && x.Type == WarehouseType.Branch)
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Type, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<WarehouseDto?> GetMainAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warehouses
            .Where(x => x.IsActive && x.Type == WarehouseType.Main)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Type, x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<WarehouseDto>> ListBranchesForAdminAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Warehouses
            .Where(x => x.Type == WarehouseType.Branch)
            .OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.Type, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateBranchAsync(string name, int adminUserId, CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("اسم الفرع مطلوب.");
        if (name.Length > 100)
            throw new InvalidOperationException("اسم الفرع يجب ألا يتجاوز 100 حرف.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertWarehouseAdminAsync(db, adminUserId, cancellationToken);

        if (await db.Warehouses.AnyAsync(x => x.Name == name, cancellationToken))
            throw new InvalidOperationException("يوجد مستودع بهذا الاسم بالفعل.");

        var w = new Warehouse { Name = name, Type = WarehouseType.Branch, IsActive = true };
        db.Warehouses.Add(w);
        await db.SaveChangesAsync(cancellationToken);
        return w.Id;
    }

    public async Task UpdateBranchAsync(int branchWarehouseId, string name, bool isActive, int adminUserId, CancellationToken cancellationToken = default)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("اسم الفرع مطلوب.");
        if (name.Length > 100)
            throw new InvalidOperationException("اسم الفرع يجب ألا يتجاوز 100 حرف.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertWarehouseAdminAsync(db, adminUserId, cancellationToken);

        var w = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == branchWarehouseId, cancellationToken)
            ?? throw new InvalidOperationException("الفرع غير موجود.");
        if (w.Type != WarehouseType.Branch)
            throw new InvalidOperationException("يمكن تعديل الفروع فقط من هنا.");

        if (await db.Warehouses.AnyAsync(x => x.Id != branchWarehouseId && x.Name == name, cancellationToken))
            throw new InvalidOperationException("يوجد مستودع آخر يستخدم هذا الاسم.");

        w.Name = name;
        w.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task AssertWarehouseAdminAsync(OilChangePosDbContext db, int userId, CancellationToken cancellationToken)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (u.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم إدارة الفروع.");
    }
}
