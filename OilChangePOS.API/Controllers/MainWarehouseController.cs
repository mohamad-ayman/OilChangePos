using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Security;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/main-warehouse")]
[Authorize(Roles = nameof(UserRole.Admin))]
public sealed class MainWarehouseController(IMainWarehouseAdminService mainWarehouse) : ControllerBase
{
    [HttpGet("grid-rows")]
    public async Task<ActionResult<IReadOnlyList<MainWarehouseGridRowDto>>> GridRows(CancellationToken ct) =>
        Ok(await mainWarehouse.GetGridRowsAsync(ct));

    [HttpGet("catalog")]
    public async Task<ActionResult<IReadOnlyList<MainWarehouseCatalogEntryDto>>> Catalog(CancellationToken ct) =>
        Ok(await mainWarehouse.GetCatalogEntriesAsync(ct));

    [HttpPut("purchase")]
    public async Task<IActionResult> UpdatePurchase([FromBody] UpdateMainWarehousePurchaseRequest body, CancellationToken ct)
    {
        await mainWarehouse.UpdatePurchaseLineAsync(body, ct);
        return NoContent();
    }

    [HttpDelete("purchase/{purchaseId:int}")]
    public async Task<IActionResult> DeletePurchase(int purchaseId, CancellationToken ct)
    {
        await mainWarehouse.DeletePurchaseLineAsync(purchaseId, ct);
        return NoContent();
    }

    public sealed record ImportBody(int MainWarehouseId, List<MainWarehouseExcelImportLineDto> Lines);

    [HttpPost("import")]
    public async Task<ActionResult<int>> Import([FromBody] ImportBody body, CancellationToken ct)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        return Ok(await mainWarehouse.ImportExcelLinesAsync(uid, body.MainWarehouseId, body.Lines, ct));
    }
}
