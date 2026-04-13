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
    Branch = 2
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
    public UserRole Role { get; set; } = UserRole.Branch;
    public bool IsActive { get; set; } = true;
}

/// <summary>Manual operating expenses (rent, utilities, etc.) for cash-flow reporting — not COGS.</summary>
public class Expense
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ExpenseDateUtc { get; set; } = DateTime.UtcNow;
    public int? WarehouseId { get; set; }
    public int CreatedByUserId { get; set; }

    public Warehouse? Warehouse { get; set; }
    public AppUser? CreatedByUser { get; set; }
}
