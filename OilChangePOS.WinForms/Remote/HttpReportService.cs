using System.Globalization;
using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpReportService(HttpClient http) : IReportService
{
    private static string L(DateTime d) => Uri.EscapeDataString(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    private static string U(DateTime d) => Uri.EscapeDataString(d.ToString("o", CultureInfo.InvariantCulture));

    public async Task<DailySalesDto> GetDailySalesReportAsync(DateTime dateUtc, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Reports/daily-sales?dateUtc={U(dateUtc)}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<DailySalesDto>(res, cancellationToken);
    }

    public async Task<DailySalesDto> GetDailySalesReportForWarehouseAsync(DateTime dateUtc, int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Reports/daily-sales-warehouse?dateUtc={U(dateUtc)}&warehouseId={warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<DailySalesDto>(res, cancellationToken);
    }

    public async Task<List<InventorySnapshotDto>> GetInventorySnapshotAsync(int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Inventory/{warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<InventorySnapshotDto>>(res, cancellationToken);
    }

    public async Task<SalesDashboardDto> GetSalesDashboardAsync(DateTime fromUtc, DateTime toUtc, int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Reports/sales-dashboard?fromUtc={U(fromUtc)}&toUtc={U(toUtc)}&warehouseId={warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<SalesDashboardDto>(res, cancellationToken);
    }

    public async Task<List<SalesByWarehouseSummaryDto>> GetSalesSummariesByWarehouseAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Reports/sales-by-warehouse?fromUtc={U(fromUtc)}&toUtc={U(toUtc)}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<SalesByWarehouseSummaryDto>>(res, cancellationToken);
    }

    public async Task<SalesPeriodSummaryDto> GetSalesPeriodSummaryAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        var wh = warehouseId is { } w ? $"&warehouseId={w}" : string.Empty;
        using var res = await http.GetAsync($"api/Reports/sales-period-summary?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}{wh}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<SalesPeriodSummaryDto>(res, cancellationToken);
    }

    public async Task<List<InvoiceProfitDto>> GetInvoiceProfitBreakdownAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        var wh = warehouseId is { } w ? $"&warehouseId={w}" : string.Empty;
        using var res = await http.GetAsync($"api/Reports/invoice-profit?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}{wh}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<InvoiceProfitDto>>(res, cancellationToken);
    }

    public async Task<List<ProductProfitDto>> GetProductProfitBreakdownAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        var wh = warehouseId is { } w ? $"&warehouseId={w}" : string.Empty;
        using var res = await http.GetAsync($"api/Reports/product-profit?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}{wh}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<ProductProfitDto>>(res, cancellationToken);
    }

    public async Task<ProfitRollupDto> GetProfitRollupAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        var wh = warehouseId is { } w ? $"&warehouseId={w}" : string.Empty;
        using var res = await http.GetAsync($"api/Reports/profit-rollup?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}{wh}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<ProfitRollupDto>(res, cancellationToken);
    }

    public async Task<List<WarehouseStockMovementRowDto>> GetCurrentStockFromMovementsAsync(int? warehouseId, CancellationToken cancellationToken = default)
    {
        var wh = warehouseId is { } w ? $"?warehouseId={w}" : string.Empty;
        using var res = await http.GetAsync($"api/Reports/stock-from-movements{wh}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<WarehouseStockMovementRowDto>>(res, cancellationToken);
    }

    public async Task<List<StockMovementHistoryRowDto>> GetStockMovementHistoryAsync(int productId, DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        var wh = warehouseId is { } w ? $"&warehouseId={w}" : string.Empty;
        using var res = await http.GetAsync($"api/Reports/stock-movement-history?productId={productId}&fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}{wh}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<StockMovementHistoryRowDto>>(res, cancellationToken);
    }

    public async Task<List<TransferLedgerRowDto>> GetTransfersReportAsync(DateTime fromLocalDate, DateTime toLocalDate, int? fromWarehouseId, int? toWarehouseId, CancellationToken cancellationToken = default)
    {
        var f = fromWarehouseId is { } x ? $"&fromWarehouseId={x}" : string.Empty;
        var t = toWarehouseId is { } y ? $"&toWarehouseId={y}" : string.Empty;
        using var res = await http.GetAsync($"api/Reports/transfers?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}{f}{t}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<TransferLedgerRowDto>>(res, cancellationToken);
    }

    public async Task<List<TransferLedgerRowDto>> GetBranchTransferLedgerAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        int warehouseId,
        CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync(
            $"api/Reports/branch-transfers?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}&warehouseId={warehouseId}",
            cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<TransferLedgerRowDto>>(res, cancellationToken);
    }

    public async Task<List<TopSellingProductDto>> GetTopSellingProductsAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, int take, CancellationToken cancellationToken = default)
    {
        var wh = warehouseId is { } w ? $"&warehouseId={w}" : string.Empty;
        using var res = await http.GetAsync($"api/Reports/top-selling?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}{wh}&take={take}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<TopSellingProductDto>>(res, cancellationToken);
    }

    public async Task<List<SlowMovingProductDto>> GetSlowMovingProductsAsync(DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, int take, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Reports/slow-moving?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}&warehouseId={warehouseId}&take={take}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<SlowMovingProductDto>>(res, cancellationToken);
    }

    public async Task<List<DailyCashFlowRowDto>> GetDailyCashFlowAsync(DateTime fromLocalDate, DateTime toLocalDate, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Reports/daily-cash-flow?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<DailyCashFlowRowDto>>(res, cancellationToken);
    }

    public async Task<List<ExpenseReportRowDto>> GetExpensesInPeriodAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default)
    {
        var wh = warehouseId is { } w ? $"&warehouseId={w}" : string.Empty;
        using var res = await http.GetAsync($"api/Reports/expenses?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}{wh}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<ExpenseReportRowDto>>(res, cancellationToken);
    }

    public async Task<List<BranchSalesLineRegisterDto>> GetBranchSalesLineRegisterAsync(DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Reports/branch-sales-lines?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}&warehouseId={warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<BranchSalesLineRegisterDto>>(res, cancellationToken);
    }

    public async Task<List<BranchIncomingRegisterDto>> GetBranchIncomingRegisterAsync(DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Reports/branch-incoming?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}&warehouseId={warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<BranchIncomingRegisterDto>>(res, cancellationToken);
    }

    public async Task<List<BranchSellerSalesSummaryDto>> GetBranchSalesBySellerAsync(DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Reports/branch-sellers?fromLocalDate={L(fromLocalDate)}&toLocalDate={L(toLocalDate)}&warehouseId={warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<BranchSellerSalesSummaryDto>>(res, cancellationToken);
    }
}
