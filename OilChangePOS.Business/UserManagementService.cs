using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public sealed class UserManagementService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IUserManagementService
{
    private const int MinPasswordLength = 6;
    private const int MinUsernameLength = 2;
    private const int MaxUsernameLength = 50;

    public async Task<IReadOnlyList<AdminUserRowDto>> ListUsersAsync(int requestingUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertAuthAdminAsync(db, requestingUserId, cancellationToken);
        return await db.Users.AsNoTracking()
            .Include(u => u.HomeBranchWarehouse)
            .OrderBy(u => u.Username)
            .Select(u => new AdminUserRowDto(
                u.Id,
                u.Username,
                u.Role.ToString(),
                u.IsActive,
                u.HomeBranchWarehouseId,
                u.HomeBranchWarehouse != null ? u.HomeBranchWarehouse.Name : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateUserAsync(
        int requestingUserId,
        string username,
        string password,
        UserRole role,
        int? homeBranchWarehouseId,
        CancellationToken cancellationToken = default)
    {
        username = (username ?? string.Empty).Trim();
        password ??= string.Empty;
        ValidateUsername(username);
        if (password.Length < MinPasswordLength)
            throw new InvalidOperationException($"كلمة المرور يجب ألا تقل عن {MinPasswordLength} أحرف.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertAuthAdminAsync(db, requestingUserId, cancellationToken);

        var exists = await db.Users.AnyAsync(
            u => u.Username.ToLower() == username.ToLower(),
            cancellationToken);
        if (exists)
            throw new InvalidOperationException("اسم المستخدم مستخدم بالفعل.");

        var resolvedHome = await ResolveHomeBranchForRoleAsync(db, role, homeBranchWarehouseId, cancellationToken);

        var user = new AppUser
        {
            Username = username,
            PasswordHash = AuthService.ComputeHash(password),
            Role = role,
            IsActive = true,
            HomeBranchWarehouseId = resolvedHome
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public async Task UpdateUserAsync(
        int requestingUserId,
        int userId,
        UserRole role,
        bool isActive,
        int? homeBranchWarehouseId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertAuthAdminAsync(db, requestingUserId, cancellationToken);

        if (userId == requestingUserId && !isActive)
            throw new InvalidOperationException("لا يمكن تعطيل حسابك أثناء الجلسة الحالية.");

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");

        var resolvedHome = await ResolveHomeBranchForRoleAsync(db, role, homeBranchWarehouseId, cancellationToken);

        target.Role = role;
        target.IsActive = isActive;
        target.HomeBranchWarehouseId = resolvedHome;

        await db.SaveChangesAsync(cancellationToken);
        await EnsureAtLeastOneActiveAdminAsync(db, cancellationToken);
    }

    public async Task SetPasswordAsync(int requestingUserId, int userId, string newPassword, CancellationToken cancellationToken = default)
    {
        newPassword ??= string.Empty;
        if (newPassword.Length < MinPasswordLength)
            throw new InvalidOperationException($"كلمة المرور يجب ألا تقل عن {MinPasswordLength} أحرف.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertAuthAdminAsync(db, requestingUserId, cancellationToken);

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");

        target.PasswordHash = AuthService.ComputeHash(newPassword);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateUsername(string username)
    {
        if (username.Length < MinUsernameLength)
            throw new InvalidOperationException("اسم المستخدم قصير جداً.");
        if (username.Length > MaxUsernameLength)
            throw new InvalidOperationException($"اسم المستخدم يجب ألا يتجاوز {MaxUsernameLength} حرفاً.");
    }

    private static async Task<int?> ResolveHomeBranchForRoleAsync(
        OilChangePosDbContext db,
        UserRole role,
        int? homeBranchWarehouseId,
        CancellationToken cancellationToken)
    {
        if (role == UserRole.Admin)
            return null;

        if (!homeBranchWarehouseId.HasValue)
            throw new InvalidOperationException("يجب تعيين فرع لمستخدمي الفرع (مدير فرع / كاشير).");

        var wh = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == homeBranchWarehouseId.Value, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        if (wh.Type != WarehouseType.Branch)
            throw new InvalidOperationException("فرع المستخدم يجب أن يكون مستودع فرع.");
        if (!wh.IsActive)
            throw new InvalidOperationException("لا يمكن ربط مستخدم بفرع معطّل.");

        return homeBranchWarehouseId.Value;
    }

    private static async Task EnsureAtLeastOneActiveAdminAsync(OilChangePosDbContext db, CancellationToken cancellationToken)
    {
        var n = await db.Users.CountAsync(u => u.IsActive && u.Role == UserRole.Admin, cancellationToken);
        if (n == 0)
            throw new InvalidOperationException("يجب أن يبقى مسؤول واحد على الأقل نشطاً في النظام.");
    }

    private static async Task AssertAuthAdminAsync(OilChangePosDbContext db, int userId, CancellationToken cancellationToken)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (u.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم هذه العملية.");
    }
}
