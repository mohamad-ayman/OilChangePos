using Microsoft.AspNetCore.Mvc;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ExpensesController(IExpenseService expenses) : ControllerBase
{
    public sealed record RecordExpenseBody(decimal Amount, string Category, string Description, DateTime ExpenseDateLocal, int? WarehouseId, int UserId);

    [HttpPost]
    public async Task<ActionResult<int>> Record([FromBody] RecordExpenseBody body, CancellationToken ct) =>
        Ok(await expenses.RecordExpenseAsync(body.Amount, body.Category, body.Description, body.ExpenseDateLocal, body.WarehouseId, body.UserId, ct));
}
