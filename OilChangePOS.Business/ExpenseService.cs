using Microsoft.EntityFrameworkCore;
using OilChangePOS.Data;
using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public class ExpenseService(IDbContextFactory<OilChangePosDbContext> dbFactory) : IExpenseService
{
    public async Task<int> RecordExpenseAsync(decimal amount, string category, string description, DateTime expenseDateLocal, int? warehouseId, int userId, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) throw new InvalidOperationException("مبلغ المصروف يجب أن يكون أكبر من صفر.");
        category = (category ?? string.Empty).Trim();
        if (category.Length == 0) throw new InvalidOperationException("التصنيف مطلوب.");
        if (category.Length > 80) throw new InvalidOperationException("التصنيف طويل جداً.");
        description = (description ?? string.Empty).Trim();
        if (description.Length > 500) throw new InvalidOperationException("الوصف طويل جداً.");

        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(expenseDateLocal.Date, DateTimeKind.Unspecified), TimeZoneInfo.Local);
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var actor = await RbacRules.RequireUserAsync(db, userId, cancellationToken);
        RbacRules.EnsureExpenseForActor(actor, warehouseId);

        var visibleInBranchList = true;
        if (actor.Role.IsAdmin() && warehouseId is int wid)
        {
            var siteType = await db.Warehouses.AsNoTracking()
                .Where(w => w.Id == wid)
                .Select(w => (WarehouseType?)w.Type)
                .FirstOrDefaultAsync(cancellationToken);
            if (siteType == WarehouseType.Branch)
                visibleInBranchList = false;
        }

        var e = new Expense
        {
            Amount = amount,
            Category = category,
            Description = string.IsNullOrEmpty(description) ? category : description,
            ExpenseDateUtc = utc,
            WarehouseId = warehouseId,
            CreatedByUserId = userId,
            VisibleInBranchExpenseList = visibleInBranchList
        };
        db.Expenses.Add(e);
        await db.SaveChangesAsync(cancellationToken);
        return e.Id;
    }
}
