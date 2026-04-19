using System.Net.Http.Json;
using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpMainWarehouseAdminService(HttpClient http) : IMainWarehouseAdminService
{
    public async Task<IReadOnlyList<MainWarehouseGridRowDto>> GetGridRowsAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/main-warehouse/grid-rows", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<MainWarehouseGridRowDto>>(res, cancellationToken);
    }

    public async Task<IReadOnlyList<MainWarehouseCatalogEntryDto>> GetCatalogEntriesAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/main-warehouse/catalog", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<MainWarehouseCatalogEntryDto>>(res, cancellationToken);
    }

    public async Task UpdatePurchaseLineAsync(UpdateMainWarehousePurchaseRequest request, CancellationToken cancellationToken = default)
    {
        using var res = await http.PutAsJsonAsync("api/main-warehouse/purchase", request, OilChangeJson.Options, cancellationToken);
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
    }

    public async Task DeletePurchaseLineAsync(int purchaseId, CancellationToken cancellationToken = default)
    {
        using var res = await http.DeleteAsync($"api/main-warehouse/purchase/{purchaseId}", cancellationToken);
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
    }

    public async Task<int> ImportExcelLinesAsync(int userId, int mainWarehouseId, IReadOnlyList<MainWarehouseExcelImportLineDto> lines, CancellationToken cancellationToken = default)
    {
        _ = userId;
        var body = new { mainWarehouseId, lines };
        using var res = await http.PostAsJsonAsync("api/main-warehouse/import", body, OilChangeJson.Options, cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<int>(res, cancellationToken);
    }
}
