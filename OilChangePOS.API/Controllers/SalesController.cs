using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Security;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SalesController(ISalesService sales) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(SaleCreatedResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SaleCreatedResponse>> CompleteSaleAsync(
        [FromBody] CompleteSaleRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        if (request.UserId != uid)
            return Forbid();
        var invoiceId = await sales.CompleteSaleAsync(request, cancellationToken);
        return Ok(new SaleCreatedResponse(invoiceId));
    }
}

public sealed record SaleCreatedResponse(int InvoiceId);
