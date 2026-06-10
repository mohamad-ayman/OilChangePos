using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;

namespace OilChangePOS.Tests;

internal sealed class TestDbContextFactory(DbContextOptions<OilChangePosDbContext> options)
    : IDbContextFactory<OilChangePosDbContext>
{
    public OilChangePosDbContext CreateDbContext() => new(options);
}
