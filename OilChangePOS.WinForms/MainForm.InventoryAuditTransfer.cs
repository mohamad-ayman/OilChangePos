using ClosedXML.Excel;
using OilChangePOS.Business;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;
public partial class MainForm : Form
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
        _newProductPackageSize.Items.AddRange(["1L", "4L", "5L", "16L", "20L", "Unit"]);
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

    private TabPage BuildAuditTab()
    {
        var tab = new TabPage("جرد المخزون");
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal
        };
        ApplyInitialSplitterDistance(split, 400, 200, 160);

        var topBar = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(12, 8, 12, 8), BackColor = Color.FromArgb(245, 247, 250), RightToLeft = RightToLeft.No };
        var runAudit = BuildButton("تنفيذ الجرد وترحيل الفروقات", Color.FromArgb(142, 68, 173));
        runAudit.Click += async (_, _) => await RunAuditAsync();

        var auditPageHeader = BuildStandardModuleHeaderCard(
            "جرد المخزون",
            "المستودع كما في تبويب المخزون (أو فرع البيع، ثم الرئيسي). أدخل الكمية الفعلية لكل صنف واختر السبب عند الفارق (يُرحّل كحركة تسوية). اللوحة السفلية: سجل للقراءة فقط.",
            subtitleItalic: false,
            DockStyle.Top,
            autoSizeHeight: true,
            out _,
            out _);
        auditPageHeader.Margin = new Padding(0, 0, 0, 10);
        var runBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.RightToLeft,
            RightToLeft = RightToLeft.No,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 4)
        };
        runBar.Controls.Add(runAudit);

        topBar.Controls.Add(runBar);
        topBar.Controls.Add(auditPageHeader);

        StyleGrid(_auditGrid);
        ConfigureAuditColumns();
        _auditGrid.DataSource = _auditBinding;
        var countHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 8) };
        countHost.Controls.Add(_auditGrid);
        split.Panel1.Controls.Add(countHost);
        split.Panel1.Controls.Add(topBar);

        var historyHeader = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 8, 8, 0) };
        var histFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, RightToLeft = RightToLeft.Yes };
        histFlow.Controls.Add(new Label { Text = "من تاريخ", AutoSize = true, Padding = new Padding(0, 6, 4, 0), RightToLeft = RightToLeft.Yes });
        _auditHistoryFrom.Value = DateTime.Today.AddDays(-30);
        histFlow.Controls.Add(_auditHistoryFrom);
        histFlow.Controls.Add(new Label { Text = "إلى", AutoSize = true, Padding = new Padding(8, 6, 4, 0), RightToLeft = RightToLeft.Yes });
        _auditHistoryTo.Value = DateTime.Today;
        histFlow.Controls.Add(_auditHistoryTo);
        var refreshHist = BuildButton("تحديث السجل", Color.FromArgb(52, 152, 219));
        refreshHist.Margin = new Padding(16, 0, 0, 0);
        refreshHist.Click += async (_, _) => await RefreshAuditHistoryAsync();
        histFlow.Controls.Add(refreshHist);
        histFlow.Controls.Add(_auditHistoryFilterWarehouse);
        historyHeader.Controls.Add(histFlow);

        StyleGrid(_auditHistoryGrid);
        ConfigureAuditHistoryColumns();
        var historyHost = new Panel { Dock = DockStyle.Fill };
        historyHost.Controls.Add(_auditHistoryGrid);
        historyHost.Controls.Add(historyHeader);
        split.Panel2.Controls.Add(historyHost);

        tab.Controls.Add(split);
        return tab;
    }

    private void ConfigureAuditColumns()
    {
        _auditGrid.Columns.Clear();
        _auditGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "الصنف",
            DataPropertyName = nameof(AuditRow.ProductName),
            ReadOnly = true,
            FillWeight = 28
        });
        _auditGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "كمية النظام",
            DataPropertyName = nameof(AuditRow.SystemQuantity),
            ReadOnly = true,
            FillWeight = 11,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _auditGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "الكمية الفعلية",
            DataPropertyName = nameof(AuditRow.ActualQuantity),
            ReadOnly = false,
            FillWeight = 11,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        var reasonCol = new DataGridViewComboBoxColumn
        {
            HeaderText = "السبب (عند الفارق)",
            DataPropertyName = nameof(AuditRow.ReasonCode),
            DisplayMember = nameof(AuditReasonItem.Display),
            ValueMember = nameof(AuditReasonItem.Code),
            FlatStyle = FlatStyle.Flat,
            FillWeight = 20
        };
        reasonCol.DataSource = StockAuditReasonCodes.Options
            .Where(o => o.Code != StockAuditReasonCodes.InventoryScreen)
            .Select(o => new AuditReasonItem { Code = o.Code, Display = o.Display })
            .ToList();
        _auditGrid.Columns.Add(reasonCol);
    }

    private void ConfigureAuditHistoryColumns()
    {
        _auditHistoryGrid.Columns.Clear();
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AuditId", HeaderText = "رقم الجرد", FillWeight = 8, ReadOnly = true });
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AuditDateLocal", HeaderText = "التاريخ", FillWeight = 14, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "WarehouseName", HeaderText = "المستودع", FillWeight = 12, ReadOnly = true });
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductName", HeaderText = "الصنف", FillWeight = 16, ReadOnly = true });
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SystemQuantity", HeaderText = "كمية النظام", FillWeight = 10, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ActualQuantity", HeaderText = "الكمية الفعلية", FillWeight = 10, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Variance", HeaderText = "الفارق", FillWeight = 8, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReasonDisplay", HeaderText = "السبب", FillWeight = 14, ReadOnly = true });
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AuditNotes", HeaderText = "ملاحظات", FillWeight = 14, ReadOnly = true });
        _auditHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CreatedByUsername", HeaderText = "بواسطة", FillWeight = 10, ReadOnly = true });
    }

    private sealed class AuditReasonItem
    {
        public string Code { get; init; } = string.Empty;
        public string Display { get; init; } = string.Empty;
    }

    private TabPage BuildTransferTab()
    {
        var tab = new TabPage("التحويلات");
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 20), BackColor = Color.FromArgb(245, 247, 250), RightToLeft = RightToLeft.No, AutoScroll = true };
        var transferHeader = BuildStandardModuleHeaderCard(
            "تحويل بين المستودعات",
            "المستودع الرئيسي يستقبل المشتريات؛ لكل فرع رصيده. التحويل من الرئيسي للفرع يستهلك أقدم تاريخ إنتاج أولاً (FEFO) وقد يظهر عدة أسطر في السجل. التحويل بين الفروع غير مسموح. اختر «من المستودع» أولاً؛ الأصناف المعروضة لها رصيد هناك.",
            subtitleItalic: true,
            DockStyle.Top,
            autoSizeHeight: true,
            out _,
            out _);
        transferHeader.Margin = new Padding(0, 0, 0, 12);

        _transferFromWarehouseCombo.SelectedIndexChanged += async (_, _) => await RefreshTransferProductsAsync();
        _transferToWarehouseCombo.SelectedIndexChanged += async (_, _) => await RefreshTransferBranchPriceRowAsync();
        _transferProductCombo.SelectedIndexChanged += async (_, _) =>
        {
            SyncTransferQtyLimitFromSelection();
            await RefreshTransferBranchPriceRowAsync();
        };
        _transferApplyBranchSalePriceCheck.CheckedChanged += async (_, _) => await OnTransferApplyBranchSalePriceCheckChangedAsync();

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 8,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(16, 14, 16, 16),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        form.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        var transferButton = BuildButton("تنفيذ التحويل", Color.FromArgb(41, 128, 185));
        transferButton.RightToLeft = RightToLeft.Yes;
        transferButton.Click += async (_, _) => await TransferStockAsync();

        form.Controls.Add(new Label { Text = "من المستودع", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UiFontCaption, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 0, 0);
        form.Controls.Add(_transferFromWarehouseCombo, 1, 0);
        form.Controls.Add(new Label { Text = "إلى المستودع", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UiFontCaption, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 0, 1);
        form.Controls.Add(_transferToWarehouseCombo, 1, 1);
        form.Controls.Add(new Label { Text = "الصنف (حسب المصدر)", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UiFontCaption, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 0, 2);
        form.Controls.Add(_transferProductCombo, 1, 2);
        form.Controls.Add(new Label { Text = "الكمية", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = UiFontCaption, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes }, 0, 3);
        form.Controls.Add(_transferQty, 1, 3);
        form.Controls.Add(_transferApplyBranchSalePriceCheck, 1, 4);
        form.Controls.Add(new Label
        {
            Text = "سعر البيع في الفرع (ج.م)",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            Font = UiFontCaption,
            ForeColor = UiTextPrimary,
            RightToLeft = RightToLeft.Yes
        }, 0, 5);
        form.Controls.Add(_transferBranchSalePrice, 1, 5);
        form.SetColumnSpan(_transferBranchSalePriceHint, 2);
        form.Controls.Add(_transferBranchSalePriceHint, 0, 6);
        form.Controls.Add(transferButton, 1, 7);

        root.Controls.Add(form);
        root.Controls.Add(transferHeader);
        tab.Controls.Add(root);
        return tab;
    }
}

