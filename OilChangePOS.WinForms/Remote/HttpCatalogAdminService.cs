using System.Net.Http.Json;
using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpCatalogAdminService(HttpClient http) : ICatalogAdminService
{
    public async Task<IReadOnlyList<CatalogCompanyListRowDto>> ListCompaniesForCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/catalog-admin/companies", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<CatalogCompanyListRowDto>>(res, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogProductListRowDto>> ListProductsForCompanyAsync(int companyId, CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync($"api/catalog-admin/companies/{companyId}/products", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<CatalogProductListRowDto>>(res, cancellationToken);
    }

    public async Task SaveCatalogCompanyAsync(bool createNew, int? existingCompanyId, string name, bool isActive, CancellationToken cancellationToken = default)
    {
        var body = new { createNew, existingCompanyId, name, isActive };
        using var res = await http.PostAsJsonAsync("api/catalog-admin/companies", body, OilChangeJson.Options, cancellationToken);
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
    }

    public async Task SaveCatalogProductAsync(bool createNew, int companyId, int? existingProductId, string name, string category, string package, bool isActive, CancellationToken cancellationToken = default)
    {
        var body = new { createNew, companyId, existingProductId, name, category, package, isActive };
        using var res = await http.PostAsJsonAsync("api/catalog-admin/products", body, OilChangeJson.Options, cancellationToken);
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyComboItemDto>> ListActiveCompaniesForComboAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/catalog-admin/company-combo", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<CompanyComboItemDto>>(res, cancellationToken);
    }

    public async Task<int> CreatePosTabProductAsync(int companyId, string name, string category, string package, decimal unitPrice, CancellationToken cancellationToken = default)
    {
        var body = new { companyId, name, category, package, unitPrice };
        using var res = await http.PostAsJsonAsync("api/catalog-admin/pos-products", body, OilChangeJson.Options, cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<int>(res, cancellationToken);
    }

    public async Task<bool> PosTabProductExistsAsync(int companyId, string name, string category, string package, CancellationToken cancellationToken = default)
    {
        var q =
            $"api/catalog-admin/pos-product-exists?companyId={companyId}&name={Uri.EscapeDataString(name)}&category={Uri.EscapeDataString(category)}&package={Uri.EscapeDataString(package)}";
        using var res = await http.GetAsync(q, cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<bool>(res, cancellationToken);
    }
}
