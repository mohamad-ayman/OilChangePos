using Microsoft.AspNetCore.Mvc;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CustomerListDto>>> List(CancellationToken ct) =>
        Ok(await customers.ListActiveAsync(ct));
}
