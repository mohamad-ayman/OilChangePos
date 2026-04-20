using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;

namespace OilChangePOS.Business;

public class CustomerService(IDbContextFactory<OilChangePosDbContext> dbFactory) : ICustomerService
{
    public async Task<List<CustomerListDto>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Customers.AsNoTracking()
            .OrderBy(x => x.FullName)
            .Select(x => new CustomerListDto(x.Id, $"{x.FullName} · {x.PhoneNumber}"))
            .ToListAsync(cancellationToken);
    }
}
