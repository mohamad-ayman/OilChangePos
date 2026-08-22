using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Security;

/// <summary>
/// Rejects tokens for missing/inactive users and tokens whose role or home-branch claims no longer match the database.
/// </summary>
public static class JwtLiveUserGuard
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("Invalid token.");
            return;
        }

        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub) || !int.TryParse(sub, out var userId))
        {
            context.Fail("Invalid token subject.");
            return;
        }

        var factory = context.HttpContext.RequestServices.GetRequiredService<IDbContextFactory<OilChangePosDbContext>>();
        await using var db = await factory.CreateDbContextAsync(context.HttpContext.RequestAborted);
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, context.HttpContext.RequestAborted);
        if (user is null || !user.IsActive)
        {
            context.Fail("User is inactive.");
            return;
        }

        var roleClaim = principal.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(roleClaim, user.Role.ToString(), StringComparison.Ordinal))
        {
            context.Fail("Stale role claim.");
            return;
        }

        var homeRaw = principal.FindFirstValue("home_branch_id");
        int? claimHome = string.IsNullOrEmpty(homeRaw) || !int.TryParse(homeRaw, out var hid) ? null : hid;
        if (claimHome != user.HomeBranchWarehouseId)
        {
            context.Fail("Stale home branch claim.");
        }
    }
}
