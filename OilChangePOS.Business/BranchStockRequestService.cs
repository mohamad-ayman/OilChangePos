using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public sealed class BranchStockRequestService(
    IDbContextFactory<OilChangePosDbContext> dbFactory,
    ITransferService transfers) : IBranchStockRequestService
{
    public async Task<int> CreateForHomeBranchAsync(int userId, CreateBranchStockRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Quantity <= 0)
            throw new InvalidOperationException("الكمية يجب أن تكون أكبر من صفر.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, userId, cancellationToken);
        if (actor.Role.IsAdmin())
            throw new InvalidOperationException("المسؤول لا يُنشئ طلبات توريد من هنا — استخدم شاشة الفروع أو أنشئ تحويلاً مباشرة.");
        if (!actor.Role.IsBranchStaff())
            throw new InvalidOperationException("لا يُسمح بهذه العملية لهذا الدور.");
        if (actor.HomeBranchWarehouseId is not { } branchId)
            throw new InvalidOperationException("يجب ربط المستخدم بفرع قبل إرسال طلب توريد.");

        var branchWh = await RbacRules.RequireWarehouseAsync(db, branchId, cancellationToken);
        RbacRules.EnsureBranchInventoryOrPricing(actor, branchWh);

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.ProductId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("الصنف غير موجود أو غير مفعّل.");

        var row = new BranchStockRequest
        {
            BranchWarehouseId = branchId,
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            Notes = (dto.Notes ?? string.Empty).Trim(),
            Status = BranchStockRequestStatus.Pending,
            RequestedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BranchStockRequests.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public async Task<IReadOnlyList<BranchStockRequestRowDto>> ListAsync(
        int userId,
        int? branchWarehouseIdFilter,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, userId, cancellationToken);

        IQueryable<BranchStockRequest> q = db.BranchStockRequests.AsNoTracking()
            .Include(x => x.BranchWarehouse)
            .Include(x => x.Product).ThenInclude(p => p.Company)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ResolvedByUser)
            .OrderByDescending(x => x.CreatedAtUtc);

        if (actor.Role.IsAdmin())
        {
            if (branchWarehouseIdFilter is { } wh)
                q = q.Where(x => x.BranchWarehouseId == wh);
        }
        else
        {
            if (!actor.Role.IsBranchStaff() || actor.HomeBranchWarehouseId is not { } home)
                throw new InvalidOperationException("لا يُسمح بعرض الطلبات.");
            q = q.Where(x => x.BranchWarehouseId == home);
        }

        var list = await q.Take(500).ToListAsync(cancellationToken);
        return list.Select(MapRow).ToList();
    }

    public async Task RejectAsync(int adminUserId, int requestId, string? notes, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, adminUserId, cancellationToken);
        if (!actor.Role.IsAdmin())
            throw new InvalidOperationException("رفض الطلب متاح للمسؤولين فقط.");

        var row = await db.BranchStockRequests.FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException("الطلب غير موجود.");
        if (row.Status != BranchStockRequestStatus.Pending)
            throw new InvalidOperationException("يمكن رفض الطلبات المعلّقة فقط.");

        row.Status = BranchStockRequestStatus.Rejected;
        row.ResolvedByUserId = adminUserId;
        row.ResolvedAtUtc = DateTime.UtcNow;
        row.ResolutionNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FulfillAsync(int adminUserId, int requestId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, adminUserId, cancellationToken);
        if (!actor.Role.IsAdmin())
            throw new InvalidOperationException("تنفيذ الطلب متاح للمسؤولين فقط.");

        var row = await db.BranchStockRequests.FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException("الطلب غير موجود.");
        if (row.Status != BranchStockRequestStatus.Pending)
            throw new InvalidOperationException("يمكن تنفيذ الطلبات المعلّقة فقط.");

        var main = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Type == WarehouseType.Main, cancellationToken)
            ?? throw new InvalidOperationException("لم يُعثر على مستودع رئيسي.");
        var toWh = await db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == row.BranchWarehouseId, cancellationToken)
            ?? throw new InvalidOperationException("مستودع الفرع غير موجود.");
        if (toWh.Type != WarehouseType.Branch)
            throw new InvalidOperationException("طلب التوريد يجب أن يستهدف فرعاً.");

        var transferNotes = $"طلب توريد #{row.Id}";
        var movementId = await transfers.TransferStockAsync(
            new TransferStockRequest(
                row.ProductId,
                row.Quantity,
                main.Id,
                row.BranchWarehouseId,
                transferNotes,
                adminUserId),
            cancellationToken);

        row.Status = BranchStockRequestStatus.Fulfilled;
        row.ResolvedByUserId = adminUserId;
        row.ResolvedAtUtc = DateTime.UtcNow;
        row.ResolutionNotes = null;
        row.FulfillmentStockMovementId = movementId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelOwnPendingAsync(int userId, int requestId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, userId, cancellationToken);
        if (!actor.Role.IsBranchStaff() || actor.HomeBranchWarehouseId is not { } home)
            throw new InvalidOperationException("لا يُسمح بإلغاء الطلب.");

        var row = await db.BranchStockRequests.FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new InvalidOperationException("الطلب غير موجود.");
        if (row.BranchWarehouseId != home)
            throw new InvalidOperationException("لا يمكن إلغاء طلب فرع آخر.");
        if (row.Status != BranchStockRequestStatus.Pending)
            throw new InvalidOperationException("يمكن إلغاء الطلبات المعلّقة فقط.");

        row.Status = BranchStockRequestStatus.Cancelled;
        row.ResolvedByUserId = userId;
        row.ResolvedAtUtc = DateTime.UtcNow;
        row.ResolutionNotes = "ألغاه الطالب";
        await db.SaveChangesAsync(cancellationToken);
    }

    private static BranchStockRequestRowDto MapRow(BranchStockRequest x)
    {
        var company = x.Product.Company?.Name;
        var display = ProductDisplayNames.CatalogLine(company, x.Product.Name);
        return new BranchStockRequestRowDto(
            x.Id,
            x.BranchWarehouseId,
            x.BranchWarehouse.Name,
            x.ProductId,
            display,
            x.Quantity,
            x.Notes,
            x.Status.ToString(),
            x.RequestedByUserId,
            x.RequestedByUser.Username,
            x.CreatedAtUtc,
            x.ResolvedByUserId,
            x.ResolvedByUser?.Username,
            x.ResolvedAtUtc,
            x.ResolutionNotes,
            x.FulfillmentStockMovementId);
    }
}
