using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm
{
    private async Task LoadDataAsync()
    {
        LoadProductImageMap();
        await LoadPosCustomersAsync();
        await LoadWarehousesAsync();
        if (_currentUser.Role == UserRole.Admin)
        {
            await RefreshBranchesGridAsync();
            await RefreshCatalogGridsAsync();
        }

        await RefreshAllStockViewsAsync();
        _cartBinding.DataSource = new List<CartRow>();
        RefreshCartSummary();
        await RefreshAuditHistoryAsync();
        await RefreshDailyKpisAsync();
    }

    private async Task LoadWarehousesAsync()
    {
        var allWarehouses = await _warehouseService.GetAllAsync();
        var branches = await _warehouseService.GetBranchesAsync();
        // Branches only for POS + Inventory: main hub is receive-only on Main Warehouse tab; no “shortcut” to main stock here.
        var opsWarehouses = branches.Count > 0 ? branches.ToList() : allWarehouses.ToList();
        _posWarehouseCombo.DataSource = opsWarehouses.ToList();
        _posWarehouseCombo.DisplayMember = nameof(WarehouseDto.Name);
        _posWarehouseCombo.ValueMember = nameof(WarehouseDto.Id);

        _inventoryWarehouseCombo.DataSource = opsWarehouses.ToList();
        _inventoryWarehouseCombo.DisplayMember = nameof(WarehouseDto.Name);
        _inventoryWarehouseCombo.ValueMember = nameof(WarehouseDto.Id);

        var transferList = allWarehouses.ToList();
        _transferFromWarehouseCombo.DataSource = transferList;
        _transferFromWarehouseCombo.DisplayMember = nameof(WarehouseDto.Name);
        _transferFromWarehouseCombo.ValueMember = nameof(WarehouseDto.Id);
        _transferToWarehouseCombo.DataSource = transferList.ToList();
        _transferToWarehouseCombo.DisplayMember = nameof(WarehouseDto.Name);
        _transferToWarehouseCombo.ValueMember = nameof(WarehouseDto.Id);

        var mainWarehouse = await _warehouseService.GetMainAsync();
        if (mainWarehouse is not null)
            _transferFromWarehouseCombo.SelectedValue = mainWarehouse.Id;

        var defaultBranch = branches.FirstOrDefault() ?? allWarehouses.FirstOrDefault(w => w.Type == WarehouseType.Branch);
        if (defaultBranch is not null)
        {
            _transferToWarehouseCombo.SelectedValue = defaultBranch.Id;
            _posWarehouseCombo.SelectedValue = defaultBranch.Id;
            _inventoryWarehouseCombo.SelectedValue = defaultBranch.Id;
        }
        else if (opsWarehouses.FirstOrDefault() is { } fallback)
        {
            _posWarehouseCombo.SelectedValue = fallback.Id;
            _inventoryWarehouseCombo.SelectedValue = fallback.Id;
        }

        _reportWarehouseCombo.DataSource = allWarehouses.ToList();
        _reportWarehouseCombo.DisplayMember = nameof(WarehouseDto.Name);
        _reportWarehouseCombo.ValueMember = nameof(WarehouseDto.Id);
        if (defaultBranch is not null)
            _reportWarehouseCombo.SelectedValue = defaultBranch.Id;
        else if (allWarehouses.FirstOrDefault() is { } rw)
            _reportWarehouseCombo.SelectedValue = rw.Id;
        if (!_reportWarehouseEventsAttached)
        {
            _reportWarehouseEventsAttached = true;
            _reportWarehouseCombo.SelectedIndexChanged += async (_, _) => await RefreshReportsAsync();
        }

        if (!_posInventoryWarehouseEventsAttached)
        {
            _posInventoryWarehouseEventsAttached = true;
            _posWarehouseCombo.SelectedIndexChanged += async (_, _) =>
            {
                UpdatePosStockLocationLabel();
                await RefreshAvailableProductsAsync();
                await RefreshReportsAsync();
                await RefreshBranchOnlyReportsAsync();
                await RefreshDailyKpisAsync();
            };
            _inventoryWarehouseCombo.SelectedIndexChanged += async (_, _) =>
            {
                await RefreshInventoryAsync();
                await RefreshAuditViewAsync();
                await RefreshReportsAsync();
                await RefreshBranchOnlyReportsAsync();
                await RefreshAuditHistoryAsync();
            };
        }

        UpdatePosStockLocationLabel();

        _mwBranchPriceWarehouseCombo.DataSource = null;
        _mwBranchPriceWarehouseCombo.DisplayMember = nameof(WarehouseDto.Name);
        _mwBranchPriceWarehouseCombo.ValueMember = nameof(WarehouseDto.Id);
        _mwBranchPriceWarehouseCombo.DataSource = branches.Count > 0 ? branches.ToList() : opsWarehouses.ToList();
        if (defaultBranch is not null)
            _mwBranchPriceWarehouseCombo.SelectedValue = defaultBranch.Id;
        else if (opsWarehouses.FirstOrDefault() is { } mwfb)
            _mwBranchPriceWarehouseCombo.SelectedValue = mwfb.Id;

        if (!_mwBranchPriceEventsAttached)
        {
            _mwBranchPriceEventsAttached = true;
            _mwBranchPriceWarehouseCombo.SelectedIndexChanged += async (_, _) =>
            {
                await SyncMainWarehouseBranchPricePanelAsync();
            };
        }

        await SyncMainWarehouseBranchPricePanelAsync();
    }

    private static bool TryGetWarehouseIdFromCombo(ComboBox combo, out int warehouseId)
    {
        warehouseId = 0;
        // Bound combos sometimes leave SelectedValue null/DBNull even when SelectedItem is set.
        if (combo.SelectedItem is WarehouseDto dtoFromItem)
        {
            warehouseId = dtoFromItem.Id;
            return warehouseId > 0;
        }

        var v = combo.SelectedValue;
        if (v is null || v is DBNull)
            return false;

        if (v is int i)
        {
            warehouseId = i;
            return warehouseId > 0;
        }

        if (v is long l)
        {
            warehouseId = (int)l;
            return warehouseId > 0;
        }

        if (int.TryParse(Convert.ToString(v), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            warehouseId = parsed;
            return warehouseId > 0;
        }

        return false;
    }

    private static void SyncWarehouseComboToWarehouseId(ComboBox combo, int warehouseId)
    {
        if (warehouseId <= 0 || combo.Items.Count == 0)
            return;
        for (var idx = 0; idx < combo.Items.Count; idx++)
        {
            if (combo.Items[idx] is WarehouseDto w && w.Id == warehouseId)
            {
                if (combo.SelectedIndex != idx)
                    combo.SelectedIndex = idx;
                return;
            }
        }
    }


    /// <summary>
    /// Warehouse for Inventory / Audit / reports: branch combos first (same list as operational screens), never default to Main.
    /// </summary>
    private async Task<(bool Ok, int WarehouseId)> TryResolveStockContextWarehouseIdAsync(bool useReportWarehouseScope = false)
    {
        if (useReportWarehouseScope && TryGetWarehouseIdFromCombo(_reportWarehouseCombo, out var fromReport))
            return (true, fromReport);
        if (TryGetWarehouseIdFromCombo(_inventoryWarehouseCombo, out var fromInventory)) return (true, fromInventory);
        if (TryGetWarehouseIdFromCombo(_posWarehouseCombo, out var fromPos)) return (true, fromPos);
        var branches = await _warehouseService.GetBranchesAsync();
        if (branches.FirstOrDefault() is { } b) return (true, b.Id);
        return (false, 0);
    }

    private async Task RefreshAllStockViewsAsync()
    {
        await RefreshMainWarehouseGridAsync();
        await RefreshInventoryAsync();
        await RefreshAvailableProductsAsync();
        await RefreshAuditViewAsync();
        await RefreshTransferProductsAsync();
        await RefreshReportsAsync();
        await RefreshBranchOnlyReportsAsync();
    }
    private async Task RefreshWarehouseInventoryGridAsync(int? warehouseId)
    {
        var movementRows = await _reportService.GetCurrentStockFromMovementsAsync(warehouseId);
        _warehouseInventoryGrid.DataSource = movementRows.Select(r => new WarehouseInventoryRow
        {
            ProductName = r.ProductName,
            Category = r.Category,
            PackageSize = r.PackageSize,
            Warehouse = r.WarehouseName,
            WarehouseType = r.SiteTypeLabel,
            Stock = r.QuantityOnHand
        }).ToList();
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
