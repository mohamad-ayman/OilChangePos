using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OilChangePOS.Data;

/// <summary>
/// Design-time factory for EF Core CLI (migrations). Connection string matches WinForms default localdb.
/// </summary>
public sealed class OilChangePosDbContextFactory : IDesignTimeDbContextFactory<OilChangePosDbContext>
{
    public OilChangePosDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OilChangePosDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\MSSQLLocalDB;Database=OilChangePOSDb;Trusted_Connection=True;TrustServerCertificate=True;");
        return new OilChangePosDbContext(optionsBuilder.Options);
    }
}
