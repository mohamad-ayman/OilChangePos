using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm
{
    private void MainWarehouseFocusQuantity()
    {
        if (_mwQuantity is null || !_mwQuantity.CanFocus) return;
        _mwQuantity.Focus();
        _mwQuantity.Select(0, _mwQuantity.Text.Length);
    }

    private static void AttachWarehouseInputFocusCue(Control c)
    {
        var normalBack = c.BackColor;
        c.Enter += (_, _) => { c.BackColor = Color.FromArgb(255, 255, 240); };
        c.Leave += (_, _) => { c.BackColor = normalBack; };
    }

    private void SetMainWarehouseStockHint(string text)
    {
        if (_mwAvailableStockLabel is null) return;
        void apply() => _mwAvailableStockLabel.Text = string.IsNullOrEmpty(text) ? "—" : text;
        if (InvokeRequired)
            BeginInvoke(apply);
        else
            apply();
    }

    private async Task RefreshMainWarehouseAvailableStockHintAsync()
    {
        if (_mwAvailableStockLabel is null) return;
        if (_mwProductLookup.SelectedItem is not MainWarehouseCatalogRow row)
        {
            SetMainWarehouseStockHint(string.Empty);
            return;
        }

        var main = await _warehouseService.GetMainAsync();
        if (main is null)
        {
            SetMainWarehouseStockHint(string.Empty);
            return;
        }

        var qty = await _inventoryService.GetCurrentStockAsync(row.Id, main.Id);
        SetMainWarehouseStockHint($"المتاح حالياً: {qty:n2}");
    }
    private TabPage BuildMainWarehouseTab()
    {
        var tab = new TabPage("المستودع الرئيسي")
        {
            // Avoid mirroring nested TableLayoutPanels (e.g. form column order) from an implicit RTL chain.
            RightToLeft = RightToLeft.No
        };
        _mainWarehouseTabPage = tab;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(16, 14, 16, 16),
            RightToLeft = RightToLeft.No
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f)); // header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f)); // excel
        // Share remaining height: prioritize the grid; form column scrolls if vertical space is tight.
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40f)); // form + action buttons
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 60f)); // grid + pager

        var headerPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(16, 14, 10, 12),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes
        };
        var roleAr = _currentUser.Role == UserRole.Admin ? "مدير" : "فرع";
        _mwHeaderTitleLabel = new Label
        {
            AutoSize = true,
            Text = "المستودع الرئيسي — المشتريات والدفعات",
            Font = MwFontPageTitle,
            ForeColor = UiTextPrimary,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false,
            Location = new Point(16, 8),
            TextAlign = ContentAlignment.TopRight
        };
        _mwHeaderUserLabel = new Label
        {
            AutoSize = true,
            Text = $"المستخدم: {_currentUser.Username}   ·   {roleAr}",
            Font = MwFontSubtitle,
            ForeColor = UiTextSecondary,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false,
            Location = new Point(16, 34),
            TextAlign = ContentAlignment.TopRight
        };
        headerPanel.Controls.Add(_mwHeaderTitleLabel);
        headerPanel.Controls.Add(_mwHeaderUserLabel);

        var excelCard = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 14),
            BackColor = Color.White,
            Padding = new Padding(14, 10, 14, 10),
            BorderStyle = BorderStyle.FixedSingle
        };
        var excelFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.White
        };
        var importExcelButton = BuildSizedButton("استيراد من اكسل", Color.FromArgb(39, 174, 96), 168, 40);
        var exportExcelButton = BuildSizedButton("تصدير الى اكسل", Color.FromArgb(52, 152, 219), 168, 40);
        ApplyMainWarehousePrimaryButtonTypography(importExcelButton);
        ApplyMainWarehousePrimaryButtonTypography(exportExcelButton);
        importExcelButton.Margin = new Padding(0, 0, 10, 0);
        exportExcelButton.Margin = new Padding(0, 0, 10, 0);
        importExcelButton.TabIndex = 9;
        exportExcelButton.TabIndex = 10;
        importExcelButton.Click += async (_, _) => await ImportMainWarehouseExcelAsync();
        exportExcelButton.Click += async (_, _) => await ExportMainWarehouseExcelAsync();
        excelFlow.Controls.Add(importExcelButton);
        excelFlow.Controls.Add(exportExcelButton);
        excelCard.Controls.Add(excelFlow);
        _mainWarehouseToolTip.SetToolTip(importExcelButton, "استيراد كميات أو أصناف من ملف Excel إلى المستودع الرئيسي.");
        _mainWarehouseToolTip.SetToolTip(exportExcelButton, "تصدير أرصدة الأصناف في المستودع الرئيسي إلى ملف Excel.");

        var middle = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10),
            RightToLeft = RightToLeft.No
        };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224f));

        var leftCard = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 12, 0),
            BackColor = Color.White,
            Padding = new Padding(16, 14, 16, 14),
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.No,
            AutoScroll = true
        };

        var purchaseHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 34,
            ColumnCount = 2,
            RowCount = 1,
            RightToLeft = RightToLeft.No,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 6)
        };
        purchaseHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
        purchaseHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));

        // Caption clearly above the control (Dock Top + AutoSize row); avoids labels sitting beside inputs when row height is tight.
        Label MwLblAbove(string text) =>
            new()
            {
                Text = text,
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = MwFontFieldLabel,
                ForeColor = UiTextPrimary,
                BackColor = Color.White,
                TextAlign = ContentAlignment.TopLeft,
                RightToLeft = RightToLeft.No,
                Margin = new Padding(0, 0, 0, 6),
                Padding = Padding.Empty,
                UseCompatibleTextRendering = false,
                Tag = MainWarehouseUiLabelTag
            };

        ApplyModernWarehouseNumeric(_mwQuantity);
        ApplyModernWarehouseNumeric(_mwPurchasePrice);
        ApplyModernWarehouseNumeric(_mwRetailPrice);
        ApplyModernWarehouseDatePicker(_mwProductionDate);
        ApplyModernWarehouseDatePicker(_mwPurchaseDate);

        _mwProductionDate.Value = DateTime.Today;
        _mwPurchaseDate.Value = DateTime.Today;

        _mwProductLookup.SelectedIndexChanged += async (_, _) =>
        {
            if (_syncingMainWarehouseProductCombo) return;
            SyncMainWarehouseRetailFromSelectedCatalog();
            await RefreshMainWarehouseAvailableStockHintAsync();
            await SyncMainWarehouseBranchPricePanelAsync();
            SyncMainWarehouseCommandStates();
        };

        void MainWarehouseAddInputsChanged(object? s, EventArgs e) => SyncMainWarehouseCommandStates();
        _mwQuantity.TextChanged += MainWarehouseAddInputsChanged;
        _mwQuantity.ValueChanged += MainWarehouseAddInputsChanged;
        _mwPurchasePrice.TextChanged += MainWarehouseAddInputsChanged;
        _mwPurchasePrice.ValueChanged += MainWarehouseAddInputsChanged;
        _mwRetailPrice.TextChanged += MainWarehouseAddInputsChanged;
        _mwRetailPrice.ValueChanged += MainWarehouseAddInputsChanged;
        _mwProductionDate.ValueChanged += MainWarehouseAddInputsChanged;
        _mwPurchaseDate.ValueChanged += MainWarehouseAddInputsChanged;

        var lblSectionInputs = new Label
        {
            Text = "بيانات الشراء",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = MwFontSection,
            ForeColor = UiTextPrimary,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            RightToLeft = RightToLeft.No,
            Padding = new Padding(0, 2, 0, 2),
            UseCompatibleTextRendering = false,
            Tag = MainWarehouseUiLabelTag
        };
        _mwAvailableStockLabel = new Label
        {
            Text = "—",
            Dock = DockStyle.Fill,
            AutoSize = false,
            Font = MwFontSubtitle,
            ForeColor = UiTextSecondary,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            RightToLeft = RightToLeft.No,
            Padding = new Padding(0, 2, 8, 2),
            UseCompatibleTextRendering = false,
            Tag = MainWarehouseUiLabelTag
        };
        purchaseHeader.Controls.Add(lblSectionInputs, 0, 0);
        purchaseHeader.Controls.Add(_mwAvailableStockLabel, 1, 0);

        var productPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 12),
            BackColor = Color.White
        };
        productPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        productPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
        productPanel.Controls.Add(MwLblAbove("الصنف (من الكتالوج)"), 0, 0);
        var skuHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            RightToLeft = RightToLeft.No,
            Padding = new Padding(0, 2, 0, 0)
        };
        _mwProductLookup.Font = MwFontInput;
        _mwProductLookup.ItemHeight = 26;
        _mwProductLookup.Dock = DockStyle.Fill;
        _mwProductLookup.TabIndex = 0;
        _mwProductLookup.MinimumSize = new Size(80, 34);
        _mwProductLookup.MaximumSize = new Size(0, 38);
        skuHost.Controls.Add(_mwProductLookup);
        productPanel.Controls.Add(skuHost, 0, 1);

        var qtyCostSaleRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 70,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 12),
            BackColor = Color.White
        };
        qtyCostSaleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        qtyCostSaleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        qtyCostSaleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        qtyCostSaleRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        qtyCostSaleRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        qtyCostSaleRow.Controls.Add(MwLblAbove("كمية الشراء"), 0, 0);
        qtyCostSaleRow.Controls.Add(MwLblAbove("سعر الشراء (تكلفة)"), 1, 0);
        qtyCostSaleRow.Controls.Add(MwLblAbove("سعر البيع (الصرف / نقطة البيع)"), 2, 0);
        _mwQuantity.Dock = DockStyle.Fill;
        _mwPurchasePrice.Dock = DockStyle.Fill;
        _mwRetailPrice.Dock = DockStyle.Fill;
        _mwQuantity.Margin = new Padding(0, 0, 10, 0);
        _mwPurchasePrice.Margin = new Padding(0, 0, 10, 0);
        _mwRetailPrice.Margin = new Padding(0, 0, 0, 0);
        _mwQuantity.TabIndex = 1;
        _mwPurchasePrice.TabIndex = 2;
        _mwRetailPrice.TabIndex = 3;
        qtyCostSaleRow.Controls.Add(_mwQuantity, 0, 1);
        qtyCostSaleRow.Controls.Add(_mwPurchasePrice, 1, 1);
        qtyCostSaleRow.Controls.Add(_mwRetailPrice, 2, 1);

        var dateRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 70,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 12),
            BackColor = Color.White
        };
        dateRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        dateRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        dateRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        dateRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        dateRow.Controls.Add(MwLblAbove("تاريخ الإنتاج"), 0, 0);
        dateRow.Controls.Add(MwLblAbove("تاريخ الشراء"), 1, 0);
        _mwProductionDate.Dock = DockStyle.Fill;
        _mwPurchaseDate.Dock = DockStyle.Fill;
        _mwProductionDate.Margin = new Padding(0, 0, 10, 0);
        _mwPurchaseDate.Margin = new Padding(0, 0, 0, 0);
        _mwProductionDate.TabIndex = 4;
        _mwPurchaseDate.TabIndex = 5;
        dateRow.Controls.Add(_mwProductionDate, 0, 1);
        dateRow.Controls.Add(_mwPurchaseDate, 1, 1);

        var mwFormDivider = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Color.FromArgb(230, 230, 230),
            Margin = new Padding(0, 4, 0, 14)
        };

        ApplyModernWarehouseNumeric(_mwBranchRetailOverride);
        _mwBranchPriceWarehouseCombo.Font = MwFontInput;
        _mwBranchRetailOverride.Dock = DockStyle.Fill;
        _mwBranchPriceWarehouseCombo.Dock = DockStyle.Fill;
        _mwBranchPriceSaveBtn = BuildSizedButton("حفظ سعر الفرع", Color.FromArgb(39, 174, 96), 128, 38);
        _mwBranchPriceResetBtn = BuildSizedButton("إعادة للسعر المرجعي", Color.FromArgb(108, 117, 125), 158, 38);
        ApplyMainWarehousePrimaryButtonTypography(_mwBranchPriceSaveBtn);
        ApplyMainWarehousePrimaryButtonTypography(_mwBranchPriceResetBtn);
        _mwBranchPriceSaveBtn.Margin = new Padding(8, 0, 0, 0);
        _mwBranchPriceResetBtn.Margin = new Padding(8, 0, 0, 0);
        _mwBranchPriceSaveBtn.Click += async (_, _) => await SaveMainWarehouseBranchPriceAsync();
        _mwBranchPriceResetBtn.Click += async (_, _) => await ResetMainWarehouseBranchPriceAsync();

        var branchPriceGroup = new GroupBox
        {
            Dock = DockStyle.Top,
            Height = 124,
            Text = "سعر البيع في الفرع (اختياري — يتجاوز السعر المرجعي)",
            Font = MwFontSection,
            ForeColor = UiTextSecondary,
            Padding = new Padding(12, 10, 12, 10),
            BackColor = Color.White,
            RightToLeft = RightToLeft.No,
            Tag = MainWarehouseUiLabelTag
        };
        var branchInner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = Padding.Empty,
            BackColor = Color.White
        };
        branchInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
        branchInner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
        branchInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132f));
        branchInner.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 172f));
        branchInner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        branchInner.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        branchInner.Controls.Add(MwLblAbove("الفرع"), 0, 0);
        branchInner.Controls.Add(MwLblAbove("السعر في الفرع"), 1, 0);
        var branchHdrSpacer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        branchInner.Controls.Add(branchHdrSpacer, 2, 0);
        branchInner.SetColumnSpan(branchHdrSpacer, 2);
        branchInner.Controls.Add(_mwBranchPriceWarehouseCombo, 0, 1);
        branchInner.Controls.Add(_mwBranchRetailOverride, 1, 1);
        var branchBtnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(6, 2, 0, 0),
            BackColor = Color.White
        };
        branchBtnFlow.Controls.Add(_mwBranchPriceSaveBtn);
        branchBtnFlow.Controls.Add(_mwBranchPriceResetBtn);
        branchInner.Controls.Add(branchBtnFlow, 2, 1);
        branchInner.SetColumnSpan(branchBtnFlow, 2);
        branchPriceGroup.Controls.Add(branchInner);

        leftCard.Controls.Add(branchPriceGroup);
        leftCard.Controls.Add(mwFormDivider);
        leftCard.Controls.Add(dateRow);
        leftCard.Controls.Add(qtyCostSaleRow);
        leftCard.Controls.Add(productPanel);
        leftCard.Controls.Add(purchaseHeader);

        var rightCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12, 8, 12, 8),
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes,
            AutoScroll = true
        };
        var lblSectionOps = new Label
        {
            Text = "العمليات",
            Dock = DockStyle.Top,
            Height = 28,
            AutoSize = false,
            Font = MwFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 4, 0, 6),
            BackColor = Color.White,
            RightToLeft = RightToLeft.No,
            UseCompatibleTextRendering = false,
            Tag = MainWarehouseUiLabelTag
        };
        var btnCol = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(2, 4, 2, 4),
            BackColor = Color.White
        };
        const int mwBtnW = 198;
        const int mwBtnH = 42;
        _mwCmdAdd = BuildSizedButton("إضافة دفعة", Color.FromArgb(39, 174, 96), mwBtnW, mwBtnH);
        _mwCmdUpdate = BuildSizedButton("حفظ التعديل", Color.FromArgb(41, 128, 185), mwBtnW, mwBtnH);
        _mwCmdDelete = BuildSizedButton("حذف الدفعة", Color.FromArgb(192, 57, 43), mwBtnW, mwBtnH);
        _mwCmdClear = BuildSizedButton("مسح الحقول", Color.FromArgb(108, 117, 125), mwBtnW, mwBtnH);
        ApplyMainWarehousePrimaryButtonTypography(_mwCmdAdd);
        ApplyMainWarehousePrimaryButtonTypography(_mwCmdUpdate);
        ApplyMainWarehousePrimaryButtonTypography(_mwCmdDelete);
        ApplyMainWarehousePrimaryButtonTypography(_mwCmdClear);
        _mwCmdAdd.Margin = new Padding(0, 0, 0, 8);
        _mwCmdUpdate.Margin = new Padding(0, 0, 0, 8);
        _mwCmdDelete.Margin = new Padding(0, 0, 0, 8);
        _mwCmdClear.Margin = new Padding(0, 0, 0, 0);
        _mwCmdAdd.TabIndex = 5;
        _mwCmdUpdate.TabIndex = 6;
        _mwCmdDelete.TabIndex = 7;
        _mwCmdClear.TabIndex = 8;
        _mwCmdAdd.Click += async (_, _) => await AddMainWarehouseManualAsync();
        _mwCmdUpdate.Click += async (_, _) => await UpdateMainWarehouseManualAsync();
        _mwCmdDelete.Click += async (_, _) => await DeleteMainWarehouseManualAsync();
        _mwCmdClear.Click += (_, _) => ClearMainWarehouseForm();
        btnCol.Controls.Add(_mwCmdAdd);
        btnCol.Controls.Add(_mwCmdUpdate);
        btnCol.Controls.Add(_mwCmdDelete);
        btnCol.Controls.Add(_mwCmdClear);
        _mainWarehouseToolTip.SetToolTip(_mwCmdAdd, "تسجيل كمية شراء جديدة للصنف المختار (دفعة جديدة في المستودع الرئيسي).");
        _mainWarehouseToolTip.SetToolTip(_mwCmdUpdate, "تحديث الكمية أو السعر أو التواريخ لسطر الشراء المحدد في الجدول.");
        _mainWarehouseToolTip.SetToolTip(_mwCmdDelete, "حذف سجل الشراء المحدد في الجدول.");
        _mainWarehouseToolTip.SetToolTip(_mwCmdClear, "إفراغ الحقول دون حفظ. لا يغيّر بيانات الجدول.");
        _mainWarehouseToolTip.SetToolTip(_mwProductLookup, "اختر الصنف من الكتالوج؛ يظهر الاسم والشركة والنوع والعبوة في سطر واحد.");
        _mainWarehouseToolTip.SetToolTip(_mwBranchPriceSaveBtn, "حفظ سعر البيع لهذا الصنف في الفرع المختار (يتجاوز السعر المرجعي في نقطة البيع).");
        _mainWarehouseToolTip.SetToolTip(_mwBranchPriceResetBtn, "إزالة تخصيص الفرع والعودة لسعر البيع المرجعي للصنف.");
        rightCard.Controls.Add(btnCol);
        rightCard.Controls.Add(lblSectionOps);

        middle.Controls.Add(leftCard, 0, 0);
        middle.Controls.Add(rightCard, 1, 0);

        ConfigureMainWarehouseColumns();
        _mainWarehouseGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        StyleMainWarehouseGrid();
        _mainWarehouseGrid.AllowUserToAddRows = false;
        _mainWarehouseGrid.Dock = DockStyle.Fill;
        _mainWarehouseGrid.Margin = new Padding(0, 2, 0, 0);
        _mainWarehouseGrid.SelectionChanged += (_, _) => LoadSelectedMainWarehouseRow();

        var gridCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12, 10, 12, 10),
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes
        };
        var lblSectionGrid = new Label
        {
            Text = "السجل",
            Dock = DockStyle.Top,
            Height = 28,
            AutoSize = false,
            Font = MwFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 4, 0, 6),
            BackColor = Color.White,
            RightToLeft = RightToLeft.No,
            UseCompatibleTextRendering = false,
            Tag = MainWarehouseUiLabelTag
        };
        var pagerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(4, 4, 4, 4),
            Margin = Padding.Empty,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };
        _mainWarehousePagerInfo = new Label
        {
            AutoSize = true,
            Font = MwFontInput,
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "—",
            Margin = new Padding(8, 6, 8, 2),
            RightToLeft = RightToLeft.No,
            Tag = MainWarehouseUiLabelTag
        };
        var lblPageSize = new Label
        {
            Text = "عدد الأسطر:",
            AutoSize = true,
            Font = MwFontFieldLabel,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 8, 4, 2),
            RightToLeft = RightToLeft.No,
            Tag = MainWarehouseUiLabelTag
        };
        _mainWarehousePageSizeCombo = new ComboBox
        {
            Width = 72,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = MwFontInput,
            Margin = new Padding(0, 4, 8, 2),
            RightToLeft = RightToLeft.Yes,
            Tag = MainWarehouseUiLabelTag
        };
        _mainWarehousePageSizeCombo.Items.AddRange([25, 50, 100]);
        _mainWarehousePageSizeCombo.SelectedIndex = 0;
        _mainWarehousePageSizeCombo.SelectedIndexChanged += (_, _) =>
        {
            _mainWarehousePageIndex = 0;
            BindMainWarehouseGridPage(resetToFirstPage: false);
        };
        _mainWarehousePagerNext = new Button
        {
            Text = "التالي ←",
            AutoSize = true,
            Font = MwFontInput,
            Margin = new Padding(4, 4, 4, 2),
            RightToLeft = RightToLeft.Yes,
            Tag = MainWarehouseUiLabelTag
        };
        _mainWarehousePagerPrev = new Button
        {
            Text = "→ السابق",
            AutoSize = true,
            Font = MwFontInput,
            Margin = new Padding(4, 4, 4, 2),
            RightToLeft = RightToLeft.Yes,
            Tag = MainWarehouseUiLabelTag
        };
        _mainWarehousePagerNext.Click += (_, _) =>
        {
            _mainWarehousePageIndex++;
            BindMainWarehouseGridPage(resetToFirstPage: false);
        };
        _mainWarehousePagerPrev.Click += (_, _) =>
        {
            _mainWarehousePageIndex = Math.Max(0, _mainWarehousePageIndex - 1);
            BindMainWarehouseGridPage(resetToFirstPage: false);
        };
        pagerPanel.Controls.Add(_mainWarehousePagerPrev);
        pagerPanel.Controls.Add(_mainWarehousePagerNext);
        pagerPanel.Controls.Add(_mainWarehousePageSizeCombo);
        pagerPanel.Controls.Add(lblPageSize);
        pagerPanel.Controls.Add(_mainWarehousePagerInfo);
        gridCard.Controls.Add(_mainWarehouseGrid);
        gridCard.Controls.Add(pagerPanel);
        gridCard.Controls.Add(lblSectionGrid);

        AttachWarehouseInputFocusCue(_mwQuantity);
        AttachWarehouseInputFocusCue(_mwPurchasePrice);
        AttachWarehouseInputFocusCue(_mwRetailPrice);
        AttachWarehouseInputFocusCue(_mwProductionDate);
        AttachWarehouseInputFocusCue(_mwPurchaseDate);
        AttachWarehouseInputFocusCue(_mwProductLookup);
        AttachWarehouseInputFocusCue(_mwBranchPriceWarehouseCombo);
        AttachWarehouseInputFocusCue(_mwBranchRetailOverride);

        root.Controls.Add(headerPanel, 0, 0);
        root.Controls.Add(excelCard, 0, 1);
        root.Controls.Add(middle, 0, 2);
        root.Controls.Add(gridCard, 0, 3);

        tab.Controls.Add(root);
        SyncMainWarehouseCommandStates();
        return tab;
    }

    private void StyleMainWarehouseGrid()
    {
        StyleGrid(_mainWarehouseGrid);
        _mainWarehouseGrid.RightToLeft = RightToLeft.Yes;
        _mainWarehouseGrid.Font = MwFontGridCell;
        _mainWarehouseGrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _mainWarehouseGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        _mainWarehouseGrid.ColumnHeadersHeight = 48;
        _mainWarehouseGrid.RowTemplate.Height = 44;
        _mainWarehouseGrid.DefaultCellStyle.Font = MwFontGridCell;
        _mainWarehouseGrid.DefaultCellStyle.ForeColor = UiTextPrimary;
        _mainWarehouseGrid.DefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
        _mainWarehouseGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 248, 251);
        _mainWarehouseGrid.AlternatingRowsDefaultCellStyle.Font = MwFontGridCell;
        _mainWarehouseGrid.AlternatingRowsDefaultCellStyle.ForeColor = UiTextPrimary;
        _mainWarehouseGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 230, 255);
        _mainWarehouseGrid.DefaultCellStyle.SelectionForeColor = Color.Black;
        var headerBg = Color.FromArgb(45, 62, 80);
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.BackColor = headerBg;
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.Font = MwFontGridHeader;
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = headerBg;
        foreach (DataGridViewColumn col in _mainWarehouseGrid.Columns)
        {
            if (col.DataPropertyName == nameof(MainWarehouseRow.BatchLabel))
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }
    }

    private void SyncMainWarehouseCommandStates()
    {
        if (_mwCmdAdd is not null)
            _mwCmdAdd.Enabled = CanCommitMainWarehouseAdd();
        if (_mwCmdUpdate is null || _mwCmdDelete is null)
            return;
        var canEditDelete = _selectedMainPurchaseId.HasValue;
        _mwCmdUpdate.Enabled = canEditDelete;
        _mwCmdDelete.Enabled = canEditDelete;
    }

    private bool CanCommitMainWarehouseAdd() =>
        TryGetMainWarehouseAddInputs(out _, out _, out _, out _, out _, out _, out _);

    private bool TryGetMainWarehouseAddInputs(
        out MainWarehouseCatalogRow? catalog,
        out decimal quantity,
        out decimal purchasePrice,
        out decimal retailUnitPrice,
        out DateTime productionDate,
        out DateTime purchaseDate,
        out string? errorMessage)
    {
        catalog = null;
        quantity = 0;
        purchasePrice = 0;
        retailUnitPrice = 0;
        productionDate = default;
        purchaseDate = default;
        errorMessage = null;

        if (_mwProductLookup.SelectedItem is not MainWarehouseCatalogRow c)
        {
            errorMessage = "اختر صنفاً من القائمة (أضف أصنافاً من تبويب المخزون أولاً).";
            return false;
        }

        catalog = c;
        quantity = ReadCommittedNumericUpDown(_mwQuantity);
        purchasePrice = ReadCommittedNumericUpDown(_mwPurchasePrice);
        retailUnitPrice = ReadCommittedNumericUpDown(_mwRetailPrice);
        productionDate = _mwProductionDate.Value.Date;
        purchaseDate = _mwPurchaseDate.Value.Date;

        if (quantity <= 0)
        {
            errorMessage = "يرجى إدخال كمية أكبر من صفر.";
            return false;
        }

        if (purchasePrice < 0)
        {
            errorMessage = "سعر الشراء لا يمكن أن يكون سالباً.";
            return false;
        }

        if (retailUnitPrice < 0)
        {
            errorMessage = "سعر البيع لا يمكن أن يكون سالباً.";
            return false;
        }

        return true;
    }

    private void SyncMainWarehouseRetailFromSelectedCatalog()
    {
        if (_suppressMainWarehouseRowLoad) return;
        if (_mwProductLookup.SelectedItem is MainWarehouseCatalogRow row)
            _mwRetailPrice.Value = Math.Clamp(row.RetailUnitPrice, _mwRetailPrice.Minimum, _mwRetailPrice.Maximum);
    }

    private int GetMainWarehousePageSize()
    {
        if (_mainWarehousePageSizeCombo?.SelectedItem is int n && n > 0)
            return n;
        return 25;
    }

    private void BindMainWarehouseGridPage(bool resetToFirstPage)
    {
        if (resetToFirstPage)
            _mainWarehousePageIndex = 0;

        var all = _mainWarehouseAllRows;
        var pageSize = GetMainWarehousePageSize();
        var total = all.Count;
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);
        if (_mainWarehousePageIndex >= totalPages)
            _mainWarehousePageIndex = Math.Max(0, totalPages - 1);

        var skip = _mainWarehousePageIndex * pageSize;
        var pageRows = all.Skip(skip).Take(pageSize).ToList();

        _mainWarehouseGridRefreshing = true;
        try
        {
            _mainWarehouseGrid.DataSource = pageRows;
            if (pageRows.Count > 0)
            {
                _mainWarehouseGrid.ClearSelection();
                var first = _mainWarehouseGrid.Rows[0];
                first.Selected = true;
                _mainWarehouseGrid.CurrentCell = first.Cells[0];
            }
        }
        finally
        {
            _mainWarehouseGridRefreshing = false;
        }

        if (_mainWarehousePagerInfo is not null)
        {
            _mainWarehousePagerInfo.Text = total == 0
                ? "لا توجد أسطر"
                : $"الصفحة {_mainWarehousePageIndex + 1} من {totalPages} — {total} سطراً";
        }

        if (_mainWarehousePagerPrev is not null)
            _mainWarehousePagerPrev.Enabled = _mainWarehousePageIndex > 0 && total > 0;
        if (_mainWarehousePagerNext is not null)
            _mainWarehousePagerNext.Enabled = _mainWarehousePageIndex < totalPages - 1 && total > 0;

        if (pageRows.Count == 0)
            ClearMainWarehouseForm();
        else
            LoadSelectedMainWarehouseRow();
    }

    private static void ApplyModernWarehouseNumeric(NumericUpDown n)
    {
        n.Font = MwFontInput;
        n.Height = 36;
        n.MinimumSize = new Size(n.MinimumSize.Width, 36);
        n.BorderStyle = BorderStyle.FixedSingle;
        n.BackColor = Color.White;
        n.ForeColor = UiTextPrimary;
        n.TextAlign = HorizontalAlignment.Right;
        n.InterceptArrowKeys = true;
    }

    private static void ApplyModernWarehouseDatePicker(DateTimePicker d)
    {
        d.Font = MwFontInput;
        d.Format = DateTimePickerFormat.Custom;
        d.CustomFormat = "yyyy-MM-dd";
        d.Height = 36;
        d.MinimumSize = new Size(d.MinimumSize.Width, 36);
        d.ShowUpDown = false;
        d.CalendarForeColor = UiTextPrimary;
        d.CalendarMonthBackground = Color.White;
        d.CalendarTitleBackColor = Color.FromArgb(52, 152, 219);
        d.CalendarTitleForeColor = Color.White;
    }

    private void ConfigureMainWarehouseColumns()
    {
        _mainWarehouseGrid.AutoGenerateColumns = false;
        _mainWarehouseGrid.Columns.Clear();
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.CompanyName),
            HeaderText = "الشركة",
            FillWeight = 10,
            MinimumWidth = 90,
            ReadOnly = true
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.BatchLabel),
            HeaderText = "دفعة الشراء",
            FillWeight = 7,
            MinimumWidth = 60,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, NullValue = "" }
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.InventoryName),
            HeaderText = "اسم المنتج",
            FillWeight = 14,
            MinimumWidth = 110,
            ReadOnly = true
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.ProductionDate),
            HeaderText = "تاريخ الانتاج",
            FillWeight = 12,
            MinimumWidth = 110,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" }
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.PurchasedQuantity),
            HeaderText = "كمية الشراء",
            FillWeight = 10,
            MinimumWidth = 92,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.OnHandAtMain),
            HeaderText = "المتبقي بالرئيسي",
            FillWeight = 10,
            MinimumWidth = 100,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Format = "N3",
                Alignment = DataGridViewContentAlignment.MiddleRight,
                NullValue = ""
            }
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.PurchasePrice),
            HeaderText = "سعر الشراء (تكلفة)",
            FillWeight = 11,
            MinimumWidth = 100,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.RetailUnitPrice),
            HeaderText = "سعر البيع (نقطة البيع)",
            FillWeight = 11,
            MinimumWidth = 100,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.PurchaseDate),
            HeaderText = "تاريخ الشراء",
            FillWeight = 12,
            MinimumWidth = 110,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" }
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.PackageSize),
            HeaderText = "نوع العبوه كام لتر",
            FillWeight = 8,
            MinimumWidth = 72,
            ReadOnly = true
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.ProductCategory),
            HeaderText = "نوع المنتج",
            FillWeight = 8,
            MinimumWidth = 72,
            ReadOnly = true
        });
    }
    private async Task RefreshMainWarehouseGridAsync()
    {
        await LoadMainWarehouseCatalogAsync();

        var main = await _warehouseService.GetMainAsync();
        if (main is null)
        {
            _mainWarehouseAllRows = [];
            BindMainWarehouseGridPage(resetToFirstPage: true);
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var purchases = await db.Purchases
            .AsNoTracking()
            .Include(x => x.Product)
            .ThenInclude(p => p!.Company)
            .Where(x => x.WarehouseId == main.Id)
            .OrderByDescending(x => x.PurchaseDate)
            .ToListAsync();
        var rows = new List<MainWarehouseRow>();
        // Every product that has any Purchase row must skip the legacy fallback row (even if Product navigation failed to load).
        var productIdsWithPurchaseLine = purchases.Select(p => p.ProductId).ToHashSet();
        var onHandByProduct = new Dictionary<int, decimal>();
        foreach (var purchase in purchases)
        {
            if (purchase.Product is null) continue;
            if (!onHandByProduct.ContainsKey(purchase.ProductId))
                onHandByProduct[purchase.ProductId] = await _inventoryService.GetCurrentStockAsync(purchase.ProductId, main.Id);
            var onHand = onHandByProduct[purchase.ProductId];
            rows.Add(new MainWarehouseRow
            {
                ProductId = purchase.ProductId,
                PurchaseId = purchase.Id,
                CompanyName = purchase.Product.Company?.Name ?? string.Empty,
                InventoryName = purchase.Product.Name,
                ProductionDate = purchase.ProductionDate,
                PurchasedQuantity = purchase.Quantity,
                OnHandAtMain = onHand,
                PurchasePrice = purchase.PurchasePrice,
                RetailUnitPrice = purchase.Product.UnitPrice,
                PurchaseDate = purchase.PurchaseDate,
                PackageSize = purchase.Product.PackageSize,
                ProductCategory = purchase.Product.ProductCategory
            });
        }

        // Opening/legacy stock may exist only as StockMovements (no Purchases row). Those rows used to
        // disappear after the first purchase because the fallback ran only when Purchases was empty.
        var products = await db.Products
            .AsNoTracking()
            .Include(x => x.Company)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync();

        foreach (var p in products)
        {
            if (productIdsWithPurchaseLine.Contains(p.Id))
                continue;

            var qty = await _inventoryService.GetCurrentStockAsync(p.Id, main.Id);
            if (qty <= 0)
                continue;

            rows.Add(new MainWarehouseRow
            {
                ProductId = p.Id,
                PurchaseId = null,
                CompanyName = p.Company?.Name ?? string.Empty,
                InventoryName = p.Name,
                ProductionDate = DateTime.Today,
                PurchasedQuantity = qty,
                OnHandAtMain = qty,
                PurchasePrice = 0,
                RetailUnitPrice = p.UnitPrice,
                PurchaseDate = DateTime.Today,
                PackageSize = p.PackageSize,
                ProductCategory = p.ProductCategory
            });
        }

        rows.Sort(static (a, b) =>
        {
            var ad = a.PurchaseDate;
            var bd = b.PurchaseDate;
            var cmp = bd.CompareTo(ad);
            if (cmp != 0)
                return cmp;
            var idA = a.PurchaseId ?? 0;
            var idB = b.PurchaseId ?? 0;
            var idCmp = idB.CompareTo(idA);
            if (idCmp != 0)
                return idCmp;
            return string.Compare(a.InventoryName, b.InventoryName, StringComparison.OrdinalIgnoreCase);
        });

        foreach (var g in rows.Where(r => r.PurchaseId.HasValue).GroupBy(r => r.ProductId))
        {
            var ordered = g.OrderBy(r => r.PurchaseDate).ThenBy(r => r.PurchaseId!.Value).ToList();
            var total = ordered.Count;
            for (var i = 0; i < total; i++)
            {
                ordered[i].BatchNumber = i + 1;
                ordered[i].BatchTotal = total;
                ordered[i].BatchLabel = total > 1 ? $"{i + 1}/{total}" : string.Empty;
            }
        }

        // One "on hand" total per product: same SKU can have multiple purchase lines; show the total only on the newest line.
        foreach (var group in rows.Where(r => r.ProductId != 0).GroupBy(r => r.ProductId))
        {
            var keeper = group
                .OrderByDescending(r => r.PurchaseDate)
                .ThenByDescending(r => r.PurchaseId ?? int.MinValue)
                .First();
            foreach (var r in group)
            {
                if (!ReferenceEquals(r, keeper))
                    r.OnHandAtMain = null;
            }
        }

        _mainWarehouseAllRows = rows;
        BindMainWarehouseGridPage(resetToFirstPage: true);
    }

    private async Task ImportMainWarehouseExcelAsync()
    {
        var main = await _warehouseService.GetMainAsync();
        if (main is null)
        {
            MessageBox.Show("لم يُعثر على المستودع الرئيسي.", "المستودع الرئيسي", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Filter = "ملفات Excel|*.xlsx;*.xlsm",
            Multiselect = false
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var imported = 0;
        using var workbook = new XLWorkbook(dialog.FileName);
        var ws = workbook.Worksheets.First();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            var company = ws.Cell(row, 1).GetString().Trim();
            var name = ws.Cell(row, 2).GetString().Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var category = ws.Cell(row, 3).GetString().Trim();
            var pack = ws.Cell(row, 4).GetString().Trim();
            var qty = ws.Cell(row, 5).GetValue<decimal>();
            var purchasePrice = ws.Cell(row, 6).GetValue<decimal>();
            var productionDate = ws.Cell(row, 7).GetDateTime();
            var purchaseDate = ws.Cell(row, 8).GetDateTime();
            decimal? retailOverride = null;
            try
            {
                var retailCell = ws.Cell(row, 9);
                if (!retailCell.IsEmpty() && retailCell.TryGetValue<decimal>(out var rv))
                    retailOverride = rv;
            }
            catch
            {
                /* column 9 optional for older templates */
            }

            if (qty <= 0) continue;
            if (string.IsNullOrWhiteSpace(category)) category = "Oil";
            if (string.IsNullOrWhiteSpace(pack)) pack = "Unit";

            var coName = string.IsNullOrWhiteSpace(company) ? "عام" : company.Trim();
            var companyRow = await db.Companies.FirstOrDefaultAsync(c => c.Name == coName);
            if (companyRow is null)
            {
                companyRow = new Domain.Company { Name = coName, IsActive = true };
                db.Companies.Add(companyRow);
                await db.SaveChangesAsync();
            }

            var product = await db.Products.FirstOrDefaultAsync(x =>
                x.CompanyId == companyRow.Id && x.Name == name && x.ProductCategory == category && x.PackageSize == pack);
            if (product is null)
            {
                var initialRetail = retailOverride ?? purchasePrice;
                if (initialRetail < 0) initialRetail = 0;
                product = new Domain.Product
                {
                    CompanyId = companyRow.Id,
                    Name = name,
                    ProductCategory = category,
                    PackageSize = pack,
                    UnitPrice = initialRetail,
                    IsActive = true
                };
                db.Products.Add(product);
                await db.SaveChangesAsync();
            }
            else if (retailOverride.HasValue)
            {
                product.UnitPrice = retailOverride.Value;
                await db.SaveChangesAsync();
            }

            await _inventoryService.AddStockAsync(new PurchaseStockRequest(
                product.Id,
                qty,
                purchasePrice,
                productionDate,
                purchaseDate,
                main.Id,
                "استيراد من Excel",
                _currentUser.Id));
            imported++;
        }

        MessageBox.Show($"تم استيراد {imported} سطراً إلى المستودع الرئيسي.", "اكتمل الاستيراد", MessageBoxButtons.OK,
            MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
        await RefreshAllStockViewsAsync();
    }

    private async Task ExportMainWarehouseExcelAsync()
    {
        var main = await _warehouseService.GetMainAsync();
        if (main is null)
        {
            MessageBox.Show("لم يُعثر على المستودع الرئيسي.", "المستودع الرئيسي", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "ملفات Excel|*.xlsx",
            FileName = $"main-warehouse-{DateTime.Now:yyyyMMdd-HHmm}.xlsx"
        };
        if (dialog.ShowDialog() != DialogResult.OK) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var products = await db.Products.AsNoTracking().Include(p => p.Company).Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync();
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("المستودع الرئيسي");
        ws.Cell(1, 1).Value = "الشركة";
        ws.Cell(1, 2).Value = "اسم الصنف";
        ws.Cell(1, 3).Value = "التصنيف";
        ws.Cell(1, 4).Value = "العبوة";
        ws.Cell(1, 5).Value = "الكمية";
        ws.Cell(1, 6).Value = "تكلفة الشراء (وحدة)";
        ws.Cell(1, 7).Value = "تاريخ الإنتاج";
        ws.Cell(1, 8).Value = "تاريخ الشراء";
        ws.Cell(1, 9).Value = "سعر البيع (نقطة البيع)";
        ws.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var p in products)
        {
            var qty = await _inventoryService.GetCurrentStockAsync(p.Id, main.Id);
            ws.Cell(r, 1).Value = p.Company?.Name ?? string.Empty;
            ws.Cell(r, 2).Value = p.Name;
            ws.Cell(r, 3).Value = p.ProductCategory;
            ws.Cell(r, 4).Value = p.PackageSize;
            ws.Cell(r, 5).Value = qty;
            ws.Cell(r, 6).Value = 0;
            ws.Cell(r, 7).Value = DateTime.Today;
            ws.Cell(r, 8).Value = DateTime.Today;
            ws.Cell(r, 9).Value = p.UnitPrice;
            r++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(dialog.FileName);
        MessageBox.Show("تم تصدير مخزون المستودع الرئيسي.", "اكتمل التصدير", MessageBoxButtons.OK, MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1, MsgRtl);
    }

    private async Task AddMainWarehouseManualAsync()
    {
        if (!TryGetMainWarehouseAddInputs(out var catalog, out var quantity, out var purchasePrice, out var retailUnitPrice, out var productionDate, out var purchaseDate, out var err))
        {
            MessageBox.Show(err ?? "أكمل الحقول المطلوبة قبل الإضافة.", "المستودع الرئيسي", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var main = await _warehouseService.GetMainAsync();
        if (main is null) return;

        await _inventoryService.AddStockAsync(new PurchaseStockRequest(
            catalog!.Id,
            quantity,
            purchasePrice,
            productionDate,
            purchaseDate,
            main.Id,
            "إضافة يدوية من شاشة المستودع الرئيسي",
            _currentUser.Id));

        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            var productEntity = await db.Products.FirstAsync(x => x.Id == catalog.Id);
            productEntity.UnitPrice = retailUnitPrice;
            await db.SaveChangesAsync();
        }

        await RefreshAllStockViewsAsync();
        PrepareMainWarehouseFormForNextBatch();
        await RefreshMainWarehouseAvailableStockHintAsync();
        BeginInvoke(MainWarehouseFocusQuantity);
    }

    private async Task UpdateMainWarehouseManualAsync()
    {
        if (_selectedMainPurchaseId is null)
        {
            MessageBox.Show(
                "اختر صفاً مرتبطاً بعملية شراء (يظهر في الجدول بعد إضافة أو استيراد). الصفوف المعروضة من مخزون قديم بلا سجل شراء لا تُحدَّث من هنا.");
            return;
        }

        var quantity = ReadCommittedNumericUpDown(_mwQuantity);
        var purchasePrice = ReadCommittedNumericUpDown(_mwPurchasePrice);
        var retailUnitPrice = ReadCommittedNumericUpDown(_mwRetailPrice);
        if (retailUnitPrice < 0)
        {
            MessageBox.Show("سعر البيع لا يمكن أن يكون سالباً.", "تعديل", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var purchase = await db.Purchases.FirstOrDefaultAsync(x => x.Id == _selectedMainPurchaseId.Value);
        if (purchase is null) return;
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == purchase.ProductId);
        if (product is null) return;

        if (_mwProductLookup.SelectedItem is not MainWarehouseCatalogRow catalogRow || catalogRow.Id != product.Id)
        {
            MessageBox.Show(
                "يجب أن يطابق الصنف المختار سطر الشراء. لشراء نفس المنتج بدفعة جديدة استخدم «إضافة» لا «تعديل».",
                "تعديل",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1,
                MsgRtl);
            return;
        }

        product.Name = catalogRow.Name;
        product.CompanyId = catalogRow.CompanyId;
        product.ProductCategory = catalogRow.ProductCategory;
        product.PackageSize = catalogRow.PackageSize;
        product.UnitPrice = retailUnitPrice;

        purchase.Quantity = quantity;
        purchase.PurchasePrice = purchasePrice;
        purchase.ProductionDate = _mwProductionDate.Value.Date;
        purchase.PurchaseDate = _mwPurchaseDate.Value.Date;

        var movement = await db.StockMovements.FirstOrDefaultAsync(x =>
            x.ReferenceId == purchase.Id &&
            x.ProductId == purchase.ProductId &&
            x.MovementType == Domain.StockMovementType.Purchase);
        movement ??= await db.StockMovements.FirstOrDefaultAsync(x =>
            x.ReferenceId == purchase.Id && x.ProductId == purchase.ProductId);
        if (movement is not null)
        {
            movement.MovementType = Domain.StockMovementType.Purchase;
            movement.Quantity = purchase.Quantity;
            movement.ToWarehouseId = purchase.WarehouseId;
            movement.FromWarehouseId = null;
            movement.Notes = "تعديل يدوي من شاشة المستودع الرئيسي";
        }
        else
        {
            db.StockMovements.Add(new Domain.StockMovement
            {
                ProductId = purchase.ProductId,
                MovementType = Domain.StockMovementType.Purchase,
                Quantity = purchase.Quantity,
                ToWarehouseId = purchase.WarehouseId,
                ReferenceId = purchase.Id,
                Notes = "تعديل يدوي من شاشة المستودع الرئيسي (إنشاء حركة)"
            });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            MessageBox.Show(
                "تعذر حفظ التعديل (ربما يوجد منتج آخر بنفس الاسم والفئة والعبوة).\r\n" + (ex.InnerException?.Message ?? ex.Message),
                "خطأ في الحفظ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1,
                MsgRtl);
            return;
        }

        await RefreshAllStockViewsAsync();
        ClearMainWarehouseForm();
    }

    private async Task DeleteMainWarehouseManualAsync()
    {
        if (_selectedMainPurchaseId is null)
        {
            MessageBox.Show(
                "اختر صفاً مرتبطاً بعملية شراء لحذفها. الصفوف من مخزون قديم بلا سجل شراء لا تُحذف من هنا.");
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var purchase = await db.Purchases.FirstOrDefaultAsync(x => x.Id == _selectedMainPurchaseId.Value);
        if (purchase is null) return;
        var movements = await db.StockMovements
            .Where(x => x.ReferenceId == purchase.Id && x.ProductId == purchase.ProductId)
            .ToListAsync();
        db.StockMovements.RemoveRange(movements);
        db.Purchases.Remove(purchase);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            MessageBox.Show(
                "تعذر حذف السجل (ربما هناك بيانات مرتبطة).\r\n" + (ex.InnerException?.Message ?? ex.Message),
                "خطأ في الحفظ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1,
                MsgRtl);
            return;
        }

        await RefreshAllStockViewsAsync();
        ClearMainWarehouseForm();
    }

    private void LoadSelectedMainWarehouseRow()
    {
        if (_suppressMainWarehouseRowLoad || _mainWarehouseGridRefreshing) return;
        if (_mainWarehouseGrid.CurrentRow is null
            || _mainWarehouseGrid.CurrentRow.IsNewRow
            || _mainWarehouseGrid.CurrentRow.DataBoundItem is not MainWarehouseRow row)
        {
            _selectedMainPurchaseId = null;
            SyncMainWarehouseCommandStates();
            return;
        }

        _syncingMainWarehouseProductCombo = true;
        try
        {
            SelectMainWarehouseCatalogByProductId(row.ProductId);
        }
        finally
        {
            _syncingMainWarehouseProductCombo = false;
        }

        _selectedMainPurchaseId = row.PurchaseId;
        _mwQuantity.Value = Math.Clamp(row.PurchasedQuantity, _mwQuantity.Minimum, _mwQuantity.Maximum);
        _mwPurchasePrice.Value = Math.Clamp(row.PurchasePrice, _mwPurchasePrice.Minimum, _mwPurchasePrice.Maximum);
        _mwProductionDate.Value = row.ProductionDate == default ? DateTime.Today : row.ProductionDate;
        _mwPurchaseDate.Value = row.PurchaseDate == default ? DateTime.Today : row.PurchaseDate;
        SyncMainWarehouseRetailFromSelectedCatalog();
        SyncMainWarehouseCommandStates();
    }

    private static void SelectMainWarehouseComboValue(ComboBox combo, string? value)
    {
        if (combo.Items.Count == 0) return;
        if (string.IsNullOrWhiteSpace(value))
        {
            combo.SelectedIndex = 0;
            return;
        }

        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(combo.Items[i]?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        var exact = combo.FindStringExact(value);
        if (exact >= 0)
        {
            combo.SelectedIndex = exact;
            return;
        }

        combo.Items.Add(value);
        combo.SelectedItem = value;
    }

    private async Task LoadMainWarehouseCatalogAsync()
    {
        int? keepId = _mwProductLookup.SelectedItem is MainWarehouseCatalogRow r ? r.Id : null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var plist = await db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .Include(x => x.Company)
            .OrderBy(x => x.Company!.Name)
            .ThenBy(x => x.Name)
            .ToListAsync();
        var list = plist.Select(p => new MainWarehouseCatalogRow
        {
            Id = p.Id,
            CompanyId = p.CompanyId,
            CompanyName = p.Company?.Name ?? string.Empty,
            Name = p.Name,
            ProductCategory = p.ProductCategory,
            PackageSize = p.PackageSize,
            RetailUnitPrice = p.UnitPrice
        }).ToList();

        _syncingMainWarehouseProductCombo = true;
        try
        {
            _mwProductLookup.DataSource = null;
            _mwProductLookup.DisplayMember = nameof(MainWarehouseCatalogRow.Caption);
            _mwProductLookup.ValueMember = nameof(MainWarehouseCatalogRow.Id);
            _mwProductLookup.DataSource = list;
            if (keepId is > 0)
                SelectMainWarehouseCatalogByProductId(keepId.Value);
        }
        finally
        {
            _syncingMainWarehouseProductCombo = false;
        }

        SyncMainWarehouseRetailFromSelectedCatalog();
        await RefreshMainWarehouseAvailableStockHintAsync();
        await SyncMainWarehouseBranchPricePanelAsync();
        SyncMainWarehouseCommandStates();
    }

    private void SelectMainWarehouseCatalogByProductId(int productId)
    {
        if (_mwProductLookup.Items.Count == 0) return;
        for (var i = 0; i < _mwProductLookup.Items.Count; i++)
        {
            if (_mwProductLookup.Items[i] is MainWarehouseCatalogRow row && row.Id == productId)
            {
                _mwProductLookup.SelectedIndex = i;
                return;
            }
        }

        _mwProductLookup.SelectedIndex = -1;
    }

    private void ClearMainWarehouseForm()
    {
        _suppressMainWarehouseRowLoad = true;
        try
        {
            _selectedMainPurchaseId = null;
            _mwProductLookup.SelectedIndex = -1;
            _mwQuantity.Value = 0;
            _mwPurchasePrice.Value = 0;
            _mwRetailPrice.Value = 0;
            _mwProductionDate.Value = DateTime.Today;
            _mwPurchaseDate.Value = DateTime.Today;
            _mainWarehouseGrid.ClearSelection();
        }
        finally
        {
            _suppressMainWarehouseRowLoad = false;
        }

        SyncMainWarehouseCommandStates();
        SetMainWarehouseStockHint(string.Empty);
    }

    /// <summary>After «إضافة»: same SKU stays in the form for a new purchase line; only quantity and dates reset (price kept).</summary>
    private void PrepareMainWarehouseFormForNextBatch()
    {
        _suppressMainWarehouseRowLoad = true;
        try
        {
            _selectedMainPurchaseId = null;
            _mwQuantity.Value = _mwQuantity.Minimum;
            _mwProductionDate.Value = DateTime.Today;
            _mwPurchaseDate.Value = DateTime.Today;
            _mainWarehouseGrid.ClearSelection();
        }
        finally
        {
            _suppressMainWarehouseRowLoad = false;
        }

        SyncMainWarehouseCommandStates();
    }

    /// <summary>Uses the text shown in the NumericUpDown when it has not yet been committed to <see cref="NumericUpDown.Value"/>.</summary>
    private static decimal ReadCommittedNumericUpDown(NumericUpDown nud)
    {
        var styles = NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign | NumberStyles.AllowTrailingSign | NumberStyles.AllowDecimalPoint
            | NumberStyles.AllowThousands;

        static bool TryParseRounded(string? text, NumberStyles numberStyles, IFormatProvider? provider, int decimals, decimal min, decimal max, out decimal result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            if (!decimal.TryParse(text, numberStyles, provider, out var raw))
                return false;
            var rounded = Math.Round(raw, decimals, MidpointRounding.AwayFromZero);
            if (rounded < min || rounded > max)
                return false;
            result = rounded;
            return true;
        }

        if (TryParseRounded(nud.Text, styles, CultureInfo.CurrentCulture, nud.DecimalPlaces, nud.Minimum, nud.Maximum, out var fromCurrent))
            return fromCurrent;
        if (TryParseRounded(nud.Text, styles, CultureInfo.InvariantCulture, nud.DecimalPlaces, nud.Minimum, nud.Maximum, out var fromInvariant))
            return fromInvariant;

        return nud.Value;
    }
    private async Task SyncMainWarehouseBranchPricePanelAsync()
    {
        if (_mwBranchPriceWarehouseCombo.Items.Count == 0) return;
        if (_mwProductLookup.SelectedItem is not MainWarehouseCatalogRow row) return;
        if (!TryGetWarehouseIdFromCombo(_mwBranchPriceWarehouseCombo, out var whId)) return;
        var eff = await _inventoryService.GetEffectiveSalePriceAsync(row.Id, whId);
        _mwBranchRetailOverride.Value = Math.Clamp(eff, _mwBranchRetailOverride.Minimum, _mwBranchRetailOverride.Maximum);
    }

    private async Task SaveMainWarehouseBranchPriceAsync()
    {
        if (_mwProductLookup.SelectedItem is not MainWarehouseCatalogRow row)
        {
            MessageBox.Show("اختر صنفاً من الكتالوج.", "سعر الفرع", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        if (!TryGetWarehouseIdFromCombo(_mwBranchPriceWarehouseCombo, out var whId))
        {
            MessageBox.Show("اختر الفرع.", "سعر الفرع", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        try
        {
            await _inventoryService.SetBranchSalePriceAsync(_currentUser.Id, whId, row.Id, _mwBranchRetailOverride.Value);
            MessageBox.Show("تم حفظ سعر البيع للفرع.", "سعر الفرع", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MsgRtl);
            await RefreshAllStockViewsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "سعر الفرع", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }

    private async Task ResetMainWarehouseBranchPriceAsync()
    {
        if (_mwProductLookup.SelectedItem is not MainWarehouseCatalogRow row)
        {
            MessageBox.Show("اختر صنفاً من الكتالوج.", "سعر الفرع", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        if (!TryGetWarehouseIdFromCombo(_mwBranchPriceWarehouseCombo, out var whId))
        {
            MessageBox.Show("اختر الفرع.", "سعر الفرع", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        try
        {
            await _inventoryService.DeleteBranchSalePriceAsync(_currentUser.Id, whId, row.Id);
            await SyncMainWarehouseBranchPricePanelAsync();
            await RefreshAllStockViewsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "سعر الفرع", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }
    private void RestoreMainWarehouseFieldCaptionFonts(Control root)
    {
        _ = root;
        if (_mwHeaderTitleLabel is not null)
        {
            _mwHeaderTitleLabel.Font = MwFontPageTitle;
            _mwHeaderTitleLabel.ForeColor = UiTextPrimary;
            _mwHeaderTitleLabel.BackColor = Color.White;
            _mwHeaderTitleLabel.RightToLeft = RightToLeft.Yes;
            _mwHeaderTitleLabel.UseCompatibleTextRendering = false;
        }

        if (_mwHeaderUserLabel is not null)
        {
            _mwHeaderUserLabel.Font = MwFontSubtitle;
            _mwHeaderUserLabel.ForeColor = UiTextSecondary;
            _mwHeaderUserLabel.BackColor = Color.White;
            _mwHeaderUserLabel.RightToLeft = RightToLeft.Yes;
            _mwHeaderUserLabel.UseCompatibleTextRendering = false;
        }

        StyleMainWarehouseGrid();
    }
    private sealed class MainWarehouseCatalogRow
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProductCategory { get; set; } = string.Empty;
        public string PackageSize { get; set; } = string.Empty;
        /// <summary>POS retail from <see cref="Domain.Product.UnitPrice"/>.</summary>
        public decimal RetailUnitPrice { get; set; }
        public string Caption =>
            string.IsNullOrWhiteSpace(CompanyName)
                ? $"{Name} — {ProductCategory} / {PackageSize}"
                : $"{CompanyName} — {Name} ({ProductCategory}, {PackageSize})";
    }

    private sealed class MainWarehouseRow
    {
        /// <summary>Used to collapse duplicate &quot;on hand&quot; totals when several purchase lines exist for one SKU.</summary>
        public int ProductId { get; set; }

        public int? PurchaseId { get; set; }

        /// <summary>1-based index among purchase lines for this <see cref="ProductId"/> (chronological).</summary>
        public int BatchNumber { get; set; }

        /// <summary>How many purchase rows exist for this product at main.</summary>
        public int BatchTotal { get; set; }

        /// <summary>Populated when multiple purchases share one product, e.g. <c>1/2</c>, <c>2/2</c>.</summary>
        public string BatchLabel { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;
        public string InventoryName { get; set; } = string.Empty;
        public DateTime ProductionDate { get; set; }
        public decimal PurchasedQuantity { get; set; }
        /// <summary>Total on-hand at Main for this product; null on extra lines for the same product (same value otherwise).</summary>
        public decimal? OnHandAtMain { get; set; }
        public decimal PurchasePrice { get; set; }
        /// <summary>Current POS / shelf price on the product master (<see cref="Domain.Product.UnitPrice"/>).</summary>
        public decimal RetailUnitPrice { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string ProductCategory { get; set; } = string.Empty;
        public string PackageSize { get; set; } = string.Empty;
    }
}
