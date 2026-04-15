using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpProductCatalogService(HttpClient http) : IProductCatalogService
{
    public async Task<IReadOnlyList<ProductListDto>> GetActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/Products", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<ProductListDto>>(res, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetProductSummariesAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/Products/summaries?activeOnly={activeOnly.ToString().ToLowerInvariant()}", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<ProductSummaryDto>>(res, cancellationToken);
    }
}
