using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Security;

public static class ControllerAuthExtensions
{
    public static bool TryGetRequiredUserId(this ControllerBase controller, out int userId)
    {
        var sub = controller.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? controller.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub) || !int.TryParse(sub, out userId))
        {
            userId = 0;
            return false;
        }

        return true;
    }

    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.IsInRole(nameof(UserRole.Admin));

    public static bool IsBranchStaff(this ClaimsPrincipal user) =>
        user.IsInRole(nameof(UserRole.Manager)) || user.IsInRole(nameof(UserRole.Cashier));

    public static int? TryGetHomeBranchWarehouseId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("home_branch_id");
        if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out var id))
            return null;
        return id;
    }

    /// <summary>Branch staff may only access <paramref name="warehouseId"/> when it equals their home branch warehouse.</summary>
    public static IActionResult? EnsureAdminOrHomeWarehouse(this ControllerBase c, int warehouseId)
    {
        if (c.User.IsAdmin())
            return null;
        if (!c.User.IsBranchStaff())
            return c.Forbid();
        var home = c.User.TryGetHomeBranchWarehouseId();
        if (home != warehouseId)
            return c.Forbid();
        return null;
    }

    /// <summary>Admins may pass any warehouse filter (including null when supported by the underlying report). Branch staff must pass their home branch id.</summary>
    public static IActionResult? EnsureAdminOrOptionalHomeWarehouseFilter(this ControllerBase c, int? warehouseId)
    {
        if (c.User.IsAdmin())
            return null;
        if (!c.User.IsBranchStaff())
            return c.Forbid();
        var home = c.User.TryGetHomeBranchWarehouseId();
        if (home is null || !warehouseId.HasValue || warehouseId.Value != home.Value)
            return c.Forbid();
        return null;
    }
}
