using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Models;
using OilChangePOS.API.Security;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AuthController(IAuthService auth, JwtAccessTokenFactory tokens) : ControllerBase
{
    [AllowAnonymous]
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

        var dto = Map(user);
        dto.AccessToken = tokens.CreateAccessToken(user);
        return Ok(dto);
    }

    [HttpGet("branch-users")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<IReadOnlyList<BranchRoleUserDto>>> ListBranchUsersAsync(
        CancellationToken cancellationToken)
    {
        if (!this.TryGetRequiredUserId(out var actorId))
            return Unauthorized();
        return Ok(await auth.ListBranchRoleUsersAsync(actorId, cancellationToken));
    }

    public sealed record SetHomeBranchBody(int TargetUserId, int? HomeBranchWarehouseId);

    [HttpPost("user-home-branch")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> SetUserHomeBranchAsync([FromBody] SetHomeBranchBody body, CancellationToken cancellationToken)
    {
        if (!this.TryGetRequiredUserId(out var actorId))
            return Unauthorized();
        await auth.SetUserHomeBranchWarehouseAsync(actorId, body.TargetUserId, body.HomeBranchWarehouseId, cancellationToken);
        return NoContent();
    }

    private static UserInfoResponse Map(AppUser u) => new()
    {
        Id = u.Id,
        Username = u.Username,
        Role = u.Role.ToString(),
        IsActive = u.IsActive,
        HomeBranchWarehouseId = u.HomeBranchWarehouseId,
        AccessToken = string.Empty
    };
}
