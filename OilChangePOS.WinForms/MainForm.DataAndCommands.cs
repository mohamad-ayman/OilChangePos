using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm : Form
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

    private async Task RefreshTransferProductsAsync()
    {
        if (!TryGetWarehouseIdFromCombo(_transferFromWarehouseCombo, out var fromWarehouseId))
        {
            _transferProductCombo.DataSource = null;
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var products = await db.Products.AsNoTracking()
            .Include(p => p.Company)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Company!.Name)
            .ThenBy(x => x.Name)
            .ToListAsync();
        var list = new List<TransferProductRow>();
        foreach (var p in products)
        {
            var qty = await _inventoryService.GetCurrentStockAsync(p.Id, fromWarehouseId);
            if (qty <= 0) continue;
            var cname = p.Company?.Name ?? string.Empty;
            var label = string.IsNullOrWhiteSpace(cname) ? p.Name : $"{cname} — {p.Name}";
            list.Add(new TransferProductRow
            {
                ProductId = p.Id,
                AvailableQty = qty,
                Caption = $"{label}  —  {qty:n3} available"
            });
        }

        _transferProductCombo.DataSource = list;
        _transferProductCombo.DisplayMember = nameof(TransferProductRow.Caption);
        _transferProductCombo.ValueMember = nameof(TransferProductRow.ProductId);
        if (list.Count > 0)
            _transferProductCombo.SelectedIndex = 0;
        else
            _transferQty.Maximum = 100000;

        SyncTransferQtyLimitFromSelection();
    }

    private void SyncTransferQtyLimitFromSelection()
    {
        if (_transferProductCombo.SelectedItem is TransferProductRow row && row.AvailableQty > 0)
        {
            var max = Math.Min(100000m, row.AvailableQty);
            _transferQty.Maximum = max;
            if (_transferQty.Value > max)
                _transferQty.Value = max;
        }
        else
            _transferQty.Maximum = 100000;
    }

    private async Task RefreshInventoryAsync()
    {
        var (resolved, warehouseId) = await TryResolveStockContextWarehouseIdAsync();
        if (!resolved)
        {
            _inventoryRows = [];
            ApplyInventoryFilter();
            _inventoryProductCombo.DataSource = _inventoryRows.ToList();
            _inventoryProductCombo.DisplayMember = nameof(InventoryRow.Name);
            _inventoryProductCombo.ValueMember = nameof(InventoryRow.ProductId);
            UpdateInventoryTabKpiLabels();
            return;
        }

        if (_currentUser.Role == UserRole.Admin)
            await RefreshCompanyComboBoxesAsync();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var products = await db.Products.AsNoTracking().Include(p => p.Company).ToListAsync();
        var productIds = products.ConvertAll(p => p.Id);
        var overrides = await _inventoryService.GetBranchSalePriceOverridesAsync(warehouseId, productIds);
        var rows = new List<InventoryRow>();
        foreach (var p in products)
        {
            var stock = await _inventoryService.GetCurrentStockAsync(p.Id, warehouseId);
            var cn = p.Company?.Name ?? string.Empty;
            var display = string.IsNullOrWhiteSpace(cn) ? p.Name : $"{cn} — {p.Name}";
            var effective = overrides.TryGetValue(p.Id, out var o) ? o : p.UnitPrice;
            rows.Add(new InventoryRow
            {
                ProductId = p.Id,
                Name = display,
                Type = $"{p.ProductCategory} / {p.PackageSize}",
                CatalogUnitPrice = p.UnitPrice,
                BranchSalePrice = effective,
                CurrentStock = stock,
                LowStock = stock <= LowStockThreshold
            });
        }
        _inventoryRows = rows.OrderBy(x => x.Name).ToList();
        ApplyInventoryFilter();
        _inventoryProductCombo.DataSource = _inventoryRows.ToList();
        _inventoryProductCombo.DisplayMember = nameof(InventoryRow.Name);
        _inventoryProductCombo.ValueMember = nameof(InventoryRow.ProductId);

        UpdateInventoryTabKpiLabels();
    }

    private async Task RefreshAuditViewAsync()
    {
        var (resolved, warehouseId) = await TryResolveStockContextWarehouseIdAsync();
        if (!resolved)
        {
            _auditBinding.DataSource = Array.Empty<AuditRow>();
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var products = await db.Products.AsNoTracking().ToListAsync();
        var rows = new List<AuditRow>();
        foreach (var p in products)
        {
            var systemQty = await _inventoryService.GetCurrentStockAsync(p.Id, warehouseId);
            rows.Add(new AuditRow
            {
                ProductId = p.Id,
                ProductName = p.Name,
                SystemQuantity = systemQty,
                ActualQuantity = systemQty,
                ReasonCode = StockAuditReasonCodes.PhysicalCount
            });
        }
        _auditBinding.DataSource = rows;
    }

    private async Task RefreshAuditHistoryAsync()
    {
        try
        {
            var startLocal = DateTime.SpecifyKind(_auditHistoryFrom.Value.Date, DateTimeKind.Local);
            var endExclusiveLocal = DateTime.SpecifyKind(_auditHistoryTo.Value.Date.AddDays(1), DateTimeKind.Local);
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal);
            var toUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(endExclusiveLocal);
            int? wh = null;
            if (_auditHistoryFilterWarehouse.Checked && TryGetWarehouseIdFromCombo(_inventoryWarehouseCombo, out var wid))
                wh = wid;
            var list = await _inventoryService.GetStockAuditHistoryAsync(wh, fromUtc, toUtcExclusive);
            _auditHistoryGrid.DataSource = list;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "سجل الجرد", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }

    private async Task SetInventoryQuantityAsync()
    {
        if (_inventoryProductCombo.SelectedItem is not InventoryRow row)
        {
            return;
        }

        try
        {
            var warehouseId = await GetInventoryTargetWarehouseIdAsync();
            await _inventoryService.RunStockAuditAsync(_currentUser.Id, warehouseId,
                [new AuditLineRequest(row.ProductId, _inventorySetQty.Value, warehouseId, StockAuditReasonCodes.InventoryScreen)],
                "تعديل كمية من شاشة المخزون");
            await RefreshAllStockViewsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "فشل تحديث المخزون", MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }

    private async Task RunAuditAsync()
    {
        var rows = _auditBinding.List.Cast<AuditRow>().ToList();
        if (rows.Count == 0)
        {
            MessageBox.Show("لا توجد أصناف للجرد.", "جرد المخزون", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        try
        {
            var warehouseId = await GetInventoryTargetWarehouseIdAsync();
            var result = await _inventoryService.RunStockAuditAsync(_currentUser.Id, warehouseId,
                rows.Select(x => new AuditLineRequest(x.ProductId, x.ActualQuantity, warehouseId, x.ReasonCode)).ToList(),
                "جرد يدوي للمخزون");
            MessageBox.Show($"حُفظ الجرد #{result.AuditId}. عدد الأسطر المعدّلة: {result.AdjustedProductsCount}.", "جرد المخزون",
                MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            await RefreshAllStockViewsAsync();
            await RefreshAuditHistoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "جرد المخزون", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }

    private async Task<int> GetInventoryTargetWarehouseIdAsync()
    {
        if (TryGetWarehouseIdFromCombo(_inventoryWarehouseCombo, out var id)) return id;
        var branches = await _warehouseService.GetBranchesAsync();
        if (branches.FirstOrDefault() is { } b) return b.Id;
        throw new InvalidOperationException("يرجى اختيار مستودع فرع.");
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

    private async Task TransferStockAsync()
    {
        if (!TryGetWarehouseIdFromCombo(_transferFromWarehouseCombo, out var fromWarehouseId) ||
            !TryGetWarehouseIdFromCombo(_transferToWarehouseCombo, out var toWarehouseId))
        {
            MessageBox.Show("اختر المستودعات والصنف.", "التحويلات", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        if (_transferProductCombo.SelectedItem is not TransferProductRow transferRow)
        {
            MessageBox.Show("لا توجد أصناف برصيد في المستودع المصدر. أضف مخزوناً أو غيّر «من المستودع».", "التحويلات",
                MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        if (_transferQty.Value <= 0)
        {
            MessageBox.Show("الكمية يجب أن تكون أكبر من صفر.", "التحويلات", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        if (_transferQty.Value > transferRow.AvailableQty)
        {
            MessageBox.Show($"الكمية لا تتجاوز الرصيد في المصدر ({transferRow.AvailableQty:n3}).", "التحويلات", MessageBoxButtons.OK,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var productId = transferRow.ProductId;

        try
        {
            await _transferService.TransferStockAsync(new TransferStockRequest(
                productId,
                _transferQty.Value,
                fromWarehouseId,
                toWarehouseId,
                "تحويل يدوي من شاشة التحويلات",
                _currentUser.Id));

            MessageBox.Show("اكتمل التحويل.", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1,
                MsgRtl);
            await RefreshAllStockViewsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "فشل التحويل", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                MsgRtl);
        }
    }

    private void ConfigureInventoryColumns()
    {
        _inventoryGrid.AutoGenerateColumns = false;
        _inventoryGrid.Columns.Clear();
        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InventoryRow.Name), HeaderText = "الصنف", FillWeight = 34, ReadOnly = true });
        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InventoryRow.Type), HeaderText = "النوع", FillWeight = 14, ReadOnly = true });
        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(InventoryRow.CurrentStock),
            HeaderText = "الرصيد الحالي",
            FillWeight = 14,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(InventoryRow.CatalogUnitPrice),
            HeaderText = "السعر المرجعي",
            FillWeight = 11,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(InventoryRow.BranchSalePrice),
            HeaderText = "سعر البيع (الفرع)",
            FillWeight = 12,
            ReadOnly = false,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Format = "N2",
                Alignment = DataGridViewContentAlignment.MiddleRight,
                BackColor = Color.FromArgb(255, 251, 220)
            }
        });
        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InventoryRow.LowStockText), HeaderText = "الحالة", FillWeight = 9, ReadOnly = true });
    }

    private void ApplyInventoryFilter()
    {
        IEnumerable<InventoryRow> query = _inventoryRows;
        var term = _inventorySearchBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || x.Type.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!_inventoryShowZeroStockCheck.Checked)
            query = query.Where(x => x.CurrentStock > 0);

        if (_inventoryLowStockOnlyCheck.Checked)
            query = query.Where(x => x.LowStock);

        _inventoryBinding.DataSource = query.ToList();
    }

    private void UpdateInventoryTabKpiLabels()
    {
        var withStock = _inventoryRows.Where(x => x.CurrentStock > 0).ToList();
        _inventoryTotalItemsLabel.Text =
            $"أصناف برصيد: {withStock.Count} من {_inventoryRows.Count} SKU (قد يخفي الجدول الرصيد صفر)";
        _inventoryTotalValueLabel.Text = $"قيمة المخزون: {withStock.Sum(x => x.CurrentStock * x.BranchSalePrice):n2}";
        _inventoryLowStockLabel.Text =
            $"منخفضة (≤{LowStockThreshold}): {withStock.Count(x => x.CurrentStock > 0 && x.CurrentStock <= LowStockThreshold)}";
    }


    private void InventoryGrid_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (e.ColumnIndex < 0 || e.RowIndex < 0) return;
        if (_inventoryGrid.Columns[e.ColumnIndex].DataPropertyName != nameof(InventoryRow.BranchSalePrice)) return;
        var s = Convert.ToString(e.FormattedValue)?.Trim() ?? "";
        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out var d) &&
            !decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d))
        {
            e.Cancel = true;
            MessageBox.Show("أدخل رقماً صالحاً لسعر البيع.", "المخزون", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        if (d < 0)
        {
            e.Cancel = true;
            MessageBox.Show("السعر لا يمكن أن يكون سالباً.", "المخزون", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }

    private async void InventoryGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        if (_inventoryGrid.Columns[e.ColumnIndex].DataPropertyName != nameof(InventoryRow.BranchSalePrice)) return;
        if (_inventoryGrid.Rows[e.RowIndex].DataBoundItem is not InventoryRow row) return;
        var (ok, wh) = await TryResolveStockContextWarehouseIdAsync();
        if (!ok) return;
        try
        {
            await _inventoryService.SetBranchSalePriceAsync(_currentUser.Id, wh, row.ProductId, row.BranchSalePrice);
            row.BranchSalePrice = await _inventoryService.GetEffectiveSalePriceAsync(row.ProductId, wh);
            _inventoryBinding.ResetBindings(false);
            UpdateInventoryTabKpiLabels();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "المخزون", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            await RefreshInventoryAsync();
        }
    }

    private async Task InventoryResetBranchPriceAsync()
    {
        if (_inventoryGrid.CurrentRow?.DataBoundItem is not InventoryRow row)
        {
            MessageBox.Show("حدد صفاً في الجدول أولاً.", "المخزون", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var (ok, wh) = await TryResolveStockContextWarehouseIdAsync();
        if (!ok) return;
        try
        {
            await _inventoryService.DeleteBranchSalePriceAsync(_currentUser.Id, wh, row.ProductId);
            await RefreshInventoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "المخزون", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }

    /// <summary>Re-applies fonts after <see cref="ApplyUnifiedFont"/> so Main Warehouse keeps its typography (was incorrectly reset to <see cref="UiFontTitle"/>).</summary>

    private static void ApplyUnifiedFont(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is DataGridView)
            {
                ApplyUnifiedFont(child);
                continue;
            }

            var skipFontForFlatButton = child is Button flatBtn && flatBtn.FlatStyle == FlatStyle.Flat;
            var skipMainWarehouseChrome =
                string.Equals(child.Tag as string, MainWarehouseUiLabelTag, StringComparison.Ordinal);

            // Do not assign fonts to Labels here — preserves explicit caption/title hierarchy on each screen.
            if (!skipFontForFlatButton && !skipMainWarehouseChrome &&
                child is TextBox or ComboBox or NumericUpDown or DateTimePicker or CheckBox or TabControl)
            {
                child.Font = UiFont;
            }
            else if (!skipFontForFlatButton && !skipMainWarehouseChrome && child is Button)
            {
                child.Font = UiFont;
            }

            ApplyUnifiedFont(child);
        }
    }

    private async Task AddNewProductAsync()
    {
        if (_newProductCompanyCombo.SelectedValue is not int companyId)
        {
            MessageBox.Show("اختر شركة (أضف الشركات من «الشركات والأصناف» أولاً).", "صنف جديد", MessageBoxButtons.OK,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var name = _newProductName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("أدخل اسم الصنف.", "صنف جديد", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1,
                MsgRtl);
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        if (await db.Products.AnyAsync(x =>
                x.CompanyId == companyId && x.Name == name && x.ProductCategory == _newProductType.Text &&
                x.PackageSize == _newProductPackageSize.Text))
        {
            MessageBox.Show("الصنف موجود مسبقاً لهذه الشركة والنوع والعبوة.", "صنف جديد", MessageBoxButtons.OK,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var product = new Domain.Product
        {
            CompanyId = companyId,
            Name = name,
            ProductCategory = _newProductType.Text,
            PackageSize = _newProductPackageSize.Text,
            UnitPrice = _newProductPrice.Value,
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        if (_newProductOpeningStock.Value > 0)
        {
            var mainWarehouse = await _warehouseService.GetMainAsync();
            if (mainWarehouse is null) throw new InvalidOperationException("لم يُعثر على المستودع الرئيسي.");
            await _inventoryService.AddStockAsync(new PurchaseStockRequest(product.Id, _newProductOpeningStock.Value, _newProductPrice.Value, DateTime.Today, DateTime.Today, mainWarehouse.Id, "رصيد افتتاحي لصنف جديد", _currentUser.Id));
        }

        if (!string.IsNullOrWhiteSpace(_pendingImagePath) && File.Exists(_pendingImagePath))
        {
            var imagesDir = Path.Combine(AppContext.BaseDirectory, "product-images");
            Directory.CreateDirectory(imagesDir);
            var ext = Path.GetExtension(_pendingImagePath);
            var target = Path.Combine(imagesDir, $"product-{product.Id}{ext}");
            File.Copy(_pendingImagePath, target, overwrite: true);
            _productImageMap[product.Id] = target;
            SaveProductImageMap();
        }

        _newProductName.Clear();
        _newProductPrice.Value = 0;
        _newProductOpeningStock.Value = 0;
        _newProductImagePreview.Image = null;
        _pendingImagePath = null;

        await RefreshAllStockViewsAsync();
    }

    private void ChooseNewProductImage()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "ملفات صور|*.jpg;*.jpeg;*.png;*.bmp;*.webp",
            Multiselect = false
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        _pendingImagePath = dialog.FileName;
        _newProductImagePreview.Image = Image.FromFile(dialog.FileName);
    }

}

