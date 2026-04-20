using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Security;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class TransfersController(ITransferService transfers) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<int>> Transfer([FromBody] TransferStockRequest body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        if (body.UserId != uid)
            return Forbid();
        return Ok(await transfers.TransferStockAsync(body, ct));
    }

    /// <summary>Many SKUs in one transaction (same rules as single transfer).</summary>
    [HttpPost("bulk")]
    public async Task<ActionResult<IReadOnlyList<int>>> TransferBulk([FromBody] TransferStockBulkRequest body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        if (body.UserId != uid)
            return Forbid();
        return Ok(await transfers.TransferStockBulkAsync(body, ct));
    }
}
