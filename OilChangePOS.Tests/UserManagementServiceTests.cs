using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Tests;

public sealed class UserManagementServiceTests
{
    [Fact]
    public async Task UpdateUserAsync_DoesNotPersistDemotionOfLastActiveAdmin()
    {
        var options = CreateOptions();
        await SeedScenarioAsync(options);
        var service = new UserManagementService(new TestDbContextFactory(options));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateUserAsync(
                requestingUserId: UserIds.Admin,
                userId: UserIds.Admin,
                role: UserRole.Manager,
                isActive: true,
                homeBranchWarehouseId: WarehouseIds.Branch));

        await using var db = new OilChangePosDbContext(options);
        var admin = await db.Users.SingleAsync(u => u.Id == UserIds.Admin);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.True(admin.IsActive);
        Assert.Null(admin.HomeBranchWarehouseId);
    }

    private static DbContextOptions<OilChangePosDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<OilChangePosDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    private static async Task SeedScenarioAsync(DbContextOptions<OilChangePosDbContext> options)
    {
        await using var db = new OilChangePosDbContext(options);
        db.Warehouses.Add(new Warehouse { Id = WarehouseIds.Branch, Name = "Branch", Type = WarehouseType.Branch });
        db.Users.Add(new AppUser
        {
            Id = UserIds.Admin,
            Username = "admin",
            PasswordHash = "not-used",
            Role = UserRole.Admin,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private static class WarehouseIds
    {
        public const int Branch = 2;
    }

    private static class UserIds
    {
        public const int Admin = 100;
    }
}
