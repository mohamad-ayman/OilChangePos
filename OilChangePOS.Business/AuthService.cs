using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public class AuthService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IAuthService
{
    public async Task<AppUser?> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        username = (username ?? string.Empty).Trim();
        password = password ?? string.Empty;
        if (username.Length == 0 || password.Length == 0) return null;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var hash = ComputeHash(password);
        var userLower = username.ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(
            x => x.Username.ToLower() == userLower && x.IsActive,
            cancellationToken);
        if (user is null) return null;
        return string.Equals(user.PasswordHash, hash, StringComparison.OrdinalIgnoreCase) ? user : null;
    }

    public async Task<IReadOnlyList<BranchRoleUserDto>> ListBranchRoleUsersAsync(int adminUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertAuthAdminAsync(db, adminUserId, cancellationToken);
        return await db.Users.AsNoTracking()
            .Where(x => x.Role == UserRole.Manager || x.Role == UserRole.Cashier)
            .OrderBy(x => x.Username)
            .Select(x => new BranchRoleUserDto(x.Id, x.Username, x.HomeBranchWarehouseId))
            .ToListAsync(cancellationToken);
    }

    public async Task SetUserHomeBranchWarehouseAsync(int adminUserId, int targetUserId, int? homeBranchWarehouseId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await AssertAuthAdminAsync(db, adminUserId, cancellationToken);
        var target = await db.Users.FirstOrDefaultAsync(x => x.Id == targetUserId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (target.Role != UserRole.Manager && target.Role != UserRole.Cashier)
            throw new InvalidOperationException("يُحدَّد فرع الدخول لمستخدمي صلاحية الفرع (مدير فرع / كاشير) فقط.");

        if (!homeBranchWarehouseId.HasValue)
            throw new InvalidOperationException("يجب تعيين فرع لمستخدمي الفرع (مدير فرع / كاشير).");

        var wid = homeBranchWarehouseId.Value;
        var wh = await db.Warehouses.FirstOrDefaultAsync(x => x.Id == wid, cancellationToken)
            ?? throw new InvalidOperationException("المستودع غير موجود.");
        if (wh.Type != WarehouseType.Branch)
            throw new InvalidOperationException("فرع الدخول يجب أن يكون مستودع فرع.");
        if (!wh.IsActive)
            throw new InvalidOperationException("لا يمكن ربط مستخدم بفرع معطّل.");

        target.HomeBranchWarehouseId = homeBranchWarehouseId;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task AssertAuthAdminAsync(OilChangePosDbContext db, int userId, CancellationToken cancellationToken)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (u.Role != UserRole.Admin)
            throw new InvalidOperationException("المسؤولون فقط يمكنهم هذه العملية.");
    }

    public static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
