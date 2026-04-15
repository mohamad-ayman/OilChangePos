using System.Net.Http.Json;
using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpWarehouseService(HttpClient http) : IWarehouseService
{
    public async Task<List<WarehouseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/Warehouses", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<WarehouseDto>>(res, cancellationToken);
    }

    public async Task<List<WarehouseDto>> GetBranchesAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/Warehouses/branches", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<WarehouseDto>>(res, cancellationToken);
    }

    public async Task<WarehouseDto?> GetMainAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/Warehouses/main", cancellationToken);
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
        return await res.Content.ReadFromJsonAsync<WarehouseDto>(OilChangeJson.Options, cancellationToken);
    }

    public async Task<List<WarehouseDto>> ListBranchesForAdminAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/Warehouses/branches-admin", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<WarehouseDto>>(res, cancellationToken);
    }

    public async Task<int> CreateBranchAsync(string name, int adminUserId, CancellationToken cancellationToken = default)
    {
        var body = new { name, adminUserId };
        using var res = await http.PostAsJsonAsync("api/Warehouses/branches", body, OilChangeJson.Options, cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<int>(res, cancellationToken);
    }

    public async Task UpdateBranchAsync(int branchWarehouseId, string name, bool isActive, int adminUserId, CancellationToken cancellationToken = default)
    {
        var body = new { name, isActive, adminUserId };
        using var res = await http.PutAsJsonAsync($"api/Warehouses/branches/{branchWarehouseId}", body, OilChangeJson.Options, cancellationToken);
        await ApiHttp.EnsureSuccessAsync(res, cancellationToken);
    }
}
