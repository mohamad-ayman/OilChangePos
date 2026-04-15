using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Models;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserInfoResponse>> LoginAsync(
        [FromBody] LoginRequest body,
        CancellationToken cancellationToken)
    {
        var user = await auth.LoginAsync(body.Username, body.Password, cancellationToken);
        if (user is null)
            return Unauthorized(new { error = "Invalid username or password." });

        return Ok(Map(user));
    }

    [HttpGet("branch-users")]
    public async Task<ActionResult<IReadOnlyList<BranchRoleUserDto>>> ListBranchUsersAsync(
        [FromQuery] int adminUserId,
        CancellationToken cancellationToken) =>
        Ok(await auth.ListBranchRoleUsersAsync(adminUserId, cancellationToken));

    public sealed record SetHomeBranchBody(int AdminUserId, int TargetUserId, int? HomeBranchWarehouseId);

    [HttpPost("user-home-branch")]
    public async Task<IActionResult> SetUserHomeBranchAsync([FromBody] SetHomeBranchBody body, CancellationToken cancellationToken)
    {
        await auth.SetUserHomeBranchWarehouseAsync(body.AdminUserId, body.TargetUserId, body.HomeBranchWarehouseId, cancellationToken);
        return NoContent();
    }

    private static UserInfoResponse Map(AppUser u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Role = u.Role.ToString(),
        IsActive = u.IsActive,
        HomeBranchWarehouseId = u.HomeBranchWarehouseId
    };
}
