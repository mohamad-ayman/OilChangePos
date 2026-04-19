using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Security;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class InventoryController(IReportService reports, IInventoryService inventory) : ControllerBase
{
    [HttpGet("{warehouseId:int}")]
    [ProducesResponseType(typeof(List<InventorySnapshotDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSnapshotAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetInventorySnapshotAsync(warehouseId, cancellationToken));
    }

    [HttpGet("current-stock/{productId:int}/{warehouseId:int}")]
    public async Task<IActionResult> GetCurrentStockAsync(int productId, int warehouseId, CancellationToken cancellationToken)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await inventory.GetCurrentStockAsync(productId, warehouseId, cancellationToken));
    }

    [HttpGet("low-stock/{warehouseId:int}")]
    public async Task<IActionResult> GetLowStockAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await inventory.GetLowStockAsync(warehouseId, cancellationToken));
    }

    [HttpPost("purchase")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<int>> AddPurchaseAsync([FromBody] PurchaseStockRequest request, CancellationToken cancellationToken)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        if (request.UserId != uid)
            return Forbid();
        return Ok(await inventory.AddStockAsync(request, cancellationToken));
    }

    public sealed class PurchaseReceiptBatchBody
    {
        public int UserId { get; set; }
        public int WarehouseId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? ReceiptMemo { get; set; }
        public List<PurchaseReceiptLineInput> Lines { get; set; } = [];
    }

    [HttpPost("purchase-receipt-batch")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<PurchaseReceiptBatchResult>> AddPurchaseReceiptBatchAsync(
        [FromBody] PurchaseReceiptBatchBody body,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        if (body.UserId != uid)
            return Forbid();
        return Ok(await inventory.AddPurchaseReceiptBatchAsync(
            body.UserId,
            body.WarehouseId,
            body.SupplierName,
            body.ReceiptMemo,
            body.Lines,
            cancellationToken));
    }

    public sealed class RunAuditBody
    {
        public int UserId { get; set; }
        public int WarehouseId { get; set; }
        public List<AuditLineRequest> Lines { get; set; } = [];
        public string Notes { get; set; } = string.Empty;
    }

    [HttpPost("audit")]
    public async Task<ActionResult<StockAuditResultDto>> RunAuditAsync([FromBody] RunAuditBody body, CancellationToken cancellationToken)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        if (body.UserId != uid)
            return Forbid();
        return Ok(await inventory.RunStockAuditAsync(body.UserId, body.WarehouseId, body.Lines, body.Notes, cancellationToken));
    }

    [HttpGet("audit-history")]
    public async Task<IActionResult> GetAuditHistoryAsync(
        [FromQuery] int? warehouseId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtcExclusive,
        CancellationToken cancellationToken)
    {
        if (!warehouseId.HasValue)
        {
            if (!User.IsAdmin())
                return Forbid();
        }
        else
        {
            var deny = this.EnsureAdminOrHomeWarehouse(warehouseId.Value);
            if (deny is not null) return deny;
        }

        return Ok(await inventory.GetStockAuditHistoryAsync(warehouseId, fromUtc, toUtcExclusive, cancellationToken));
    }

    [HttpPost("branch-overrides")]
    public async Task<IActionResult> GetBranchOverridesAsync(
        [FromBody] BranchPriceOverrideQueryDto body,
        CancellationToken cancellationToken)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(body.WarehouseId);
        if (deny is not null) return deny;
        var dict = await inventory.GetBranchSalePriceOverridesAsync(body.WarehouseId, body.ProductIds, cancellationToken);
        var list = dict.Select(kv => new BranchPriceOverrideItemDto { ProductId = kv.Key, SalePrice = kv.Value }).ToList();
        return Ok(list);
    }

    [HttpGet("effective-sale-price/{productId:int}/{warehouseId:int}")]
    public async Task<IActionResult> GetEffectiveSalePriceAsync(int productId, int warehouseId, CancellationToken cancellationToken)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await inventory.GetEffectiveSalePriceAsync(productId, warehouseId, cancellationToken));
    }

    [HttpGet("branch-prices/{warehouseId:int}")]
    public async Task<IActionResult> GetBranchPricesAsync(int warehouseId, CancellationToken cancellationToken)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await inventory.GetBranchPricesAsync(warehouseId, cancellationToken));
    }

    [HttpPost("branch-price")]
    public async Task<IActionResult> SetBranchSalePriceAsync([FromBody] SetBranchPriceRequest body, CancellationToken cancellationToken)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        if (body.UserId != uid)
            return Forbid();
        await inventory.SetBranchSalePriceAsync(body.UserId, body.WarehouseId, body.ProductId, body.SalePrice, cancellationToken);
        return NoContent();
    }

    [HttpDelete("branch-price/{warehouseId:int}/{productId:int}")]
    public async Task<IActionResult> DeleteBranchSalePriceAsync(
        int warehouseId,
        int productId,
        [FromQuery] int userId,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetRequiredUserId(out var uid))
            return Unauthorized();
        if (userId != uid)
            return Forbid();
        await inventory.DeleteBranchSalePriceAsync(userId, warehouseId, productId, cancellationToken);
        return NoContent();
    }
}
