using System.Net.Http.Json;
using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpSalesService(HttpClient http) : ISalesService
{
    private sealed record SaleCreatedResponse(int InvoiceId);

    public async Task<int> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default)
    {
        using var res = await http.PostAsJsonAsync("api/Sales", request, OilChangeJson.Options, cancellationToken);
        var dto = await ApiHttp.ReadFromJsonAsync<SaleCreatedResponse>(res, cancellationToken);
        return dto.InvoiceId;
    }
}
