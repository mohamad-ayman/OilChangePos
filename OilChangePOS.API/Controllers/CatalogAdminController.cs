using Microsoft.AspNetCore.Mvc;
using OilChangePOS.Business;

namespace OilChangePOS.API.Controllers;

[ApiController]
[Route("api/catalog-admin")]
public sealed class CatalogAdminController(ICatalogAdminService catalog) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<ActionResult<IReadOnlyList<CatalogCompanyListRowDto>>> Companies(CancellationToken ct) =>
        Ok(await catalog.ListCompaniesForCatalogAsync(ct));

    [HttpGet("companies/{companyId:int}/products")]
    public async Task<ActionResult<IReadOnlyList<CatalogProductListRowDto>>> Products(int companyId, CancellationToken ct) =>
        Ok(await catalog.ListProductsForCompanyAsync(companyId, ct));

    public sealed record SaveCompanyBody(bool CreateNew, int? ExistingCompanyId, string Name, bool IsActive);

    [HttpPost("companies")]
    public async Task<IActionResult> SaveCompany([FromBody] SaveCompanyBody body, CancellationToken ct)
    {
        await catalog.SaveCatalogCompanyAsync(body.CreateNew, body.ExistingCompanyId, body.Name, body.IsActive, ct);
        return NoContent();
    }

    public sealed record SaveProductBody(bool CreateNew, int CompanyId, int? ExistingProductId, string Name, string Category, string Package, bool IsActive);

    [HttpPost("products")]
    public async Task<IActionResult> SaveProduct([FromBody] SaveProductBody body, CancellationToken ct)
    {
        await catalog.SaveCatalogProductAsync(body.CreateNew, body.CompanyId, body.ExistingProductId, body.Name, body.Category, body.Package, body.IsActive, ct);
        return NoContent();
    }

    [HttpGet("company-combo")]
    public async Task<ActionResult<IReadOnlyList<CompanyComboItemDto>>> CompanyCombo(CancellationToken ct) =>
        Ok(await catalog.ListActiveCompaniesForComboAsync(ct));

    [HttpGet("pos-product-exists")]
    public async Task<ActionResult<bool>> PosProductExists([FromQuery] int companyId, [FromQuery] string name, [FromQuery] string category, [FromQuery] string package, CancellationToken ct) =>
        Ok(await catalog.PosTabProductExistsAsync(companyId, name, category, package, ct));

    public sealed record CreatePosProductBody(int CompanyId, string Name, string Category, string Package, decimal UnitPrice);

    [HttpPost("pos-products")]
    public async Task<ActionResult<int>> CreatePosProduct([FromBody] CreatePosProductBody body, CancellationToken ct) =>
        Ok(await catalog.CreatePosTabProductAsync(body.CompanyId, body.Name, body.Category, body.Package, body.UnitPrice, ct));
}
