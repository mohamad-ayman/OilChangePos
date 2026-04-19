using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Security;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StockRequestsController(IBranchStockRequestService stockRequests) : ControllerBase
{
    public sealed class CreateBody
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class RejectBody
    {
        public string? Notes { get; set; }
    }

    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateBody body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        var id = await stockRequests.CreateForHomeBranchAsync(
            uid,
            new CreateBranchStockRequestDto(body.ProductId, body.Quantity, body.Notes),
            ct);
        return Ok(id);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchStockRequestRowDto>>> ListAsync([FromQuery] int? branchWarehouseId, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        if (!User.IsAdmin() && branchWarehouseId.HasValue)
            return Forbid();
        var list = await stockRequests.ListAsync(uid, branchWarehouseId, ct);
        return Ok(list);
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> RejectAsync(int id, [FromBody] RejectBody? body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        await stockRequests.RejectAsync(uid, id, body?.Notes, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/fulfill")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> FulfillAsync(int id, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        await stockRequests.FulfillAsync(uid, id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> CancelAsync(int id, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        await stockRequests.CancelOwnPendingAsync(uid, id, ct);
        return NoContent();
    }
}
