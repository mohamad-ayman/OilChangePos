using Microsoft.AspNetCore.Mvc;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/main-warehouse")]
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

    public sealed record ImportBody(int UserId, int MainWarehouseId, List<MainWarehouseExcelImportLineDto> Lines);

    [HttpPost("import")]
    public async Task<ActionResult<int>> Import([FromBody] ImportBody body, CancellationToken ct) =>
        Ok(await mainWarehouse.ImportExcelLinesAsync(body.UserId, body.MainWarehouseId, body.Lines, ct));
}
