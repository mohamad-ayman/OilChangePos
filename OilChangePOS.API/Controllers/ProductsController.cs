using Microsoft.AspNetCore.Mvc;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ProductsController(IProductCatalogService catalog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductListDto>>> GetAsync(CancellationToken cancellationToken)
    {
        var items = await catalog.GetActiveProductsAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("summaries")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductSummaryDto>>> SummariesAsync(
        [FromQuery] bool activeOnly,
        CancellationToken cancellationToken) =>
        Ok(await catalog.GetProductSummariesAsync(activeOnly, cancellationToken));
}
