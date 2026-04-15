using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm : Form
{

    private sealed class InventoryRow
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        /// <summary>Global list price from <see cref="Domain.Product.UnitPrice"/>.</summary>
        public decimal CatalogUnitPrice { get; set; }
        /// <summary>Effective retail at the selected branch (override or catalog).</summary>
        public decimal BranchSalePrice { get; set; }
        public decimal CurrentStock { get; set; }
        public bool LowStock { get; set; }
        public string LowStockText => LowStock ? "منخفض" : "طبيعي";
    }

    private sealed class AuditRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal SystemQuantity { get; set; }
        public decimal ActualQuantity { get; set; }
        public string ReasonCode { get; set; } = StockAuditReasonCodes.PhysicalCount;
    }

    private sealed class TransferProductRow
    {
        public int ProductId { get; set; }
        public decimal AvailableQty { get; set; }
        public string Caption { get; set; } = string.Empty;
    }

    private sealed class WarehouseInventoryRow
    {
        public string ProductName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string PackageSize { get; set; } = string.Empty;
        public string Warehouse { get; set; } = string.Empty;
        public string WarehouseType { get; set; } = string.Empty;
        public decimal Stock { get; set; }
    }

}

