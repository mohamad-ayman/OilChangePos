using Microsoft.EntityFrameworkCore;
using OilChangePOS.Domain;

namespace OilChangePOS.Data;

public class OilChangePosDbContext(DbContextOptions<OilChangePosDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ServiceDetail> ServiceDetails => Set<ServiceDetail>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<StockAudit> StockAudits => Set<StockAudit>();
    public DbSet<StockAuditLine> StockAuditLines => Set<StockAuditLine>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<BranchProductPrice> BranchProductPrices => Set<BranchProductPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Product>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.ProductCategory).HasMaxLength(50).IsRequired();
            b.Property(x => x.PackageSize).HasMaxLength(20).IsRequired();
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.HasIndex(x => new { x.CompanyId, x.Name, x.ProductCategory, x.PackageSize }).IsUnique();
            b.HasOne(x => x.Company)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Warehouse>(b =>
        {
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Customer>(b =>
        {
            b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            b.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
            b.HasIndex(x => x.PhoneNumber);
        });

        modelBuilder.Entity<Car>(b =>
        {
            b.Property(x => x.PlateNumber).HasMaxLength(20).IsRequired();
            b.Property(x => x.Make).HasMaxLength(100).IsRequired();
            b.Property(x => x.Model).HasMaxLength(100).IsRequired();
            b.HasIndex(x => x.PlateNumber).IsUnique();
            b.HasOne(x => x.Customer)
                .WithMany(x => x.Cars)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(b =>
        {
            b.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
            b.Property(x => x.PaymentMethod).HasMaxLength(30).IsRequired();
            b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            b.Property(x => x.Subtotal).HasPrecision(18, 2);
            b.Property(x => x.Total).HasPrecision(18, 2);
            b.HasIndex(x => x.InvoiceNumber).IsUnique();
            b.HasIndex(x => x.WarehouseId);
            b.HasOne(x => x.Customer).WithMany(x => x.Invoices).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceItem>(b =>
        {
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.LineTotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<ServiceOrder>(b =>
        {
            b.Property(x => x.ServiceNumber).HasMaxLength(30).IsRequired();
            b.Property(x => x.Subtotal).HasPrecision(18, 2);
            b.Property(x => x.Total).HasPrecision(18, 2);
            b.HasIndex(x => x.ServiceNumber).IsUnique();
            b.HasOne(x => x.Customer)
                .WithMany(x => x.ServiceOrders)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Car)
                .WithMany(x => x.ServiceOrders)
                .HasForeignKey(x => x.CarId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceDetail>(b =>
        {
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.UnitPrice).HasPrecision(18, 2);
            b.Property(x => x.LineTotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<StockMovement>(b =>
        {
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.Notes).HasMaxLength(500);
            b.HasIndex(x => new { x.ProductId, x.MovementDateUtc });
            b.HasIndex(x => new { x.ToWarehouseId, x.FromWarehouseId, x.MovementDateUtc });
            b.HasIndex(x => x.SourcePurchaseId);
            b.HasOne(x => x.FromWarehouse).WithMany().HasForeignKey(x => x.FromWarehouseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ToWarehouse).WithMany().HasForeignKey(x => x.ToWarehouseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.SourcePurchase)
                .WithMany()
                .HasForeignKey(x => x.SourcePurchaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Purchase>(b =>
        {
            b.Property(x => x.Quantity).HasPrecision(18, 3);
            b.Property(x => x.PurchasePrice).HasPrecision(18, 2);
            b.Property(x => x.Notes).HasMaxLength(500);
            b.HasOne(x => x.Product).WithMany(x => x.Purchases).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockAudit>(b =>
        {
            b.Property(x => x.Notes).HasMaxLength(500);
            b.HasIndex(x => new { x.WarehouseId, x.AuditDateUtc });
            b.HasOne(x => x.Warehouse)
                .WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockAuditLine>(b =>
        {
            b.Property(x => x.SystemQuantity).HasPrecision(18, 3);
            b.Property(x => x.ActualQuantity).HasPrecision(18, 3);
            b.Property(x => x.ReasonCode).HasMaxLength(50).IsRequired();
        });

        modelBuilder.Entity<Expense>(b =>
        {
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Category).HasMaxLength(80).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.HasIndex(x => x.ExpenseDateUtc);
            b.HasIndex(x => x.WarehouseId);
            b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BranchProductPrice>(b =>
        {
            b.Property(x => x.SalePrice).HasPrecision(18, 2);
            b.HasIndex(x => new { x.WarehouseId, x.ProductId }).IsUnique();
            b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Product).WithMany(x => x.BranchProductPrices).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AppUser>(b =>
        {
            b.Property(x => x.Username).HasMaxLength(50).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            b.HasIndex(x => x.Username).IsUnique();
        });
    }
}
