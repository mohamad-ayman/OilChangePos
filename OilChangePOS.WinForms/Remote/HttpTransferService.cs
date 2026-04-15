using System.Net.Http.Json;
using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpTransferService(HttpClient http) : ITransferService
{
    public async Task<int> TransferStockAsync(TransferStockRequest request, CancellationToken cancellationToken = default)
    {
        using var res = await http.PostAsJsonAsync("api/Transfers", request, OilChangeJson.Options, cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<int>(res, cancellationToken);
    }
}
