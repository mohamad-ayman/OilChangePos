using Microsoft.AspNetCore.Mvc;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TransfersController(ITransferService transfers) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<int>> Transfer([FromBody] TransferStockRequest body, CancellationToken ct) =>
        Ok(await transfers.TransferStockAsync(body, ct));
}
