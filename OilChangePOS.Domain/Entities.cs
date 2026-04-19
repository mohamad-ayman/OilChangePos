namespace OilChangePOS.Domain;

public enum StockMovementType
{
    Purchase = 1,
    Sale = 2,
    Transfer = 3,
    Adjust = 4
}

public enum WarehouseType
{
    Main = 1,
    Branch = 2
}

public enum UserRole
{
    Admin = 1,
    /// <summary>Branch manager — one home branch, operational scope (POS/inventory/reports for that branch).</summary>
    Manager = 2,
    /// <summary>Branch POS operator — same branch binding as manager; intended for least-privilege POS use.</summary>
    Cashier = 3
}

/// <summary>Branch asks main warehouse for stock; admin fulfills with a transfer.</summary>
public enum BranchStockRequestStatus
{
    Pending = 0,
    Rejected = 1,
    Fulfilled = 2,
    Cancelled = 3,
}

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty; // Oil, Filter, Grease
    public string PackageSize { get; set; } = string.Empty; // 4L, 5L, 20L
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; } = true;

    public Company? Company { get; set; }
    public ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
    public ICollection<ServiceDetail> ServiceDetails { get; set; } = new List<ServiceDetail>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
    public ICollection<BranchProductPrice> BranchProductPrices { get; set; } = new List<BranchProductPrice>();
}

/// <summary>Per-branch retail override; when missing, <see cref="Product.UnitPrice"/> applies.</summary>
public class BranchProductPrice
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public int ProductId { get; set; }
    public decimal SalePrice { get; set; }

    public Warehouse? Warehouse { get; set; }
    public Product? Product { get; set; }
}

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public WarehouseType Type { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public ICollection<Car> Cars { get; set; } = new List<Car>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();
}

public class Car
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int? Year { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();
}

public class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? CustomerId { get; set; } // Walk-in supported
    /// <summary>Branch (or site) where the POS sale was posted; null for legacy rows.</summary>
    public int? WarehouseId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public int CreatedByUserId { get; set; }
    /// <summary>True when any sale line used estimated COGS (no <see cref="StockMovement.SourcePurchaseId"/> slice).</summary>
    public bool ContainsEstimatedCost { get; set; }

    public Customer? Customer { get; set; }
    public Warehouse? Warehouse { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

public class InvoiceItem
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Invoice? Invoice { get; set; }
    public Product? Product { get; set; }
}

public class ServiceOrder
{
    public int Id { get; set; }
    public string ServiceNumber { get; set; } = string.Empty;
    public DateTime ServiceDateUtc { get; set; } = DateTime.UtcNow;
    public int CustomerId { get; set; }
    public int CarId { get; set; }
    public int OdometerKm { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public int CreatedByUserId { get; set; }

    public Customer? Customer { get; set; }
    public Car? Car { get; set; }
    public AppUser? CreatedByUser { get; set; }
    public ICollection<ServiceDetail> Details { get; set; } = new List<ServiceDetail>();
}

public class ServiceDetail
{
    public int Id { get; set; }
    public int ServiceOrderId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public ServiceOrder? ServiceOrder { get; set; }
    public Product? Product { get; set; }
}

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public StockMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public DateTime MovementDateUtc { get; set; } = DateTime.UtcNow;
    public int? ReferenceId { get; set; } // InvoiceId, ServiceId, or AuditId
    /// <summary>When <see cref="MovementType"/> is <see cref="StockMovementType.Transfer"/> out of Main, which purchase batch (FIFO by production date) this slice came from.</summary>
    public int? SourcePurchaseId { get; set; }
    public int? FromWarehouseId { get; set; }
    public int? ToWarehouseId { get; set; }
    public string Notes { get; set; } = string.Empty;

    public Product? Product { get; set; }
    public Purchase? SourcePurchase { get; set; }
    public Warehouse? FromWarehouse { get; set; }
    public Warehouse? ToWarehouse { get; set; }
}

public class Purchase
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public DateTime ProductionDate { get; set; }
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    public int WarehouseId { get; set; }
    public int CreatedByUserId { get; set; }
    public string Notes { get; set; } = string.Empty;

    public Product? Product { get; set; }
    public Warehouse? Warehouse { get; set; }
    public AppUser? CreatedByUser { get; set; }
}

public class StockAudit
{
    public int Id { get; set; }
    public DateTime AuditDateUtc { get; set; } = DateTime.UtcNow;
    public string Notes { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    /// <summary>Primary warehouse this audit session applies to (lines may override per SKU). Null on pre-upgrade rows.</summary>
    public int? WarehouseId { get; set; }
    public StockAuditStatus Status { get; set; } = StockAuditStatus.Submitted;
    public AppUser? CreatedByUser { get; set; }
    public Warehouse? Warehouse { get; set; }
    public ICollection<StockAuditLine> Lines { get; set; } = new List<StockAuditLine>();
}

public class StockAuditLine
{
    public int Id { get; set; }
    public int StockAuditId { get; set; }
    public int ProductId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
    /// <summary>Stored code; see <see cref="StockAuditReasonCodes"/>.</summary>
    public string ReasonCode { get; set; } = StockAuditReasonCodes.PhysicalCount;
    public decimal Difference => ActualQuantity - SystemQuantity;

    public StockAudit? StockAudit { get; set; }
    public Product? Product { get; set; }
}

public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Cashier;
    public bool IsActive { get; set; } = true;
    /// <summary>When <see cref="Role"/> is <see cref="UserRole.Manager"/> or <see cref="UserRole.Cashier"/>, POS/inventory default to this active branch warehouse; if null, first branch by name is used.</summary>
    public int? HomeBranchWarehouseId { get; set; }
    public Warehouse? HomeBranchWarehouse { get; set; }
}

public class BranchStockRequest
{
    public int Id { get; set; }
    public int BranchWarehouseId { get; set; }
    public Warehouse BranchWarehouse { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public decimal Quantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public BranchStockRequestStatus Status { get; set; } = BranchStockRequestStatus.Pending;
    public int RequestedByUserId { get; set; }
    public AppUser RequestedByUser { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? ResolvedByUserId { get; set; }
    public AppUser? ResolvedByUser { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNotes { get; set; }
    /// <summary>First stock movement id when fulfilled (main→branch transfer may split into multiple rows).</summary>
    public int? FulfillmentStockMovementId { get; set; }
}

/// <summary>Manual operating expenses (rent, utilities, etc.) — not COGS. Set <see cref="WarehouseId"/> to attribute cost to a branch for net-profit rollups.</summary>
public class Expense
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ExpenseDateUtc { get; set; } = DateTime.UtcNow;
    public int? WarehouseId { get; set; }
    public int CreatedByUserId { get; set; }
    /// <summary>
    /// When <c>false</c>, row is still summed for branch net profit but omitted from branch-operator expense lists
    /// (set when an <see cref="UserRole.Admin"/> posts an expense attributed to a <see cref="WarehouseType.Branch"/> site).
    /// </summary>
    public bool VisibleInBranchExpenseList { get; set; } = true;

    public Warehouse? Warehouse { get; set; }
    public AppUser? CreatedByUser { get; set; }
}
