using OilChangePOS.Domain;

namespace OilChangePOS.Business;

public record SaleItemRequest(int ProductId, decimal Quantity);
public record CompleteSaleRequest(int? CustomerId, decimal DiscountAmount, int UserId, int WarehouseId, List<SaleItemRequest> Items);
public record PurchaseStockRequest(int ProductId, decimal Quantity, decimal PurchasePrice, DateTime ProductionDate, DateTime PurchaseDate, int WarehouseId, string Notes, int UserId);

/// <summary>One line on a multi-SKU purchase receipt (same supplier, posted to main warehouse).</summary>
public record PurchaseReceiptLineInput(
    int ProductId,
    decimal Quantity,
    decimal UnitPurchasePrice,
    DateTime PurchaseDate,
    DateTime ProductionDate,
    string? LineNote);

/// <summary>Result of <see cref="IInventoryService.AddPurchaseReceiptBatchAsync"/>.</summary>
public record PurchaseReceiptBatchResult(int LinesPosted, IReadOnlyList<int> PurchaseIds);
/// <param name="BranchSalePriceForDestination">When set, updates <see cref="BranchProductPrice"/> for the destination branch (only valid for Main → Branch transfers).</param>
public record TransferStockRequest(int ProductId, decimal Quantity, int FromWarehouseId, int ToWarehouseId, string Notes, int UserId, decimal? BranchSalePriceForDestination = null);
public record AuditLineRequest(int ProductId, decimal ActualQuantity, int WarehouseId, string? ReasonCode = null);
public record OilChangeRequest(int CustomerId, int CarId, int OdometerKm, int UserId, int WarehouseId, List<SaleItemRequest> Details);
public record SetBranchPriceRequest(int ProductId, int WarehouseId, decimal SalePrice, int UserId);

public interface IInventoryService
{
    Task<decimal> GetCurrentStockAsync(int productId, int warehouseId, CancellationToken cancellationToken = default);
    Task<List<LowStockItemDto>> GetLowStockAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task<int> AddStockAsync(PurchaseStockRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts several purchase lines in one database transaction (main warehouse only).
    /// Each line becomes a <see cref="Domain.Purchase"/> plus matching <see cref="Domain.StockMovement"/>.
    /// Per-line purchase and production dates are taken from <see cref="PurchaseReceiptLineInput"/>.
    /// </summary>
    Task<PurchaseReceiptBatchResult> AddPurchaseReceiptBatchAsync(
        int userId,
        int warehouseId,
        string supplierName,
        string? receiptMemo,
        IReadOnlyList<PurchaseReceiptLineInput> lines,
        CancellationToken cancellationToken = default);
    Task<StockAuditResultDto> RunStockAuditAsync(int userId, int warehouseId, List<AuditLineRequest> lines, string notes, CancellationToken cancellationToken = default);
    Task<List<StockAuditHistoryRowDto>> GetStockAuditHistoryAsync(int? warehouseId, DateTime fromUtc, DateTime toUtcExclusive, CancellationToken cancellationToken = default);

    /// <summary>Override rows only; key = product id. Empty when no branch-specific price exists.</summary>
    Task<IReadOnlyDictionary<int, decimal>> GetBranchSalePriceOverridesAsync(int warehouseId, IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default);

    Task<decimal> GetEffectiveSalePriceAsync(int productId, int warehouseId, CancellationToken cancellationToken = default);
    Task<List<BranchPriceRowDto>> GetBranchPricesAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task SetBranchSalePriceAsync(int userId, int warehouseId, int productId, decimal salePrice, CancellationToken cancellationToken = default);
    Task DeleteBranchSalePriceAsync(int userId, int warehouseId, int productId, CancellationToken cancellationToken = default);
}

public interface ITransferService
{
    Task<int> TransferStockAsync(TransferStockRequest request, CancellationToken cancellationToken = default);
}

public record CreateBranchStockRequestDto(int ProductId, decimal Quantity, string? Notes);

public record BranchStockRequestRowDto(
    int Id,
    int BranchWarehouseId,
    string BranchWarehouseName,
    int ProductId,
    string ProductDisplayName,
    decimal Quantity,
    string Notes,
    string Status,
    int RequestedByUserId,
    string RequestedByUsername,
    DateTime CreatedAtUtc,
    int? ResolvedByUserId,
    string? ResolvedByUsername,
    DateTime? ResolvedAtUtc,
    string? ResolutionNotes,
    int? FulfillmentStockMovementId);

public interface IBranchStockRequestService
{
    Task<int> CreateForHomeBranchAsync(int userId, CreateBranchStockRequestDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchStockRequestRowDto>> ListAsync(
        int userId,
        int? branchWarehouseIdFilter,
        CancellationToken cancellationToken = default);
    Task RejectAsync(int adminUserId, int requestId, string? notes, CancellationToken cancellationToken = default);
    Task FulfillAsync(int adminUserId, int requestId, CancellationToken cancellationToken = default);
    Task CancelOwnPendingAsync(int userId, int requestId, CancellationToken cancellationToken = default);
}

public interface ISalesService
{
    Task<int> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Active catalog rows for POS/API (no EF entities on the wire).</summary>
public record ProductListDto(int Id, string Name, string ProductCategory, string PackageSize, decimal UnitPrice, string CompanyName);

public interface IProductCatalogService
{
    Task<IReadOnlyList<ProductListDto>> GetActiveProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>Product rows for POS / inventory / audit (optional active-only filter).</summary>
    Task<IReadOnlyList<ProductSummaryDto>> GetProductSummariesAsync(bool activeOnly, CancellationToken cancellationToken = default);
}

public interface IServiceOrderService
{
    Task<int> CreateOilChangeServiceAsync(OilChangeRequest request, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<DailySalesDto> GetDailySalesReportAsync(DateTime dateUtc, CancellationToken cancellationToken = default);

    /// <summary>Invoices for <paramref name="dateUtc"/> day (UTC) at <paramref name="warehouseId"/>; legacy null <c>WarehouseId</c> counts as main when main exists.</summary>
    Task<DailySalesDto> GetDailySalesReportForWarehouseAsync(DateTime dateUtc, int warehouseId, CancellationToken cancellationToken = default);
    Task<List<InventorySnapshotDto>> GetInventorySnapshotAsync(int warehouseId, CancellationToken cancellationToken = default);
    Task<SalesDashboardDto> GetSalesDashboardAsync(DateTime fromUtc, DateTime toUtc, int warehouseId, CancellationToken cancellationToken = default);
    Task<List<SalesByWarehouseSummaryDto>> GetSalesSummariesByWarehouseAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    /// <param name="warehouseId">When null, all warehouses (invoice <c>WarehouseId</c> must match filter if set).</param>
    Task<SalesPeriodSummaryDto> GetSalesPeriodSummaryAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default);

    Task<List<InvoiceProfitDto>> GetInvoiceProfitBreakdownAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default);
    Task<List<ProductProfitDto>> GetProductProfitBreakdownAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default);
    Task<ProfitRollupDto> GetProfitRollupAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default);

    /// <summary>Stock derived from movement in − out (same as ledger). Optional warehouse; retail value uses branch override when set, else <c>Product.UnitPrice</c>.</summary>
    Task<List<WarehouseStockMovementRowDto>> GetCurrentStockFromMovementsAsync(int? warehouseId, CancellationToken cancellationToken = default);

    Task<List<StockMovementHistoryRowDto>> GetStockMovementHistoryAsync(int productId, DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default);

    Task<List<TransferLedgerRowDto>> GetTransfersReportAsync(DateTime fromLocalDate, DateTime toLocalDate, int? fromWarehouseId, int? toWarehouseId, CancellationToken cancellationToken = default);

    /// <summary>Transfers where <paramref name="warehouseId"/> is source or destination (فرع: وارد/صادر).</summary>
    Task<List<TransferLedgerRowDto>> GetBranchTransferLedgerAsync(
        DateTime fromLocalDate,
        DateTime toLocalDate,
        int warehouseId,
        CancellationToken cancellationToken = default);

    Task<List<TopSellingProductDto>> GetTopSellingProductsAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, int take, CancellationToken cancellationToken = default);
    Task<List<SlowMovingProductDto>> GetSlowMovingProductsAsync(DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, int take, CancellationToken cancellationToken = default);

    Task<List<DailyCashFlowRowDto>> GetDailyCashFlowAsync(DateTime fromLocalDate, DateTime toLocalDate, CancellationToken cancellationToken = default);
    Task<List<ExpenseReportRowDto>> GetExpensesInPeriodAsync(DateTime fromLocalDate, DateTime toLocalDate, int? warehouseId, CancellationToken cancellationToken = default);

    /// <summary>Branch POS: one row per invoice line for the selected warehouse and local-date range (حصر المبيعات).</summary>
    Task<List<BranchSalesLineRegisterDto>> GetBranchSalesLineRegisterAsync(DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, CancellationToken cancellationToken = default);

    /// <summary>Branch: purchases into the site + transfers in (وارد) for the same warehouse and period.</summary>
    Task<List<BranchIncomingRegisterDto>> GetBranchIncomingRegisterAsync(DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, CancellationToken cancellationToken = default);

    /// <summary>Branch: sales totals grouped by cashier / invoice creator (البائع).</summary>
    Task<List<BranchSellerSalesSummaryDto>> GetBranchSalesBySellerAsync(DateTime fromLocalDate, DateTime toLocalDate, int warehouseId, CancellationToken cancellationToken = default);
}

public interface IExpenseService
{
    Task<int> RecordExpenseAsync(decimal amount, string category, string description, DateTime expenseDateLocal, int? warehouseId, int userId, CancellationToken cancellationToken = default);
}

public interface ICustomerService
{
    Task<List<CustomerListDto>> ListActiveAsync(CancellationToken cancellationToken = default);
}

public interface IAuthService
{
    Task<AppUser?> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BranchRoleUserDto>> ListBranchRoleUsersAsync(int requestingUserId, CancellationToken cancellationToken = default);
    Task SetUserHomeBranchWarehouseAsync(int requestingUserId, int targetUserId, int? homeBranchWarehouseId, CancellationToken cancellationToken = default);
}

/// <summary>Users with <see cref="UserRole.Manager"/> or <see cref="UserRole.Cashier"/> for admin assignment of default POS warehouse.</summary>
public record BranchRoleUserDto(int Id, string Username, int? HomeBranchWarehouseId);

/// <summary>Admin console row — includes branch display name when <see cref="HomeBranchWarehouseId"/> is set.</summary>
public record AdminUserRowDto(int Id, string Username, string Role, bool IsActive, int? HomeBranchWarehouseId, string? HomeBranchName);

public interface IUserManagementService
{
    Task<IReadOnlyList<AdminUserRowDto>> ListUsersAsync(int requestingUserId, CancellationToken cancellationToken = default);
    Task<int> CreateUserAsync(int requestingUserId, string username, string password, UserRole role, int? homeBranchWarehouseId, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(int requestingUserId, int userId, UserRole role, bool isActive, int? homeBranchWarehouseId, CancellationToken cancellationToken = default);
    Task SetPasswordAsync(int requestingUserId, int userId, string newPassword, CancellationToken cancellationToken = default);
}

public interface IWarehouseService
{
    Task<List<WarehouseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<WarehouseDto>> GetBranchesAsync(CancellationToken cancellationToken = default);
    Task<WarehouseDto?> GetMainAsync(CancellationToken cancellationToken = default);
    /// <summary>All branch sites for admin maintenance (includes inactive).</summary>
    Task<List<WarehouseDto>> ListBranchesForAdminAsync(CancellationToken cancellationToken = default);
    Task<int> CreateBranchAsync(string name, int adminUserId, CancellationToken cancellationToken = default);
    Task UpdateBranchAsync(int branchWarehouseId, string name, bool isActive, int adminUserId, CancellationToken cancellationToken = default);
}

public record LowStockItemDto(int ProductId, string ProductName, decimal CurrentStock, decimal Threshold);
public record InventorySnapshotDto(int ProductId, string ProductName, decimal CurrentStock, decimal UnitPrice, decimal StockValue);
public record WarehouseDto(int Id, string Name, WarehouseType Type, bool IsActive = true);
public record DailySalesDto(DateTime DateUtc, int InvoiceCount, decimal TotalSales, decimal TotalDiscounts);
public record StockAuditResultDto(int AuditId, int AdjustedProductsCount);
public record StockAuditHistoryRowDto(
    int AuditId,
    DateTime AuditDateLocal,
    string WarehouseName,
    string ProductName,
    decimal SystemQuantity,
    decimal ActualQuantity,
    decimal Variance,
    string ReasonDisplay,
    string AuditNotes,
    string CreatedByUsername);
public record TopSellingProductDto(string ProductName, decimal QuantitySold, decimal SalesAmount);
public record SlowMovingProductDto(string ProductName, decimal OnHandAtWarehouse, decimal QuantitySoldInPeriod);
public record TransferLedgerRowDto(DateTime MovementUtc, string ProductName, decimal Quantity, string FromWarehouseName, string ToWarehouseName, string Notes);
public record SalesByWarehouseSummaryDto(int WarehouseId, string WarehouseName, string SiteType, int InvoiceCount, decimal NetSales, decimal GrossSales, decimal TotalDiscounts);
public record CustomerListDto(int Id, string DisplayName);
public record SalesDashboardDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int InvoiceCount,
    decimal GrossSales,
    decimal TotalDiscounts,
    decimal NetSales,
    decimal AverageInvoice,
    decimal InventoryValue,
    int LowStockCount,
    List<TopSellingProductDto> TopProducts,
    decimal EstimatedCogs,
    decimal EstimatedGrossProfit,
    List<SlowMovingProductDto> SlowMovingProducts,
    List<TransferLedgerRowDto> TransfersInPeriod);

public record SalesPeriodSummaryDto(
    DateTime FromLocalDate,
    DateTime ToLocalDate,
    int InvoiceCount,
    decimal GrossSales,
    decimal TotalDiscounts,
    decimal NetSales,
    decimal AverageInvoiceValue);

public record InvoiceProfitDto(
    int InvoiceId,
    string InvoiceNumber,
    DateTime InvoiceDateUtc,
    string? WarehouseName,
    decimal NetRevenue,
    decimal EstimatedCogs,
    decimal EstimatedGrossProfit,
    decimal MarginPercent);

public record ProductProfitDto(
    int ProductId,
    string ProductName,
    decimal QuantitySold,
    decimal Revenue,
    decimal EstimatedCogs,
    decimal EstimatedGrossProfit);

public record ProfitRollupDto(
    decimal TotalRevenue,
    decimal TotalEstimatedCogs,
    decimal TotalEstimatedGrossProfit);

public record WarehouseStockMovementRowDto(
    int ProductId,
    string ProductName,
    string Category,
    string PackageSize,
    int WarehouseId,
    string WarehouseName,
    string SiteTypeLabel,
    decimal QuantityOnHand,
    decimal RetailUnitPrice,
    decimal RetailStockValue);

public record StockMovementHistoryRowDto(
    DateTime MovementDateUtc,
    string MovementType,
    decimal Quantity,
    int? FromWarehouseId,
    string? FromWarehouseName,
    int? ToWarehouseId,
    string? ToWarehouseName,
    string Notes);

public record DailyCashFlowRowDto(
    DateTime DayLocal,
    decimal SalesIncome,
    decimal ServiceIncome,
    decimal PurchaseCashOut,
    decimal OperatingExpenses,
    decimal NetCashIndicator);

public record ExpenseReportRowDto(
    int Id,
    DateTime ExpenseDateUtc,
    decimal Amount,
    string Category,
    string Description,
    string? WarehouseName,
    string? CreatedByUsername);

public record BranchPriceRowDto(int ProductId, string ProductName, int WarehouseId, string WarehouseName, decimal SalePrice);

/// <summary>Detailed branch sales lines for audit (invoice × SKU).</summary>
public record BranchSalesLineRegisterDto(
    DateTime InvoiceDateUtc,
    string InvoiceNumber,
    string WarehouseName,
    string CustomerDisplay,
    string SellerUsername,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal InvoiceSubtotal,
    decimal InvoiceDiscount,
    decimal InvoiceTotal);

/// <summary>Stock entering the branch: direct purchases and transfer receipts.</summary>
public record BranchIncomingRegisterDto(
    DateTime EntryDateUtc,
    string EntryType,
    string ProductName,
    decimal Quantity,
    decimal AmountValue,
    string SourceDetail,
    string Notes,
    string CreatedByDisplay);

/// <summary>Per-cashier rollup for branch-attributed invoices in the period.</summary>
public record BranchSellerSalesSummaryDto(
    string SellerUsername,
    int InvoiceCount,
    int LineItemCount,
    decimal InvoicesGrossSubtotal,
    decimal InvoicesDiscountTotal,
    decimal InvoicesNetTotal);

public sealed class ProductSummaryDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public string PackageSize { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CatalogCompanyListRowDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
}

public sealed class CatalogProductListRowDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public string PackageSize { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CompanyComboItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class MainWarehouseGridRowDto
{
    public int ProductId { get; set; }
    public int? PurchaseId { get; set; }
    public int BatchNumber { get; set; }
    public int BatchTotal { get; set; }
    public string BatchLabel { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string InventoryName { get; set; } = string.Empty;
    public DateTime ProductionDate { get; set; }
    public decimal PurchasedQuantity { get; set; }
    public decimal? OnHandAtMain { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string ProductCategory { get; set; } = string.Empty;
    public string PackageSize { get; set; } = string.Empty;
}

public sealed class MainWarehouseCatalogEntryDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public string PackageSize { get; set; } = string.Empty;
    public bool IsPlaceholder { get; set; }
    public string Caption =>
        IsPlaceholder
            ? "— اختر الصنف —"
            : string.IsNullOrWhiteSpace(CompanyName)
                ? $"{Name} — {ProductCategory} / {PackageSize}"
                : $"{CompanyName} — {Name} ({ProductCategory}, {PackageSize})";
}

public sealed class MainWarehouseExcelImportLineDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string PackageSize { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime ProductionDate { get; set; }
    public DateTime PurchaseDate { get; set; }
}

public sealed class UpdateMainWarehousePurchaseRequest
{
    public int PurchaseId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public string ProductCategory { get; set; } = string.Empty;
    public string PackageSize { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime ProductionDate { get; set; }
    public DateTime PurchaseDate { get; set; }
}

public sealed class BranchPriceOverrideQueryDto
{
    public int WarehouseId { get; set; }
    public List<int> ProductIds { get; set; } = [];
}

public sealed class BranchPriceOverrideItemDto
{
    public int ProductId { get; set; }
    public decimal SalePrice { get; set; }
}

public interface ICatalogAdminService
{
    Task<IReadOnlyList<CatalogCompanyListRowDto>> ListCompaniesForCatalogAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogProductListRowDto>> ListProductsForCompanyAsync(int companyId, CancellationToken cancellationToken = default);
    Task SaveCatalogCompanyAsync(bool createNew, int? existingCompanyId, string name, bool isActive, CancellationToken cancellationToken = default);
    Task SaveCatalogProductAsync(bool createNew, int companyId, int? existingProductId, string name, string category, string package, bool isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CompanyComboItemDto>> ListActiveCompaniesForComboAsync(CancellationToken cancellationToken = default);
    Task<int> CreatePosTabProductAsync(int companyId, string name, string category, string package, decimal unitPrice, CancellationToken cancellationToken = default);
    Task<bool> PosTabProductExistsAsync(int companyId, string name, string category, string package, CancellationToken cancellationToken = default);
}

public interface IMainWarehouseAdminService
{
    Task<IReadOnlyList<MainWarehouseGridRowDto>> GetGridRowsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MainWarehouseCatalogEntryDto>> GetCatalogEntriesAsync(CancellationToken cancellationToken = default);
    Task UpdatePurchaseLineAsync(UpdateMainWarehousePurchaseRequest request, CancellationToken cancellationToken = default);
    Task DeletePurchaseLineAsync(int purchaseId, CancellationToken cancellationToken = default);
    Task<int> ImportExcelLinesAsync(int userId, int mainWarehouseId, IReadOnlyList<MainWarehouseExcelImportLineDto> lines, CancellationToken cancellationToken = default);
}
