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
    public async Task<ActionResult<List<WarehouseDto>>> GetAll(CancellationToken ct) =>
        Ok(await warehouses.GetAllAsync(ct));

    [HttpGet("branches")]
    public async Task<ActionResult<List<WarehouseDto>>> GetBranches(CancellationToken ct) =>
        Ok(await warehouses.GetBranchesAsync(ct));

    [HttpGet("main")]
    public async Task<ActionResult<WarehouseDto?>> GetMain(CancellationToken ct) =>
        Ok(await warehouses.GetMainAsync(ct));

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
