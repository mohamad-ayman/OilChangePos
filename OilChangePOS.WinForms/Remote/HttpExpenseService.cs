using System.Net.Http.Json;
using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpExpenseService(HttpClient http) : IExpenseService
{
    public async Task<int> RecordExpenseAsync(decimal amount, string category, string description, DateTime expenseDateLocal, int? warehouseId, int userId, CancellationToken cancellationToken = default)
    {
        _ = userId;
        var body = new { amount, category, description, expenseDateLocal, warehouseId };
        using var res = await http.PostAsJsonAsync("api/Expenses", body, OilChangeJson.Options, cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<int>(res, cancellationToken);
    }
}
