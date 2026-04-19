using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Security;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class AdminUsersController(IUserManagementService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserRowDto>>> ListAsync(CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var actorId))
            return Unauthorized();
        return Ok(await users.ListUsersAsync(actorId, ct));
    }

    public sealed record CreateUserBody(string Username, string Password, string Role, int? HomeBranchWarehouseId);

    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateUserBody body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var actorId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(body.Role, ignoreCase: true, out var role))
            return BadRequest(new { error = "Invalid role." });
        var id = await users.CreateUserAsync(actorId, body.Username, body.Password, role, body.HomeBranchWarehouseId, ct);
        return Ok(id);
    }

    public sealed record UpdateUserBody(string Role, bool IsActive, int? HomeBranchWarehouseId);

    [HttpPut("{userId:int}")]
    public async Task<IActionResult> UpdateAsync(int userId, [FromBody] UpdateUserBody body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var actorId))
            return Unauthorized();
        if (!Enum.TryParse<UserRole>(body.Role, ignoreCase: true, out var role))
            return BadRequest(new { error = "Invalid role." });
        await users.UpdateUserAsync(actorId, userId, role, body.IsActive, body.HomeBranchWarehouseId, ct);
        return NoContent();
    }

    public sealed record SetPasswordBody(string NewPassword);

    [HttpPost("{userId:int}/password")]
    public async Task<IActionResult> SetPasswordAsync(int userId, [FromBody] SetPasswordBody body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var actorId))
            return Unauthorized();
        await users.SetPasswordAsync(actorId, userId, body.NewPassword, ct);
        return NoContent();
    }
}
