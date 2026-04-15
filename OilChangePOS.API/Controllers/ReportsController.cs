using Microsoft.AspNetCore.Mvc;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("daily-sales")]
    public Task<ActionResult<DailySalesDto>> DailySales([FromQuery] DateTime dateUtc, CancellationToken ct) =>
        Map(() => reports.GetDailySalesReportAsync(dateUtc, ct));

    [HttpGet("daily-sales-warehouse")]
    public Task<ActionResult<DailySalesDto>> DailySalesWarehouse([FromQuery] DateTime dateUtc, [FromQuery] int warehouseId, CancellationToken ct) =>
        Map(() => reports.GetDailySalesReportForWarehouseAsync(dateUtc, warehouseId, ct));

    [HttpGet("sales-dashboard")]
    public Task<ActionResult<SalesDashboardDto>> SalesDashboard([FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, [FromQuery] int warehouseId, CancellationToken ct) =>
        Map(() => reports.GetSalesDashboardAsync(fromUtc, toUtc, warehouseId, ct));

    [HttpGet("sales-by-warehouse")]
    public Task<ActionResult<List<SalesByWarehouseSummaryDto>>> SalesByWarehouse([FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, CancellationToken ct) =>
        Map(() => reports.GetSalesSummariesByWarehouseAsync(fromUtc, toUtc, ct));

    [HttpGet("sales-period-summary")]
    public Task<ActionResult<SalesPeriodSummaryDto>> SalesPeriodSummary(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int? warehouseId, CancellationToken ct) =>
        Map(() => reports.GetSalesPeriodSummaryAsync(fromLocalDate, toLocalDate, warehouseId, ct));

    [HttpGet("invoice-profit")]
    public Task<ActionResult<List<InvoiceProfitDto>>> InvoiceProfit(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int? warehouseId, CancellationToken ct) =>
        Map(() => reports.GetInvoiceProfitBreakdownAsync(fromLocalDate, toLocalDate, warehouseId, ct));

    [HttpGet("product-profit")]
    public Task<ActionResult<List<ProductProfitDto>>> ProductProfit(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int? warehouseId, CancellationToken ct) =>
        Map(() => reports.GetProductProfitBreakdownAsync(fromLocalDate, toLocalDate, warehouseId, ct));

    [HttpGet("profit-rollup")]
    public Task<ActionResult<ProfitRollupDto>> ProfitRollup(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int? warehouseId, CancellationToken ct) =>
        Map(() => reports.GetProfitRollupAsync(fromLocalDate, toLocalDate, warehouseId, ct));

    [HttpGet("stock-from-movements")]
    public Task<ActionResult<List<WarehouseStockMovementRowDto>>> StockFromMovements([FromQuery] int? warehouseId, CancellationToken ct) =>
        Map(() => reports.GetCurrentStockFromMovementsAsync(warehouseId, ct));

    [HttpGet("stock-movement-history")]
    public Task<ActionResult<List<StockMovementHistoryRowDto>>> StockMovementHistory(
        [FromQuery] int productId, [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int? warehouseId, CancellationToken ct) =>
        Map(() => reports.GetStockMovementHistoryAsync(productId, fromLocalDate, toLocalDate, warehouseId, ct));

    [HttpGet("transfers")]
    public Task<ActionResult<List<TransferLedgerRowDto>>> Transfers(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int? fromWarehouseId, [FromQuery] int? toWarehouseId, CancellationToken ct) =>
        Map(() => reports.GetTransfersReportAsync(fromLocalDate, toLocalDate, fromWarehouseId, toWarehouseId, ct));

    [HttpGet("top-selling")]
    public Task<ActionResult<List<TopSellingProductDto>>> TopSelling(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int? warehouseId, [FromQuery] int take, CancellationToken ct) =>
        Map(() => reports.GetTopSellingProductsAsync(fromLocalDate, toLocalDate, warehouseId, take, ct));

    [HttpGet("slow-moving")]
    public Task<ActionResult<List<SlowMovingProductDto>>> SlowMoving(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int warehouseId, [FromQuery] int take, CancellationToken ct) =>
        Map(() => reports.GetSlowMovingProductsAsync(fromLocalDate, toLocalDate, warehouseId, take, ct));

    [HttpGet("daily-cash-flow")]
    public Task<ActionResult<List<DailyCashFlowRowDto>>> DailyCashFlow(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, CancellationToken ct) =>
        Map(() => reports.GetDailyCashFlowAsync(fromLocalDate, toLocalDate, ct));

    [HttpGet("expenses")]
    public Task<ActionResult<List<ExpenseReportRowDto>>> Expenses(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int? warehouseId, CancellationToken ct) =>
        Map(() => reports.GetExpensesInPeriodAsync(fromLocalDate, toLocalDate, warehouseId, ct));

    [HttpGet("branch-sales-lines")]
    public Task<ActionResult<List<BranchSalesLineRegisterDto>>> BranchSalesLines(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int warehouseId, CancellationToken ct) =>
        Map(() => reports.GetBranchSalesLineRegisterAsync(fromLocalDate, toLocalDate, warehouseId, ct));

    [HttpGet("branch-incoming")]
    public Task<ActionResult<List<BranchIncomingRegisterDto>>> BranchIncoming(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int warehouseId, CancellationToken ct) =>
        Map(() => reports.GetBranchIncomingRegisterAsync(fromLocalDate, toLocalDate, warehouseId, ct));

    [HttpGet("branch-sellers")]
    public Task<ActionResult<List<BranchSellerSalesSummaryDto>>> BranchSellers(
        [FromQuery] DateTime fromLocalDate, [FromQuery] DateTime toLocalDate, [FromQuery] int warehouseId, CancellationToken ct) =>
        Map(() => reports.GetBranchSalesBySellerAsync(fromLocalDate, toLocalDate, warehouseId, ct));

    private async Task<ActionResult<T>> Map<T>(Func<Task<T>> fn) =>
        Ok(await fn());
}
