using OilChangePOS.Business;

namespace OilChangePOS.WinForms.Remote;

internal sealed class HttpCustomerService(HttpClient http) : ICustomerService
{
    public async Task<List<CustomerListDto>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        using var res = await http.GetAsync("api/Customers", cancellationToken);
        return await ApiHttp.ReadFromJsonAsync<List<CustomerListDto>>(res, cancellationToken);
    }
}
