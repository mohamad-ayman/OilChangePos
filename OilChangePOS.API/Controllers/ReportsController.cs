using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OilChangePOS.API.Security;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("daily-sales")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> DailySales([FromQuery] DateTime dateUtc, CancellationToken ct) =>
        Ok(await reports.GetDailySalesReportAsync(dateUtc, ct));

    [HttpGet("daily-sales-warehouse")]
    public async Task<IActionResult> DailySalesWarehouse(
        [FromQuery] DateTime dateUtc,
        [FromQuery] int warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetDailySalesReportForWarehouseAsync(dateUtc, warehouseId, ct));
    }

    [HttpGet("sales-dashboard")]
    public async Task<IActionResult> SalesDashboard(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] int warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetSalesDashboardAsync(fromUtc, toUtc, warehouseId, ct));
    }

    [HttpGet("sales-by-warehouse")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> SalesByWarehouse(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken ct) =>
        Ok(await reports.GetSalesSummariesByWarehouseAsync(fromUtc, toUtc, ct));

    [HttpGet("sales-period-summary")]
    public async Task<IActionResult> SalesPeriodSummary(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int? warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrOptionalHomeWarehouseFilter(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetSalesPeriodSummaryAsync(fromLocalDate, toLocalDate, warehouseId, ct));
    }

    [HttpGet("invoice-profit")]
    public async Task<IActionResult> InvoiceProfit(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int? warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrOptionalHomeWarehouseFilter(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetInvoiceProfitBreakdownAsync(fromLocalDate, toLocalDate, warehouseId, ct));
    }

    [HttpGet("product-profit")]
    public async Task<IActionResult> ProductProfit(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int? warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrOptionalHomeWarehouseFilter(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetProductProfitBreakdownAsync(fromLocalDate, toLocalDate, warehouseId, ct));
    }

    [HttpGet("profit-rollup")]
    public async Task<IActionResult> ProfitRollup(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int? warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrOptionalHomeWarehouseFilter(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetProfitRollupAsync(fromLocalDate, toLocalDate, warehouseId, ct));
    }

    [HttpGet("stock-from-movements")]
    public async Task<IActionResult> StockFromMovements(
        [FromQuery] int? warehouseId,
        CancellationToken ct)
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

        return Ok(await reports.GetCurrentStockFromMovementsAsync(warehouseId, ct));
    }

    [HttpGet("stock-movement-history")]
    public async Task<IActionResult> StockMovementHistory(
        [FromQuery] int productId,
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int? warehouseId,
        CancellationToken ct)
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

        return Ok(await reports.GetStockMovementHistoryAsync(productId, fromLocalDate, toLocalDate, warehouseId, ct));
    }

    [HttpGet("transfers")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Transfers(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int? fromWarehouseId,
        [FromQuery] int? toWarehouseId,
        CancellationToken ct) =>
        Ok(await reports.GetTransfersReportAsync(fromLocalDate, toLocalDate, fromWarehouseId, toWarehouseId, ct));

    /// <summary>Stock transfers involving this warehouse (in or out). Branch staff: home warehouse only.</summary>
    [HttpGet("branch-transfers")]
    public async Task<IActionResult> BranchTransfers(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetBranchTransferLedgerAsync(fromLocalDate, toLocalDate, warehouseId, ct));
    }

    [HttpGet("top-selling")]
    public async Task<IActionResult> TopSelling(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int? warehouseId,
        [FromQuery] int take,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrOptionalHomeWarehouseFilter(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetTopSellingProductsAsync(fromLocalDate, toLocalDate, warehouseId, take, ct));
    }

    [HttpGet("slow-moving")]
    public async Task<IActionResult> SlowMoving(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int warehouseId,
        [FromQuery] int take,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetSlowMovingProductsAsync(fromLocalDate, toLocalDate, warehouseId, take, ct));
    }

    [HttpGet("daily-cash-flow")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> DailyCashFlow(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        CancellationToken ct) =>
        Ok(await reports.GetDailyCashFlowAsync(fromLocalDate, toLocalDate, ct));

    [HttpGet("expenses")]
    public async Task<IActionResult> Expenses(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int? warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrOptionalHomeWarehouseFilter(warehouseId);
        if (deny is not null) return deny;
        var branchOperatorView = !User.IsAdmin();
        return Ok(await reports.GetExpensesInPeriodAsync(fromLocalDate, toLocalDate, warehouseId, branchOperatorView, ct));
    }

    [HttpGet("branch-sales-lines")]
    public async Task<IActionResult> BranchSalesLines(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetBranchSalesLineRegisterAsync(fromLocalDate, toLocalDate, warehouseId, ct));
    }

    [HttpGet("branch-incoming")]
    public async Task<IActionResult> BranchIncoming(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetBranchIncomingRegisterAsync(fromLocalDate, toLocalDate, warehouseId, ct));
    }

    [HttpGet("branch-sellers")]
    public async Task<IActionResult> BranchSellers(
        [FromQuery] DateTime fromLocalDate,
        [FromQuery] DateTime toLocalDate,
        [FromQuery] int warehouseId,
        CancellationToken ct)
    {
        var deny = this.EnsureAdminOrHomeWarehouse(warehouseId);
        if (deny is not null) return deny;
        return Ok(await reports.GetBranchSalesBySellerAsync(fromLocalDate, toLocalDate, warehouseId, ct));
    }
}
