using OilChangePOS.Data;
using OilChangePOS.Domain;
using Microsoft.EntityFrameworkCore;

namespace OilChangePOS.Business;

/// <summary>Server-side RBAC helpers (warehouse scope). Call from services after loading the acting user.</summary>
public static class RbacRules
{
    public static async Task<AppUser> RequireUserAsync(
        OilChangePosDbContext db,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var actor = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        return actor;
    }

    public static async Task<Warehouse> RequireWarehouseAsync(
        OilChangePosDbContext db,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        var wh = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        return wh;
    }

    public static void EnsurePosSaleWarehouse(AppUser actor, Warehouse warehouse)
    {
        if (actor.Role.IsAdmin())
            return;
        if (!actor.Role.IsBranchStaff())
            throw new InvalidOperationException("لا يُسمح بتنفيذ البيع لهذا الدور.");
        if (warehouse.Type != WarehouseType.Branch)
            throw new InvalidOperationException("نقطة البيع متاحة على فروع فقط.");
        if (actor.HomeBranchWarehouseId != warehouse.Id)
            throw new InvalidOperationException("لا يُسمح بالبيع خارج فرعك المعيّن.");
    }

    public static void EnsureBranchInventoryOrPricing(AppUser actor, Warehouse warehouse)
    {
        if (actor.Role.IsAdmin())
            return;
        if (!actor.Role.IsBranchStaff())
            throw new InvalidOperationException("لا يُسمح بهذه العملية لهذا الدور.");
        if (warehouse.Type != WarehouseType.Branch)
            throw new InvalidOperationException("هذه العملية متاحة على مستودعات الفروع فقط.");
        if (actor.HomeBranchWarehouseId != warehouse.Id)
            throw new InvalidOperationException("لا يُسمح بالوصول خارج فرعك المعيّن.");
    }

    public static void EnsureBranchStockAudit(AppUser actor, Warehouse warehouse)
    {
        if (actor.Role.IsAdmin())
            return;
        if (!actor.Role.IsBranchStaff())
            throw new InvalidOperationException("لا يُسمح بتنفيذ الجرد لهذا الدور.");
        EnsureBranchInventoryOrPricing(actor, warehouse);
    }

    public static void EnsureExpenseForActor(AppUser actor, int? expenseWarehouseId)
    {
        if (actor.Role.IsAdmin())
            return;
        if (!actor.Role.IsBranchStaff())
            throw new InvalidOperationException("لا يُسمح بتسجيل المصروفات لهذا الدور.");
        if (actor.HomeBranchWarehouseId is not { } home)
            throw new InvalidOperationException("يجب ربط المستخدم بفرع قبل تسجيل المصروفات.");
        if (expenseWarehouseId is not { } wid || wid != home)
            throw new InvalidOperationException("يمكن تسجيل مصروفات فرعك فقط.");
    }
}
