using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Security;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ExpensesController(IExpenseService expenses) : ControllerBase
{
    public sealed record RecordExpenseBody(decimal Amount, string Category, string Description, DateTime ExpenseDateLocal, int? WarehouseId);

    [HttpPost]
    public async Task<ActionResult<int>> Record([FromBody] RecordExpenseBody body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        return Ok(await expenses.RecordExpenseAsync(body.Amount, body.Category, body.Description, body.ExpenseDateLocal, body.WarehouseId, uid, ct));
    }
}
