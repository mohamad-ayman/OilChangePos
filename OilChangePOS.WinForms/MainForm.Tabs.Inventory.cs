using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm
{
    private TabPage BuildInventoryTab()
    {
        var tab = new TabPage("المخزون") { RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(245, 247, 250) };

        _inventoryTopPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 428,
            Padding = new Padding(14, 12, 14, 8),
            BackColor = Color.FromArgb(245, 247, 250)
        };

        var inventoryStockHint = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            Text =
                "قائمة رف الفرع تخفي الأصناف ذات الرصيد صفر حتى تصل عبر التحويل. فعّل «إظهار أصناف بلا رصيد في الفرع» لعرض كامل الكتالوج. الاستلام الرئيسي يبقى في تبويب المستودع الرئيسي.",
            ForeColor = UiTextSecondary,
            Font = new Font(UiFont, FontStyle.Italic),
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes
        };

        var kpiStrip = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 70,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.FromArgb(245, 247, 250),
            Margin = new Padding(0, 0, 0, 10)
        };
        for (var i = 0; i < 3; i++)
            kpiStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));

        _inventoryTotalItemsLabel.Dock = DockStyle.Fill;
        _inventoryTotalItemsLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _inventoryTotalValueLabel.Dock = DockStyle.Fill;
        _inventoryTotalValueLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
        _inventoryLowStockLabel.Dock = DockStyle.Fill;
        _inventoryLowStockLabel.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);

        kpiStrip.Controls.Add(WrapInBanner(_inventoryTotalItemsLabel, Color.FromArgb(33, 150, 243)), 0, 0);
        kpiStrip.Controls.Add(WrapInBanner(_inventoryTotalValueLabel, Color.FromArgb(39, 174, 96)), 1, 0);
        kpiStrip.Controls.Add(WrapInBanner(_inventoryLowStockLabel, Color.FromArgb(192, 57, 43)), 2, 0);

        var filterCard = BuildCard();
        filterCard.Dock = DockStyle.Top;
        filterCard.Height = 58;
        filterCard.Padding = new Padding(12, 10, 12, 10);

        _inventorySearchBox.TextChanged += (_, _) => ApplyInventoryFilter();
        _inventoryLowStockOnlyCheck.CheckedChanged += (_, _) => ApplyInventoryFilter();
        _inventoryShowZeroStockCheck.CheckedChanged += (_, _) => ApplyInventoryFilter();
        _inventoryLowStockOnlyCheck.Font = UiFontCaption;
        _inventoryShowZeroStockCheck.Font = UiFontCaption;

        var filterRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true
        };

        _inventoryWarehouseCombo.Height = 30;
        _inventoryWarehouseCombo.Font = UiFont;
        _inventoryWarehouseCombo.Width = 180;

        _inventorySearchBox.Height = 30;
        _inventorySearchBox.Font = UiFont;
        _inventorySearchBox.Width = 200;
        _inventorySearchBox.PlaceholderText = "بحث في المخزون...";

        filterRow.Controls.Add(_inventoryResetBranchPriceBtn);
        filterRow.Controls.Add(_inventoryLowStockOnlyCheck);
        filterRow.Controls.Add(_inventoryShowZeroStockCheck);
        filterRow.Controls.Add(_inventorySearchBox);
        filterRow.Controls.Add(new Label { Text = "بحث:", AutoSize = true, Padding = new Padding(0, 6, 6, 0), Font = UiFontCaption, ForeColor = UiTextPrimary });
        filterRow.Controls.Add(_inventoryWarehouseCombo);
        filterRow.Controls.Add(new Label { Text = "الفرع:", AutoSize = true, Padding = new Padding(0, 6, 6, 0), Font = UiFontCaption, ForeColor = UiTextPrimary });
        filterCard.Controls.Add(filterRow);

        var labelFont = UiFontCaption;
        _inventoryAdminGroup = new GroupBox { Dock = DockStyle.Top, Height = 76, Text = "تعيين الكمية (مدير)", Font = UiFontCaption, RightToLeft = RightToLeft.Yes };
        var setQtyButton = BuildButton("تطبيق الكمية", Color.FromArgb(52, 152, 219));
        setQtyButton.Click += async (_, _) => await SetInventoryQuantityAsync();
        var adminLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Padding = new Padding(0, 4, 0, 0) };
        adminLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        adminLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        adminLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        adminLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        adminLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        adminLayout.Controls.Add(new Label { Text = "الصنف", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = labelFont, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 0, 0);
        adminLayout.Controls.Add(_inventoryProductCombo, 1, 0);
        adminLayout.Controls.Add(new Label { Text = "الكمية", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = labelFont, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 2, 0);
        adminLayout.Controls.Add(_inventorySetQty, 3, 0);
        adminLayout.Controls.Add(setQtyButton, 4, 0);
        _inventoryAdminGroup.Controls.Add(adminLayout);

        _inventoryAddProductGroup = new GroupBox { Dock = DockStyle.Top, Height = 136, Text = "إضافة صنف جديد (مدير)", Font = UiFontCaption, Padding = new Padding(8), RightToLeft = RightToLeft.Yes };
        var addLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 14, RowCount = 2 };
        addLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        addLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        addLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _newProductType.Items.Clear();
        _newProductType.Items.AddRange(["Oil", "Filter", "Grease", "Other"]);
        _newProductType.SelectedIndex = 0;
        _newProductPackageSize.Items.Clear();
        _newProductPackageSize.Items.AddRange(["4L", "5L", "20L", "Unit"]);
        _newProductPackageSize.SelectedIndex = 0;
        _chooseImageButton.Click += (_, _) => ChooseNewProductImage();
        var addProductButton = BuildButton("إضافة صنف", Color.FromArgb(39, 174, 96));
        addProductButton.Click += async (_, _) => await AddNewProductAsync();
        foreach (var c in new Control[] { _inventorySearchBox, _inventoryProductCombo, _inventorySetQty, _newProductCompanyCombo, _newProductName, _newProductType, _newProductPackageSize, _newProductPrice, _newProductOpeningStock, _inventoryLowStockOnlyCheck, _inventoryShowZeroStockCheck, _chooseImageButton, addProductButton, setQtyButton, _inventoryWarehouseCombo, _inventoryResetBranchPriceBtn })
        {
            c.Font = UiFont;
        }

        addLayout.Controls.Add(new Label { Text = "الاسم", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = labelFont, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 0, 0);
        addLayout.Controls.Add(_newProductName, 1, 0);
        addLayout.Controls.Add(new Label { Text = "النوع", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = labelFont, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 2, 0);
        addLayout.Controls.Add(_newProductType, 3, 0);
        addLayout.Controls.Add(new Label { Text = "السعر", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = labelFont, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 4, 0);
        addLayout.Controls.Add(_newProductPrice, 5, 0);
        addLayout.Controls.Add(new Label { Text = "العبوة", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = labelFont, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 12, 0);
        addLayout.Controls.Add(_newProductPackageSize, 13, 0);
        addLayout.Controls.Add(new Label { Text = "رصيد افتتاحي", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = labelFont, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 8, 0);
        addLayout.Controls.Add(_newProductOpeningStock, 9, 0);
        addLayout.Controls.Add(new Label { Text = "الشركة", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = labelFont, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 0, 1);
        addLayout.Controls.Add(_newProductCompanyCombo, 1, 1);
        addLayout.SetColumnSpan(_newProductCompanyCombo, 5);

        var imagePanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        imagePanel.Controls.Add(_newProductImagePreview);
        imagePanel.Controls.Add(_chooseImageButton);
        imagePanel.Controls.Add(addProductButton);
        addLayout.Controls.Add(imagePanel, 10, 0);
        _inventoryAddProductGroup.Controls.Add(addLayout);

        _inventoryTopPanel.Controls.Add(inventoryStockHint);
        _inventoryTopPanel.Controls.Add(_inventoryAddProductGroup);
        _inventoryTopPanel.Controls.Add(_inventoryAdminGroup);
        _inventoryTopPanel.Controls.Add(filterCard);
        _inventoryTopPanel.Controls.Add(kpiStrip);

        StyleGrid(_inventoryGrid);
        ConfigureInventoryColumns();
        _inventoryGrid.DataError += (_, e) => e.ThrowException = false;
        _inventoryGrid.CellValidating += InventoryGrid_CellValidating;
        _inventoryGrid.CellEndEdit += InventoryGrid_CellEndEdit;
        _inventoryResetBranchPriceBtn.Click += async (_, _) => await InventoryResetBranchPriceAsync();
        _inventoryGrid.Dock = DockStyle.Fill;
        _inventoryGrid.DataSource = _inventoryBinding;
        _inventoryGrid.BackgroundColor = Color.White;
        _inventoryGrid.RowTemplate.Height = 40;
        _inventoryGrid.Font = new Font("Segoe UI", 11f);
        _inventoryGrid.DefaultCellStyle.Padding = new Padding(10, 4, 10, 4);
        _inventoryGrid.BorderStyle = BorderStyle.None;
        _inventoryGrid.GridColor = Color.FromArgb(232, 234, 237);

        var gridCard = BuildCard();
        gridCard.Dock = DockStyle.Fill;
        gridCard.Padding = new Padding(0);
        gridCard.Controls.Add(_inventoryGrid);

        var gridWrapper = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 8, 14, 14), BackColor = Color.FromArgb(245, 247, 250) };
        gridWrapper.Controls.Add(gridCard);

        tab.Controls.Add(gridWrapper);
        tab.Controls.Add(_inventoryTopPanel);
        var inventoryPageHeader = BuildStandardModuleHeaderCard(
            "المخزون",
            "عرض البحث والتنبيهات وتعديل مستويات إعادة الطلب والكميات (صلاحيات المدير).",
            subtitleItalic: false,
            DockStyle.Top,
            autoSizeHeight: true,
            out _,
            out _);
        inventoryPageHeader.Margin = new Padding(0, 0, 0, 8);
        tab.Controls.Add(inventoryPageHeader);
        return tab;
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
    private async Task<int> GetInventoryTargetWarehouseIdAsync()
    {
        if (TryGetWarehouseIdFromCombo(_inventoryWarehouseCombo, out var id)) return id;
        var branches = await _warehouseService.GetBranchesAsync();
        if (branches.FirstOrDefault() is { } b) return b.Id;
        throw new InvalidOperationException("يرجى اختيار مستودع فرع.");
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
}
