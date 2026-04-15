using Microsoft.AspNetCore.Mvc;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<List<WarehouseDto>>> ListBranchesAdmin(CancellationToken ct) =>
        Ok(await warehouses.ListBranchesForAdminAsync(ct));

    public sealed record CreateBranchBody(string Name, int AdminUserId);

    [HttpPost("branches")]
    public async Task<ActionResult<int>> CreateBranch([FromBody] CreateBranchBody body, CancellationToken ct) =>
        Ok(await warehouses.CreateBranchAsync(body.Name, body.AdminUserId, ct));

    public sealed record UpdateBranchBody(string Name, bool IsActive, int AdminUserId);

    [HttpPut("branches/{branchWarehouseId:int}")]
    public async Task<IActionResult> UpdateBranch(int branchWarehouseId, [FromBody] UpdateBranchBody body, CancellationToken ct)
    {
        await warehouses.UpdateBranchAsync(branchWarehouseId, body.Name, body.IsActive, body.AdminUserId, ct);
        return NoContent();
    }
}
