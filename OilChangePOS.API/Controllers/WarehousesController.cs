using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Security;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class WarehousesController(IWarehouseService warehouses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WarehouseDto>>> GetAll(CancellationToken ct)
    {
        var all = await warehouses.GetAllAsync(ct);
        if (User.IsAdmin())
            return Ok(all);
        if (!User.IsBranchStaff())
            return Ok(all);
        var home = User.TryGetHomeBranchWarehouseId();
        if (home is null)
            return Ok(new List<WarehouseDto>());
        return Ok(all.Where(w => w.Id == home.Value).ToList());
    }

    [HttpGet("branches")]
    public async Task<ActionResult<List<WarehouseDto>>> GetBranches(CancellationToken ct)
    {
        var branches = await warehouses.GetBranchesAsync(ct);
        if (User.IsAdmin())
            return Ok(branches);
        if (!User.IsBranchStaff())
            return Ok(branches);
        var home = User.TryGetHomeBranchWarehouseId();
        if (home is null)
            return Ok(new List<WarehouseDto>());
        return Ok(branches.Where(b => b.Id == home.Value).ToList());
    }

    [HttpGet("main")]
    public async Task<ActionResult<WarehouseDto?>> GetMain(CancellationToken ct)
    {
        if (!User.IsAdmin() && User.IsBranchStaff())
            return Ok((WarehouseDto?)null);
        return Ok(await warehouses.GetMainAsync(ct));
    }

    [HttpGet("branches-admin")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<List<WarehouseDto>>> ListBranchesAdmin(CancellationToken ct) =>
        Ok(await warehouses.ListBranchesForAdminAsync(ct));

    public sealed record CreateBranchBody(string Name);

    [HttpPost("branches")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<int>> CreateBranch([FromBody] CreateBranchBody body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var actorId))
            return Unauthorized();
        return Ok(await warehouses.CreateBranchAsync(body.Name, actorId, ct));
    }

    public sealed record UpdateBranchBody(string Name, bool IsActive);

    [HttpPut("branches/{branchWarehouseId:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> UpdateBranch(int branchWarehouseId, [FromBody] UpdateBranchBody body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var actorId))
            return Unauthorized();
        await warehouses.UpdateBranchAsync(branchWarehouseId, body.Name, body.IsActive, actorId, ct);
        return NoContent();
    }
}
