using System.Globalization;
using System.Net.Http.Json;
using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpInventoryService(HttpClient http) : IInventoryService
{
    public async Task<decimal> GetCurrentStockAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Inventory/current-stock/{productId}/{warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<decimal>(res, cancellationToken);
    }

    public async Task<List<LowStockItemDto>> GetLowStockAsync(int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Inventory/low-stock/{warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<LowStockItemDto>>(res, cancellationToken);
    }

    public async Task<int> AddStockAsync(PurchaseStockRequest request, CancellationToken cancellationToken = default)
    {
        using var res = await http.PostAsJsonAsync("api/Inventory/purchase", request, OilChangeJson.Options, cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<int>(res, cancellationToken);
    }

    public async Task<PurchaseReceiptBatchResult> AddPurchaseReceiptBatchAsync(
        int userId,
        int warehouseId,
        string supplierName,
        string? receiptMemo,
        IReadOnlyList<PurchaseReceiptLineInput> lines,
        CancellationToken cancellationToken = default)
    {
        var body = new { userId, warehouseId, supplierName, receiptMemo, lines };
        using var res = await http.PostAsJsonAsync("api/Inventory/purchase-receipt-batch", body, OilChangeJson.Options, cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<PurchaseReceiptBatchResult>(res, cancellationToken);
    }

    public async Task<StockAuditResultDto> RunStockAuditAsync(int userId, int warehouseId, List<AuditLineRequest> lines, string notes, CancellationToken cancellationToken = default)
    {
        var body = new { userId, warehouseId, lines, notes };
        using var res = await http.PostAsJsonAsync("api/Inventory/audit", body, OilChangeJson.Options, cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<StockAuditResultDto>(res, cancellationToken);
    }

    public async Task<List<StockAuditHistoryRowDto>> GetStockAuditHistoryAsync(int? warehouseId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken cancellationToken = default)
    {
        var wh = warehouseId is { } w ? $"&warehouseId={w}" : string.Empty;
        using var res = await http.GetAsync(
            $"api/Inventory/audit-history?fromUtc={Uri.EscapeDataString(fromUtc.ToString("o", CultureInfo.InvariantCulture))}&toUtcExclusive={Uri.EscapeDataString(toUtcExclusive.ToString("o", CultureInfo.InvariantCulture))}{wh}",
            cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<StockAuditHistoryRowDto>>(res, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetBranchSalePriceOverridesAsync(int warehouseId, IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
    {
        var body = new BranchPriceOverrideQueryDto { WarehouseId = warehouseId, ProductIds = productIds.ToList() };
        using var res = await http.PostAsJsonAsync("api/Inventory/branch-overrides", body, OilChangeJson.Options, cancellationToken);
        var list = await ApiHttp.ReadFromJsonAsync<List<BranchPriceOverrideItemDto>>(res, cancellationToken);
        return list.ToDictionary(x => x.ProductId, x => x.SalePrice);
    }

    public async Task<decimal> GetEffectiveSalePriceAsync(int productId, int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Inventory/effective-sale-price/{productId}/{warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<decimal>(res, cancellationToken);
    }

    public async Task<List<BranchPriceRowDto>> GetBranchPricesAsync(int warehouseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Inventory/branch-prices/{warehouseId}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<BranchPriceRowDto>>(res, cancellationToken);
    }

    public async Task SetBranchSalePriceAsync(int userId, int warehouseId, int productId, decimal salePrice, CancellationToken cancellationToken = default)
    {
        var body = new SetBranchPriceRequest(productId, warehouseId, salePrice, userId);
        using var res = await http.PostAsJsonAsync("api/Inventory/branch-price", body, OilChangeJson.Options, cancellationToken);
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
    }

    public async Task DeleteBranchSalePriceAsync(int userId, int warehouseId, int productId, CancellationToken cancellationToken = default)
    {
        using var res = await http.DeleteAsync($"api/Inventory/branch-price/{warehouseId}/{productId}?userId={userId}", cancellationToken);
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
    }
}
