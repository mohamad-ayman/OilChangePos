using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm : Form
{
    /// <summary>Primary body text / values (high contrast).</summary>
    private static readonly Color UiTextPrimary = Color.FromArgb(30, 30, 30);
    /// <summary>Supporting text (still readable; not light gray).</summary>
    private static readonly Color UiTextSecondary = Color.FromArgb(70, 70, 70);
    /// <summary>Microsoft Segoe UI scale: typography hierarchy only (no layout changes).</summary>
    private static readonly Font UiFontTitle = new("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font UiFontSection = new("Segoe UI", 12.5f, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font UiFont = new("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
    /// <summary>Field captions slightly heavier than inputs.</summary>
    private static readonly Font UiFontCaption = new("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font UiGridCell = new("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font UiGridHeader = new("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
    /// <summary>Main Warehouse tab: smaller section titles than <see cref="UiFontTitle"/> / <see cref="UiFontSection"/>, slightly larger body than <see cref="UiFont"/> for Arabic in combo + grid.</summary>
    private static readonly Font MwFontPageTitle = new("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font MwFontSubtitle = new("Segoe UI", 10.75f, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font MwFontSection = new("Segoe UI", 10.75f, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font MwFontFieldLabel = new("Segoe UI", 10.75f, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font MwFontInput = new("Segoe UI", 11.25f, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font MwFontGridCell = new("Segoe UI", 11.25f, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font MwFontGridHeader = new("Segoe UI", 10.75f, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly CultureInfo ReportsCulture = CultureInfo.GetCultureInfo("ar-SA");
    private const decimal LowStockThreshold = 5m;
    private const MessageBoxOptions MsgRtl = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;
    /// <summary>Main Warehouse captions/inputs with this <see cref="Control.Tag"/> skip <see cref="ApplyUnifiedFont"/> so sizes stay readable.</summary>
    private const string MainWarehouseUiLabelTag = "MW_UI";
    private readonly IDbContextFactory<OilChangePosDbContext> _dbFactory;
    private readonly ISalesService _salesService;
    private readonly IServiceOrderService _serviceOrderService;
    private readonly IInventoryService _inventoryService;
    private readonly IReportService _reportService;
    private readonly IExpenseService _expenseService;
    private readonly ITransferService _transferService;
    private readonly IWarehouseService _warehouseService;
    private readonly ICustomerService _customerService;
    private readonly AppUser _currentUser;
    private readonly List<Button> _sidebarNavButtons = [];
    private Panel _inventoryTopPanel = null!;
    private GroupBox _inventoryAdminGroup = null!;
    private GroupBox _inventoryAddProductGroup = null!;
    private readonly BindingSource _cartBinding = new();
    private readonly BindingSource _inventoryBinding = new();
    private readonly BindingSource _auditBinding = new();
    private readonly DataGridView _cartGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = false };
    private readonly DataGridView _inventoryGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = true };
    private readonly DataGridView _auditGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly DataGridView _auditHistoryGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DateTimePicker _auditHistoryFrom = new() { Width = 120, Format = DateTimePickerFormat.Short };
    private readonly DateTimePicker _auditHistoryTo = new() { Width = 120, Format = DateTimePickerFormat.Short };
    private readonly CheckBox _auditHistoryFilterWarehouse = new() { Text = "تقييد السجل بمستودع المخزون الحالي", AutoSize = true, Checked = true, RightToLeft = RightToLeft.Yes };
    private readonly DataGridView _topProductsGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _slowMovingGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _transferReportGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _warehouseInventoryGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _salesByWarehouseGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _reportLowStockGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _mainWarehouseGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = true, ReadOnly = true };
    private readonly TabControl _mainTabs = new() { Dock = DockStyle.Fill };
    private readonly Label _reportPeriodBanner = new() { Dock = DockStyle.Top, Height = 42, ForeColor = Color.White, BackColor = Color.FromArgb(44, 62, 80), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 16, 0), RightToLeft = RightToLeft.Yes };
    private readonly Label _reportMetricsFootnote = new()
    {
        Dock = DockStyle.Top,
        Height = 44,
        ForeColor = UiTextSecondary,
        BackColor = Color.FromArgb(248, 249, 250),
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(16, 8, 16, 8),
        Font = UiFont,
        RightToLeft = RightToLeft.Yes
    };
    private readonly Label _kpiNetSalesVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiInvoicesVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiAvgTicketVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiEstProfitVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiStockValueVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiLowStockVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    // Branch POS — KPI labels
    private readonly Label _kpiBranchSalesVal = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes };
    private readonly Label _kpiBranchInvVal   = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes };
    private readonly Label _kpiBranchLowVal   = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes };
    private readonly Label _kpiBranchNameVal  = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _reportWarehouseCombo = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _reportFromPicker = new() { Width = 150 };
    private readonly DateTimePicker _reportToPicker = new() { Width = 150 };
    private readonly DataGridView _profitByInvoiceGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _profitByProductGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly Label _profitRollupLabel = new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        TextAlign = ContentAlignment.TopRight,
        Font = UiFontSection,
        ForeColor = Color.FromArgb(44, 62, 80),
        BackColor = Color.FromArgb(232, 238, 245),
        RightToLeft = RightToLeft.Yes,
        Padding = new Padding(16, 14, 16, 14),
        Margin = new Padding(0, 0, 0, 10),
        BorderStyle = BorderStyle.None,
        MaximumSize = new Size(3200, 0),
        UseCompatibleTextRendering = false
    };
    private readonly CheckBox _profitAllBranchesCheck = new() { Text = "ربحية جميع الفروع (تجاهل المستودع المحدد)", AutoSize = true, RightToLeft = RightToLeft.Yes };
    private readonly DataGridView _stockFromMovementsGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly ComboBox _reportHistoryProductCombo = new() { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _stockHistoryGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _transferFullGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _cashFlowGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _expenseReportGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly NumericUpDown _expenseAmountInput = new() { Width = 120, DecimalPlaces = 2, Maximum = 10000000, Minimum = 0 };
    private readonly TextBox _expenseCategoryInput = new() { Width = 200 };
    private readonly TextBox _expenseDescriptionInput = new() { Width = 320 };
    private readonly DateTimePicker _expenseDateInput = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly Button _expenseSaveButton = new() { Text = "تسجيل مصروف", Width = 140, Height = 32 };
    private readonly TextBox _inventorySearchBox = new() { Width = 260, PlaceholderText = "بحث في المخزون...", RightToLeft = RightToLeft.Yes };
    private readonly CheckBox _inventoryLowStockOnlyCheck = new() { Text = "المخزون المنخفض فقط", AutoSize = true, RightToLeft = RightToLeft.Yes };
    private readonly CheckBox _inventoryShowZeroStockCheck = new()
    {
        Text = "إظهار أصناف بلا رصيد في الفرع",
        AutoSize = true,
        Checked = false,
        RightToLeft = RightToLeft.Yes
    };
    private readonly Button _inventoryLowStockToggle = new() { Width = 130, Height = 32, Text = "تنبيه منخفض: إيقاف", RightToLeft = RightToLeft.Yes };
    private readonly Label _inventoryTotalItemsLabel = new() { Dock = DockStyle.Top, Height = 34, ForeColor = Color.White, BackColor = Color.FromArgb(52, 152, 219), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 10, 0), RightToLeft = RightToLeft.Yes };
    private readonly Label _inventoryTotalValueLabel = new() { Dock = DockStyle.Top, Height = 34, ForeColor = Color.White, BackColor = Color.FromArgb(39, 174, 96), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 10, 0), RightToLeft = RightToLeft.Yes };
    private readonly Label _inventoryLowStockLabel = new() { Dock = DockStyle.Top, Height = 34, ForeColor = Color.White, BackColor = Color.FromArgb(192, 57, 43), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(0, 0, 10, 0), RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _newProductCompanyCombo = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _newProductName = new() { Width = 150, PlaceholderText = "اسم الصنف", RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _newProductType = new() { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _newProductPackageSize = new() { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _newProductPrice = new() { Width = 90, DecimalPlaces = 2, Maximum = 1000000 };
    private readonly NumericUpDown _newProductOpeningStock = new() { Width = 90, DecimalPlaces = 3, Maximum = 1000000 };
    private readonly PictureBox _newProductImagePreview = new() { Width = 36, Height = 36, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
    private readonly Button _chooseImageButton = new() { Width = 110, Height = 32, Text = "اختيار صورة", RightToLeft = RightToLeft.Yes };
    private string? _pendingImagePath;
    private List<InventoryRow> _inventoryRows = [];
    private Dictionary<int, string> _productImageMap = [];
    private readonly FlowLayoutPanel _productCardsPanel = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(4) };
    private readonly TextBox _posSearchBox = new() { Width = 280, PlaceholderText = "بحث عن صنف...", RightToLeft = RightToLeft.Yes };
    private readonly Label _subtotalValueLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(44, 62, 80) };
    private readonly Label _discountValueLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(192, 57, 43) };
    private readonly Label _totalValueLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(39, 174, 96) };
    private readonly ComboBox _inventoryProductCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
    private readonly NumericUpDown _inventorySetQty = new() { DecimalPlaces = 3, Width = 140, Maximum = 100000 };
    private readonly ComboBox _posWarehouseCombo = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _posCustomerCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _inventoryWarehouseCombo = new() { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _inventoryResetBranchPriceBtn = new()
    {
        Text = "إعادة السعر للقائمة (الصف المحدد)",
        AutoSize = true,
        Height = 32,
        RightToLeft = RightToLeft.Yes
    };
    private readonly ComboBox _transferFromWarehouseCombo = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _transferToWarehouseCombo = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly ComboBox _transferProductCombo = new() { Width = 400, DropDownStyle = ComboBoxStyle.DropDownList, RightToLeft = RightToLeft.Yes };
    private readonly NumericUpDown _transferQty = new() { DecimalPlaces = 3, Width = 140, Maximum = 100000, RightToLeft = RightToLeft.Yes };
    private Label? _mwHeaderTitleLabel;
    private Label? _mwHeaderUserLabel;
    private Label? _mwAvailableStockLabel;
    private TabPage? _mainWarehouseTabPage;
    private Button _mwCmdAdd = null!;
    private Button _mwCmdUpdate = null!;
    private Button _mwCmdDelete = null!;
    private Button _mwCmdClear = null!;
    private readonly ComboBox _mwProductLookup = new()
    {
        Width = 320,
        DropDownStyle = ComboBoxStyle.DropDownList,
        DrawMode = DrawMode.Normal,
        ItemHeight = 26,
        DropDownWidth = 480,
        // Standard/Poppy styles paint Arabic + bound DisplayMember more reliably than Flat with owner-draw.
        FlatStyle = FlatStyle.Standard,
        BackColor = Color.White,
        ForeColor = UiTextPrimary,
        Font = MwFontInput,
        Cursor = Cursors.Hand,
        IntegralHeight = false,
        Height = 38,
        Margin = new Padding(0, 2, 0, 4),
        RightToLeft = RightToLeft.Yes,
        Tag = MainWarehouseUiLabelTag
    };

    private bool _syncingMainWarehouseProductCombo;
    private readonly NumericUpDown _mwQuantity = new()
    {
        Width = 118,
        DecimalPlaces = 3,
        Maximum = 1000000,
        Tag = MainWarehouseUiLabelTag
    };
    private readonly NumericUpDown _mwPurchasePrice = new()
    {
        Width = 118,
        DecimalPlaces = 2,
        Maximum = 1000000,
        Tag = MainWarehouseUiLabelTag
    };
    /// <summary><see cref="Domain.Product.UnitPrice"/> — shelf / POS selling price (not purchase cost).</summary>
    private readonly NumericUpDown _mwRetailPrice = new()
    {
        Width = 118,
        DecimalPlaces = 2,
        Maximum = 1000000,
        Tag = MainWarehouseUiLabelTag
    };
    private readonly ComboBox _mwBranchPriceWarehouseCombo = new()
    {
        Width = 200,
        DropDownStyle = ComboBoxStyle.DropDownList,
        Tag = MainWarehouseUiLabelTag,
        RightToLeft = RightToLeft.Yes
    };
    private readonly NumericUpDown _mwBranchRetailOverride = new()
    {
        Width = 118,
        DecimalPlaces = 2,
        Maximum = 1000000,
        Tag = MainWarehouseUiLabelTag
    };
    private Button _mwBranchPriceSaveBtn = null!;
    private Button _mwBranchPriceResetBtn = null!;
    private bool _mwBranchPriceEventsAttached;
    private readonly DateTimePicker _mwProductionDate = new()
    {
        Width = 142,
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd",
        Tag = MainWarehouseUiLabelTag
    };
    private readonly DateTimePicker _mwPurchaseDate = new()
    {
        Width = 142,
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd",
        Tag = MainWarehouseUiLabelTag
    };
    private readonly ToolTip _mainWarehouseToolTip = new() { AutoPopDelay = 14000, InitialDelay = 350, ReshowDelay = 180, ShowAlways = true };
    private int? _selectedMainPurchaseId;
    private bool _suppressMainWarehouseRowLoad;
    private bool _mainWarehouseGridRefreshing;
    private List<MainWarehouseRow> _mainWarehouseAllRows = [];
    private int _mainWarehousePageIndex;
    private Button _mainWarehousePagerPrev = null!;
    private Button _mainWarehousePagerNext = null!;
    private Label _mainWarehousePagerInfo = null!;
    private ComboBox _mainWarehousePageSizeCombo = null!;
    private readonly Label _mainWarehouseInfoLabel = new()
    {
        AutoSize = true,
        ForeColor = UiTextPrimary,
        Font = UiFontCaption,
        Text = "المستودع الرئيسي: -"
    };
    private readonly NumericUpDown _posAddQty = new() { DecimalPlaces = 3, Width = 130, Minimum = 0.001m, Value = 1, Maximum = 100000 };
    private readonly NumericUpDown _posDiscount = new() { DecimalPlaces = 2, Width = 120, Maximum = 100000 };
    private readonly NumericUpDown _oilChangeCustomerId = new() { Minimum = 1, Maximum = 999_999, Width = 72 };
    private readonly NumericUpDown _oilChangeCarId = new() { Minimum = 1, Maximum = 999_999, Width = 72 };
    private readonly NumericUpDown _oilChangeOdometer = new() { Minimum = 0, Maximum = 999_999, Width = 88 };
    private readonly Label _breadcrumbLabel = new() { Dock = DockStyle.Top, Height = 22, ForeColor = UiTextSecondary, Text = "الرئيسية / الطلب", TextAlign = ContentAlignment.TopRight, RightToLeft = RightToLeft.Yes };
    private readonly Label _orderTitleLabel = new() { Dock = DockStyle.Top, Height = 38, Font = UiFontTitle, ForeColor = UiTextPrimary, Text = "البيع", TextAlign = ContentAlignment.TopRight, RightToLeft = RightToLeft.Yes };
    private readonly FlowLayoutPanel _categoryPanel = new() { Dock = DockStyle.Top, Height = 42, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 4, 0, 0) };
    private List<AvailableProductRow> _availableProducts = [];
    private string _selectedCategory = "All";
    private bool _reportWarehouseEventsAttached;
    private bool _posInventoryWarehouseEventsAttached;
    private readonly DataGridView _branchesGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly TextBox _branchName = new() { Width = 320 };
    private readonly CheckBox _branchActive = new() { Text = "نشط (يظهر في نقطة البيع والمخزون)", AutoSize = true, Checked = true };
    private int? _selectedBranchId;
    private bool _suppressBranchRowLoad;

    private readonly DataGridView _catalogCompaniesGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _catalogProductsGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly TextBox _catalogCompanyNameEdit = new() { Width = 300 };
    private readonly CheckBox _catalogCompanyActiveEdit = new() { AutoSize = true, Text = "شركة نشطة", Checked = true };
    private readonly TextBox _catalogProductNameEdit = new() { Width = 220 };
    private readonly ComboBox _catalogProductTypeCombo = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _catalogProductPackCombo = new() { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _catalogProductActiveEdit = new() { AutoSize = true, Text = "صنف نشط", Checked = true };
    private int? _selectedCatalogCompanyId;
    private int? _selectedCatalogProductId;
    private bool _suppressCatalogCompanyLoad;
    private bool _suppressCatalogProductLoad;

    /// <summary>Set when the user chooses log out; <see cref="Program"/> shows the login dialog again.</summary>
    internal bool LogoutRequested { get; private set; }

    public MainForm(
        IDbContextFactory<OilChangePosDbContext> dbFactory,
        ISalesService salesService,
        IServiceOrderService serviceOrderService,
        IInventoryService inventoryService,
        IReportService reportService,
        IExpenseService expenseService,
        ITransferService transferService,
        IWarehouseService warehouseService,
        ICustomerService customerService,
        AppUser currentUser)
    {
        _dbFactory = dbFactory;
        _salesService = salesService;
        _serviceOrderService = serviceOrderService;
        _inventoryService = inventoryService;
        _reportService = reportService;
        _expenseService = expenseService;
        _transferService = transferService;
        _warehouseService = warehouseService;
        _customerService = customerService;
        _currentUser = currentUser;

        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Text = "نظام نقطة بيع تغيير الزيت والمخزون";
        Width = 1500;
        Height = 900;
        MinimumSize = new Size(1300, 780);
        Font = UiFont;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 250);
        KeyPreview = true;
        KeyDown += OnMainFormKeyDown;
        BuildUi();
        ApplyRoleToUi();
        ApplyUnifiedFont(this);
        RestoreMainWarehouseFieldCaptionFonts(this);
        ApplyReportsVisualStyle();
        Load += async (_, _) => await LoadDataAsync();
    }

    private async void OnMainFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (_mainWarehouseTabPage is null || _mainTabs.SelectedTab != _mainWarehouseTabPage)
            return;

        if (_mainWarehouseGrid.IsCurrentCellInEditMode)
            return;

        if (ActiveControl == _mainWarehouseGrid && e.KeyCode == Keys.Enter)
            return;

        if (e.KeyCode == Keys.Enter && !e.Control && !e.Shift && !e.Alt)
        {
            if (ActiveControl is DateTimePicker)
                return;

            if (_mwCmdAdd is null || !_mwCmdAdd.Enabled)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            await AddMainWarehouseManualAsync();
            return;
        }

        if (e.KeyCode == Keys.Delete && _selectedMainPurchaseId.HasValue)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            await DeleteMainWarehouseManualAsync();
            return;
        }

        if (e.KeyCode == Keys.E && e.Control)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            await ExportMainWarehouseExcelAsync();
        }
    }

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

    private void ApplyReportsVisualStyle()
    {
        _reportPeriodBanner.Font = UiFontSection;
        _reportPeriodBanner.RightToLeft = RightToLeft.Yes;
        foreach (var l in new[] { _kpiNetSalesVal, _kpiInvoicesVal, _kpiAvgTicketVal, _kpiEstProfitVal, _kpiStockValueVal, _kpiLowStockVal })
            l.Font = new Font("Segoe UI", 16.5f, FontStyle.Bold, GraphicsUnit.Point);
    }

    private void ApplyRoleToUi()
    {
        var admin = _currentUser.Role == UserRole.Admin;
        Text = admin
            ? $"نقطة بيع تغيير الزيت — {_currentUser.Username} (مدير)"
            : $"نقطة بيع تغيير الزيت — {_currentUser.Username} (فرع)";

        if (admin)
        {
            if (_sidebarNavButtons.Count > 1)
            {
                _sidebarNavButtons[0].Visible = false;
                _sidebarNavButtons[1].Visible = false;
            }
            _mainTabs.SelectedIndex = 4;
            ApplySidebarNavHighlight(_mainTabs.SelectedIndex);
            return;
        }

        for (var i = 2; i < _sidebarNavButtons.Count; i++)
            _sidebarNavButtons[i].Visible = false;

        _inventoryShowZeroStockCheck.Checked = true;
        _inventoryAdminGroup.Visible = false;
        _inventoryAddProductGroup.Visible = false;
        _inventoryTopPanel.Height = 268;
        _mainTabs.SelectedIndex = 0;
        ApplySidebarNavHighlight(_mainTabs.SelectedIndex);
    }

    private void OnMainTabsSelecting(object? sender, TabControlCancelEventArgs e)
    {
        if (_currentUser.Role == UserRole.Admin)
        {
            if (e.TabPageIndex < 2)
                e.Cancel = true;
            return;
        }

        if (e.TabPageIndex > 1)
            e.Cancel = true;
    }

    private void BuildUi()
    {
        var sidebar = BuildSidebar();
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = 268;

        var content = BuildMainContent();
        content.Dock = DockStyle.Fill;

        Controls.Add(content);
        Controls.Add(sidebar);
    }

    private Control BuildSidebar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(38, 43, 48) };
        var header = new Label
        {
            Text = "نقطة بيع تغيير الزيت",
            Dock = DockStyle.Top,
            Height = 56,
            ForeColor = Color.White,
            Font = UiFontSection,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 12, 0),
            RightToLeft = RightToLeft.Yes
        };

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10, 16, 10, 12),
            RightToLeft = RightToLeft.Yes
        };

        menu.Controls.Add(BuildMenuButton("طلب / بيع", 0));
        menu.Controls.Add(BuildMenuButton("المخزون", 1));
        menu.Controls.Add(BuildMenuButton("الشركات والأصناف", 2));
        menu.Controls.Add(BuildMenuButton("الفروع", 3));
        menu.Controls.Add(BuildMenuButton("المستودع الرئيسي", 4));
        menu.Controls.Add(BuildMenuButton("التحويلات", 5));
        menu.Controls.Add(BuildMenuButton("جرد المخزون", 6));
        menu.Controls.Add(BuildMenuButton("التقارير", 7));

        panel.Controls.Add(menu);
        panel.Controls.Add(header);
        return panel;
    }

    private Control BuildMainContent()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(242, 245, 249) };
        var topbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(33, 37, 41) };
        var topbarTitle = new Label
        {
            Text = " ",
            Dock = DockStyle.Left,
            Width = 40,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var logoutBtn = new Button
        {
            Text = "تسجيل الخروج",
            AutoSize = true,
            MinimumSize = new Size(120, 32),
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(52, 58, 64),
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 0, 0, 0),
            Font = new Font("Segoe UI", 10.75f, FontStyle.Bold, GraphicsUnit.Point),
            UseCompatibleTextRendering = false
        };
        logoutBtn.FlatAppearance.BorderSize = 0;
        logoutBtn.Click += (_, _) =>
        {
            if (MessageBox.Show(
                    "تسجيل الخروج من النظام؟",
                    "تأكيد",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2,
                    MsgRtl) != DialogResult.Yes)
                return;
            LogoutRequested = true;
            Close();
        };

        var userCaps = _currentUser.Role == UserRole.Admin ? "مدير" : "فرع";
        var userLine = new Label
        {
            AutoSize = true,
            Text = $"{_currentUser.Username} · {userCaps}",
            ForeColor = Color.FromArgb(230, 232, 235),
            Font = UiFont,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 6, 16, 0)
        };

        var rightBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(8, 8, 12, 8),
            BackColor = Color.FromArgb(33, 37, 41)
        };
        rightBar.Controls.Add(logoutBtn);
        rightBar.Controls.Add(userLine);

        topbar.Controls.Add(rightBar);
        topbar.Controls.Add(topbarTitle);

        _mainTabs.TabPages.Add(BuildPosTab());
        _mainTabs.TabPages.Add(BuildInventoryTab());
        _mainTabs.TabPages.Add(BuildCatalogTab());
        _mainTabs.TabPages.Add(BuildBranchesAdminTab());
        _mainTabs.TabPages.Add(BuildMainWarehouseTab());
        _mainTabs.TabPages.Add(BuildTransferTab());
        _mainTabs.TabPages.Add(BuildAuditTab());
        _mainTabs.TabPages.Add(BuildReportsTab());
        _mainTabs.Appearance = TabAppearance.FlatButtons;
        _mainTabs.ItemSize = new Size(0, 1);
        _mainTabs.SizeMode = TabSizeMode.Fixed;
        _mainTabs.RightToLeft = RightToLeft.Yes;
        _mainTabs.RightToLeftLayout = false;
        _mainTabs.Selecting += OnMainTabsSelecting;
        _mainTabs.SelectedIndexChanged += (_, _) => ApplySidebarNavHighlight(_mainTabs.SelectedIndex);

        var tabHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 16, 14),
            BackColor = Color.FromArgb(242, 245, 249)
        };
        tabHost.Controls.Add(_mainTabs);
        panel.Controls.Add(tabHost);
        panel.Controls.Add(topbar);
        return panel;
    }

    private static readonly Color SidebarNavBg = Color.FromArgb(38, 43, 48);
    private static readonly Color SidebarNavHover = Color.FromArgb(45, 52, 62);
    private static readonly Color SidebarNavActive = Color.FromArgb(30, 40, 50);

    private Button BuildMenuButton(string text, int targetTab)
    {
        var button = new Button
        {
            Text = text,
            Width = 220,
            Height = 50,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = SidebarNavBg,
            ForeColor = Color.FromArgb(230, 232, 235),
            TextAlign = ContentAlignment.MiddleRight,
            Tag = targetTab,
            Margin = new Padding(0, 0, 0, 10),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 11.5f, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(12, 0, 16, 0),
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(48, 54, 64);
        button.MouseEnter += (_, _) =>
        {
            if (_mainTabs.SelectedIndex == targetTab)
                return;
            button.BackColor = SidebarNavHover;
            button.FlatAppearance.BorderColor = Color.FromArgb(72, 80, 94);
        };
        button.MouseLeave += (_, _) =>
        {
            var active = _mainTabs.SelectedIndex == targetTab;
            button.BackColor = active ? SidebarNavActive : SidebarNavBg;
            button.FlatAppearance.BorderColor = active ? Color.FromArgb(52, 152, 219) : Color.FromArgb(48, 54, 64);
            button.FlatAppearance.BorderSize = active ? 2 : 1;
        };
        button.Click += (_, _) =>
        {
            if (targetTab < 0 || targetTab >= _mainTabs.TabPages.Count) return;
            if (_currentUser.Role == UserRole.Admin)
            {
                if (targetTab < 2) return;
            }
            else if (targetTab > 1)
            {
                return;
            }

            _mainTabs.SelectedIndex = targetTab;
            ApplySidebarNavHighlight(targetTab);
        };
        _sidebarNavButtons.Add(button);
        return button;
    }

    private void ApplySidebarNavHighlight(int selectedTabIndex)
    {
        foreach (var b in _sidebarNavButtons)
        {
            if (b.Tag is not int idx) continue;
            var active = idx == selectedTabIndex;
            b.BackColor = active ? SidebarNavActive : SidebarNavBg;
            b.ForeColor = active ? Color.White : Color.FromArgb(230, 232, 235);
            b.FlatAppearance.BorderColor = active ? Color.FromArgb(52, 152, 219) : Color.FromArgb(48, 54, 64);
            b.FlatAppearance.BorderSize = active ? 2 : 1;
        }
    }

    /// <summary>
    /// Applies <paramref name="preferredDistance"/> once the split container has a real client size.
    /// Large <see cref="SplitContainer.Panel1MinSize"/> / <see cref="SplitContainer.Panel2MinSize"/> values
    /// must not be set on a new control (still at default ~150px); the runtime validates
    /// <see cref="SplitContainer.SplitterDistance"/> against those mins and throws immediately.
    /// </summary>
    private static void ApplyInitialSplitterDistance(SplitContainer split, int preferredDistance) =>
        ApplyInitialSplitterDistance(split, preferredDistance, split.Panel1MinSize, split.Panel2MinSize);

    private static void ApplyInitialSplitterDistance(SplitContainer split, int preferredDistance, int panel1MinSize, int panel2MinSize)
    {
        const int safeMin = 25;
        split.Panel1MinSize = safeMin;
        split.Panel2MinSize = safeMin;

        void OnSized(object? sender, EventArgs e)
        {
            var along = split.Orientation == Orientation.Horizontal ? split.ClientSize.Height : split.ClientSize.Width;
            var usable = along - split.SplitterWidth;
            if (usable < panel1MinSize + panel2MinSize)
                return;

            var maxDist = usable - panel2MinSize;
            var dist = maxDist < panel1MinSize
                ? panel1MinSize
                : Math.Clamp(preferredDistance, panel1MinSize, maxDist);

            try
            {
                split.SplitterDistance = dist;
                split.Panel1MinSize = panel1MinSize;
                split.Panel2MinSize = panel2MinSize;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            split.SizeChanged -= OnSized;
        }

        split.SizeChanged += OnSized;
        OnSized(split, EventArgs.Empty);
    }

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
        return tab;
    }

    private TabPage BuildCatalogTab()
    {
        var tab = new TabPage("الشركات والأصناف");
        tab.BackColor = Color.FromArgb(245, 247, 250);

        _catalogProductTypeCombo.Items.Clear();
        _catalogProductTypeCombo.Items.AddRange(["Oil", "Filter", "Grease", "Other"]);
        _catalogProductTypeCombo.SelectedIndex = 0;
        _catalogProductPackCombo.Items.Clear();
        _catalogProductPackCombo.Items.AddRange(["4L", "5L", "20L", "Unit"]);
        _catalogProductPackCombo.SelectedIndex = 0;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Padding = new Padding(8, 6, 8, 6),
            BackColor = Color.FromArgb(228, 232, 238),
            SplitterWidth = 8
        };
        ApplyInitialSplitterDistance(split, 520, 400, 460);

        var leftRoot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12, 12, 16, 12) };
        leftRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        // Must fit company fields + toolbar (BuildButton height 46 + padding); a too-short row clips captions.
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 172));
        leftRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var leftTitle = new Label
        {
            Text = "الشركات (المورّد)",
            Dock = DockStyle.Fill,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.MiddleRight
        };

        var companyForm = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(0, 6, 0, 4) };
        companyForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
        companyForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        companyForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        companyForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        companyForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        companyForm.Controls.Add(new Label
        {
            Text = "اسم الشركة",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextPrimary,
            Padding = new Padding(0, 8, 12, 0)
        }, 0, 0);
        _catalogCompanyNameEdit.MinimumSize = new Size(220, 36);
        companyForm.Controls.Add(_catalogCompanyNameEdit, 1, 0);
        _catalogCompanyActiveEdit.Padding = new Padding(0, 6, 0, 0);
        companyForm.Controls.Add(_catalogCompanyActiveEdit, 1, 1);
        var companyBtns = new FlowLayoutPanel
        {
            // Same pattern as Main Warehouse: top-docked strip with explicit height (avoids TableLayout % row clipping).
            Dock = DockStyle.Top,
            Height = 62,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        var addCo = BuildButton("إضافة شركة", Color.FromArgb(39, 174, 96));
        addCo.Click += async (_, _) => await SaveCatalogCompanyAsync(createNew: true);
        var saveCo = BuildButton("حفظ", Color.FromArgb(243, 156, 18));
        saveCo.Click += async (_, _) => await SaveCatalogCompanyAsync(createNew: false);
        var newCo = BuildButton("جديد", Color.FromArgb(52, 73, 94));
        newCo.Click += (_, _) => ClearCatalogCompanyForm();
        var refAll = BuildButton("تحديث الكل", Color.FromArgb(52, 152, 219));
        refAll.Click += async (_, _) => await RefreshCatalogGridsAsync();
        companyBtns.Controls.Add(addCo);
        companyBtns.Controls.Add(saveCo);
        companyBtns.Controls.Add(newCo);
        companyBtns.Controls.Add(refAll);
        companyForm.SetColumnSpan(companyBtns, 2);
        companyForm.Controls.Add(companyBtns, 0, 2);

        StyleGrid(_catalogCompaniesGrid);
        ConfigureCatalogCompaniesColumns();
        _catalogCompaniesGrid.AllowUserToAddRows = false;
        _catalogCompaniesGrid.SelectionChanged += (_, _) =>
        {
            LoadSelectedCatalogCompanyRow();
            _ = RefreshCatalogProductsGridAsync();
        };

        leftRoot.Controls.Add(leftTitle, 0, 0);
        leftRoot.Controls.Add(companyForm, 0, 1);
        leftRoot.Controls.Add(_catalogCompaniesGrid, 0, 2);

        var rightRoot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16, 12, 12, 12) };
        rightRoot.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rightRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        rightRoot.RowStyles.Add(new RowStyle(SizeType.Absolute, 218));
        rightRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var rightTitle = new Label
        {
            Text = "أصناف الشركة المحددة",
            Dock = DockStyle.Fill,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.MiddleRight
        };

        var productForm = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4, Padding = new Padding(0, 6, 0, 4) };
        productForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        productForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
        productForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        productForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
        productForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        productForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        productForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        productForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        productForm.Controls.Add(new Label
        {
            Text = "اسم الصنف",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextPrimary,
            Padding = new Padding(0, 8, 12, 0)
        }, 0, 0);
        _catalogProductNameEdit.MinimumSize = new Size(200, 36);
        productForm.SetColumnSpan(_catalogProductNameEdit, 3);
        productForm.Controls.Add(_catalogProductNameEdit, 1, 0);
        productForm.Controls.Add(new Label
        {
            Text = "النوع",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextPrimary,
            Padding = new Padding(0, 6, 10, 0)
        }, 0, 1);
        _catalogProductTypeCombo.MinimumSize = new Size(108, 32);
        productForm.Controls.Add(_catalogProductTypeCombo, 1, 1);
        productForm.Controls.Add(new Label
        {
            Text = "العبوة",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextPrimary,
            Padding = new Padding(0, 6, 10, 0)
        }, 2, 1);
        _catalogProductPackCombo.MinimumSize = new Size(96, 32);
        productForm.Controls.Add(_catalogProductPackCombo, 3, 1);
        _catalogProductActiveEdit.Margin = new Padding(0, 6, 0, 0);
        productForm.SetColumnSpan(_catalogProductActiveEdit, 3);
        productForm.Controls.Add(_catalogProductActiveEdit, 1, 2);
        var productBtns = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 64,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        var addPr = BuildButton("إضافة صنف", Color.FromArgb(39, 174, 96));
        addPr.Click += async (_, _) => await SaveCatalogProductAsync(createNew: true);
        var savePr = BuildButton("حفظ الصنف", Color.FromArgb(243, 156, 18));
        savePr.Click += async (_, _) => await SaveCatalogProductAsync(createNew: false);
        var newPr = BuildButton("صنف جديد", Color.FromArgb(52, 73, 94));
        newPr.Click += (_, _) => ClearCatalogProductForm();
        productBtns.Controls.Add(addPr);
        productBtns.Controls.Add(savePr);
        productBtns.Controls.Add(newPr);
        productForm.SetColumnSpan(productBtns, 4);
        productForm.Controls.Add(productBtns, 0, 3);

        StyleGrid(_catalogProductsGrid);
        ConfigureCatalogProductsColumns();
        _catalogProductsGrid.AllowUserToAddRows = false;
        _catalogProductsGrid.SelectionChanged += (_, _) => LoadSelectedCatalogProductRow();

        rightRoot.Controls.Add(rightTitle, 0, 0);
        rightRoot.Controls.Add(productForm, 0, 1);
        rightRoot.Controls.Add(_catalogProductsGrid, 0, 2);

        split.Panel1.Controls.Add(leftRoot);
        split.Panel2.Controls.Add(rightRoot);

        var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 16, 18, 14), BackColor = Color.FromArgb(245, 247, 250) };
        var hint = new Label
        {
            Text =
                "أضف الشركة أولاً، ثم اخترها في الجدول وأضف أصنافها (زيت / شحوم / …). المخزون يُستلم من تبويب المستودع الرئيسي. تفعيل الشركة/الصنف يؤثر على ظهوره في القوائم.",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(1150, 0),
            Margin = new Padding(0, 0, 0, 12),
            ForeColor = UiTextSecondary,
            Font = UiFont,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false
        };
        wrap.Controls.Add(hint);
        wrap.Controls.Add(split);
        tab.Controls.Add(wrap);
        return tab;
    }

    private void ConfigureCatalogCompaniesColumns()
    {
        _catalogCompaniesGrid.AutoGenerateColumns = false;
        _catalogCompaniesGrid.Columns.Clear();
        _catalogCompaniesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _catalogCompaniesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CatalogCompanyRow.Name), HeaderText = "الشركة", FillWeight = 50, ReadOnly = true });
        _catalogCompaniesGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(CatalogCompanyRow.IsActive), HeaderText = "نشط", FillWeight = 14, ReadOnly = true });
        _catalogCompaniesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CatalogCompanyRow.ProductCount), HeaderText = "عدد الأصناف", FillWeight = 18, ReadOnly = true });
    }

    private void ConfigureCatalogProductsColumns()
    {
        _catalogProductsGrid.AutoGenerateColumns = false;
        _catalogProductsGrid.Columns.Clear();
        _catalogProductsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _catalogProductsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CatalogProductRow.Name), HeaderText = "الصنف", FillWeight = 42, ReadOnly = true });
        _catalogProductsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CatalogProductRow.ProductCategory), HeaderText = "النوع", FillWeight = 24, ReadOnly = true });
        _catalogProductsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CatalogProductRow.PackageSize), HeaderText = "العبوة", FillWeight = 20, ReadOnly = true });
        _catalogProductsGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(CatalogProductRow.IsActive), HeaderText = "نشط", FillWeight = 14, ReadOnly = true });
    }

    private async Task RefreshCatalogGridsAsync()
    {
        if (_currentUser.Role != UserRole.Admin)
            return;

        await RefreshCompanyComboBoxesAsync();

        await using var db = await _dbFactory.CreateDbContextAsync();
        var companies = await db.Companies.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CatalogCompanyRow
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = c.IsActive,
                ProductCount = db.Products.Count(p => p.CompanyId == c.Id)
            })
            .ToListAsync();

        _suppressCatalogCompanyLoad = true;
        try
        {
            _catalogCompaniesGrid.DataSource = companies;
            if (companies.Count > 0)
            {
                var selectId = _selectedCatalogCompanyId;
                _catalogCompaniesGrid.ClearSelection();
                if (selectId is > 0)
                {
                    for (var i = 0; i < _catalogCompaniesGrid.Rows.Count; i++)
                    {
                        if (_catalogCompaniesGrid.Rows[i].DataBoundItem is CatalogCompanyRow r && r.Id == selectId.Value)
                        {
                            _catalogCompaniesGrid.Rows[i].Selected = true;
                            _catalogCompaniesGrid.CurrentCell = _catalogCompaniesGrid.Rows[i].Cells[0];
                            break;
                        }
                    }
                }

                if (_catalogCompaniesGrid.CurrentRow is null)
                {
                    var first = _catalogCompaniesGrid.Rows[0];
                    first.Selected = true;
                    _catalogCompaniesGrid.CurrentCell = first.Cells[0];
                }
            }
        }
        finally
        {
            _suppressCatalogCompanyLoad = false;
        }

        if (companies.Count == 0)
            ClearCatalogCompanyForm();
        else
            LoadSelectedCatalogCompanyRow();

        await RefreshCatalogProductsGridAsync();
        await RefreshAllStockViewsAsync();
    }

    private async Task RefreshCatalogProductsGridAsync()
    {
        if (_currentUser.Role != UserRole.Admin)
            return;

        if (!_selectedCatalogCompanyId.HasValue)
        {
            _catalogProductsGrid.DataSource = Array.Empty<CatalogProductRow>();
            ClearCatalogProductForm();
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var cid = _selectedCatalogCompanyId.Value;
        var products = await db.Products.AsNoTracking()
            .Where(p => p.CompanyId == cid)
            .OrderBy(p => p.Name)
            .Select(p => new CatalogProductRow
            {
                Id = p.Id,
                CompanyId = p.CompanyId,
                Name = p.Name,
                ProductCategory = p.ProductCategory,
                PackageSize = p.PackageSize,
                IsActive = p.IsActive
            })
            .ToListAsync();

        _suppressCatalogProductLoad = true;
        try
        {
            _catalogProductsGrid.DataSource = products;
            if (products.Count > 0)
            {
                var selectId = _selectedCatalogProductId;
                _catalogProductsGrid.ClearSelection();
                if (selectId is > 0)
                {
                    for (var i = 0; i < _catalogProductsGrid.Rows.Count; i++)
                    {
                        if (_catalogProductsGrid.Rows[i].DataBoundItem is CatalogProductRow r && r.Id == selectId.Value)
                        {
                            _catalogProductsGrid.Rows[i].Selected = true;
                            _catalogProductsGrid.CurrentCell = _catalogProductsGrid.Rows[i].Cells[0];
                            break;
                        }
                    }
                }

                if (_catalogProductsGrid.CurrentRow is null)
                {
                    var first = _catalogProductsGrid.Rows[0];
                    first.Selected = true;
                    _catalogProductsGrid.CurrentCell = first.Cells[0];
                }
            }
        }
        finally
        {
            _suppressCatalogProductLoad = false;
        }

        if (products.Count == 0)
            ClearCatalogProductForm();
        else
            LoadSelectedCatalogProductRow();
    }

    private void LoadSelectedCatalogCompanyRow()
    {
        if (_suppressCatalogCompanyLoad) return;
        if (_catalogCompaniesGrid.CurrentRow is null || _catalogCompaniesGrid.CurrentRow.IsNewRow
                                                     || _catalogCompaniesGrid.CurrentRow.DataBoundItem is not CatalogCompanyRow row)
        {
            _selectedCatalogCompanyId = null;
            return;
        }

        _selectedCatalogCompanyId = row.Id;
        _catalogCompanyNameEdit.Text = row.Name;
        _catalogCompanyActiveEdit.Checked = row.IsActive;
    }

    private void LoadSelectedCatalogProductRow()
    {
        if (_suppressCatalogProductLoad) return;
        if (_catalogProductsGrid.CurrentRow is null || _catalogProductsGrid.CurrentRow.IsNewRow
                                                     || _catalogProductsGrid.CurrentRow.DataBoundItem is not CatalogProductRow row)
        {
            _selectedCatalogProductId = null;
            return;
        }

        _selectedCatalogProductId = row.Id;
        _catalogProductNameEdit.Text = row.Name;
        SelectMainWarehouseComboValue(_catalogProductTypeCombo, row.ProductCategory);
        SelectMainWarehouseComboValue(_catalogProductPackCombo, row.PackageSize);
        _catalogProductActiveEdit.Checked = row.IsActive;
    }

    private void ClearCatalogCompanyForm()
    {
        _suppressCatalogCompanyLoad = true;
        try
        {
            _selectedCatalogCompanyId = null;
            _catalogCompanyNameEdit.Clear();
            _catalogCompanyActiveEdit.Checked = true;
            _catalogCompaniesGrid.ClearSelection();
        }
        finally
        {
            _suppressCatalogCompanyLoad = false;
        }
    }

    private void ClearCatalogProductForm()
    {
        _suppressCatalogProductLoad = true;
        try
        {
            _selectedCatalogProductId = null;
            _catalogProductNameEdit.Clear();
            if (_catalogProductTypeCombo.Items.Count > 0) _catalogProductTypeCombo.SelectedIndex = 0;
            if (_catalogProductPackCombo.Items.Count > 0) _catalogProductPackCombo.SelectedIndex = 0;
            _catalogProductActiveEdit.Checked = true;
            _catalogProductsGrid.ClearSelection();
        }
        finally
        {
            _suppressCatalogProductLoad = false;
        }
    }

    private async Task SaveCatalogCompanyAsync(bool createNew)
    {
        if (_currentUser.Role != UserRole.Admin) return;

        var name = _catalogCompanyNameEdit.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("أدخل اسم الشركة.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        try
        {
            if (createNew)
            {
                if (await db.Companies.AnyAsync(c => c.Name == name))
                {
                    MessageBox.Show("هذه الشركة موجودة بالفعل.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                    return;
                }

                var created = new Domain.Company { Name = name, IsActive = _catalogCompanyActiveEdit.Checked };
                db.Companies.Add(created);
                await db.SaveChangesAsync();
                _selectedCatalogCompanyId = created.Id;
                MessageBox.Show("تمت إضافة الشركة.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            }
            else
            {
                if (!_selectedCatalogCompanyId.HasValue)
                {
                    MessageBox.Show("اختر شركة من الجدول أو استخدم «إضافة شركة».", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                    return;
                }

                var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == _selectedCatalogCompanyId.Value);
                if (company is null) return;
                if (!string.Equals(company.Name, name, StringComparison.Ordinal) &&
                    await db.Companies.AnyAsync(c => c.Name == name && c.Id != company.Id))
                {
                    MessageBox.Show("اسم الشركة مستخدم من شركة أخرى.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                    return;
                }

                company.Name = name;
                company.IsActive = _catalogCompanyActiveEdit.Checked;
                await db.SaveChangesAsync();
                MessageBox.Show("تم حفظ الشركة.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            }

            await RefreshCatalogGridsAsync();
        }
        catch (DbUpdateException ex)
        {
            MessageBox.Show(ex.InnerException?.Message ?? ex.Message, "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }

    private async Task SaveCatalogProductAsync(bool createNew)
    {
        if (_currentUser.Role != UserRole.Admin) return;

        if (!_selectedCatalogCompanyId.HasValue)
        {
            MessageBox.Show("اختر صف شركة من الجدول الأيسر أولاً.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var pname = _catalogProductNameEdit.Text.Trim();
        if (string.IsNullOrWhiteSpace(pname))
        {
            MessageBox.Show("أدخل اسم الصنف.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var category = _catalogProductTypeCombo.Text;
        var package = _catalogProductPackCombo.Text;
        var companyId = _selectedCatalogCompanyId.Value;

        await using var db = await _dbFactory.CreateDbContextAsync();
        try
        {
            if (createNew)
            {
                if (await db.Products.AnyAsync(p =>
                        p.CompanyId == companyId && p.Name == pname && p.ProductCategory == category && p.PackageSize == package))
                {
                    MessageBox.Show("هذا الصنف موجود بالفعل لهذه الشركة.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                    return;
                }

                var product = new Domain.Product
                {
                    CompanyId = companyId,
                    Name = pname,
                    ProductCategory = category,
                    PackageSize = package,
                    UnitPrice = 0,
                    IsActive = _catalogProductActiveEdit.Checked
                };
                db.Products.Add(product);
                await db.SaveChangesAsync();
                _selectedCatalogProductId = product.Id;
                MessageBox.Show("تمت إضافة الصنف.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            }
            else
            {
                if (!_selectedCatalogProductId.HasValue)
                {
                    MessageBox.Show("اختر صنفاً من الجدول أو استخدم «إضافة صنف».", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                    return;
                }

                var product = await db.Products.FirstOrDefaultAsync(p => p.Id == _selectedCatalogProductId.Value);
                if (product is null || product.CompanyId != companyId)
                {
                    MessageBox.Show("صنف غير صالح.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                    return;
                }

                if (await db.Products.AnyAsync(p =>
                        p.CompanyId == companyId && p.Name == pname && p.ProductCategory == category && p.PackageSize == package &&
                        p.Id != product.Id))
                {
                    MessageBox.Show("هناك صنف آخر بنفس الاسم والنوع والعبوة.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                    return;
                }

                product.Name = pname;
                product.ProductCategory = category;
                product.PackageSize = package;
                product.IsActive = _catalogProductActiveEdit.Checked;
                await db.SaveChangesAsync();
                MessageBox.Show("تم حفظ الصنف.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            }

            await RefreshCatalogGridsAsync();
        }
        catch (DbUpdateException ex)
        {
            MessageBox.Show(ex.InnerException?.Message ?? ex.Message, "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }

    private async Task RefreshCompanyComboBoxesAsync()
    {
        await using var dbCo = await _dbFactory.CreateDbContextAsync();
        var companies = await dbCo.Companies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CompanyListItem { Id = c.Id, Name = c.Name })
            .ToListAsync();

        _newProductCompanyCombo.DataSource = null;
        _newProductCompanyCombo.DisplayMember = nameof(CompanyListItem.Name);
        _newProductCompanyCombo.ValueMember = nameof(CompanyListItem.Id);
        _newProductCompanyCombo.DataSource = companies;
        if (companies.Count > 0)
            _newProductCompanyCombo.SelectedIndex = 0;
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

        var topBar = new Panel { Dock = DockStyle.Top, Height = 92, Padding = new Padding(12, 8, 12, 0) };
        var runAudit = BuildButton("تنفيذ الجرد وترحيل الفروقات", Color.FromArgb(142, 68, 173));
        runAudit.Left = 0;
        runAudit.Top = 8;
        runAudit.Click += async (_, _) => await RunAuditAsync();

        var hint = new Label
        {
            AutoSize = false,
            Width = 720,
            Height = 44,
            Left = 0,
            Top = 48,
            ForeColor = Color.FromArgb(52, 73, 94),
            Text = "المستودع كما في تبويب المخزون (أو فرع البيع، ثم الرئيسي). أدخل الكمية الفعلية لكل صنف واختر السبب عند الفارق (يُرحّل كحركة تسوية). اللوحة السفلية: سجل للقراءة فقط.",
            RightToLeft = RightToLeft.Yes
        };

        topBar.Controls.Add(hint);
        topBar.Controls.Add(runAudit);

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

    private TabPage BuildBranchesAdminTab()
    {
        var tab = new TabPage("الفروع");
        tab.BackColor = Color.FromArgb(245, 247, 250);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(22, 20, 22, 18),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // title
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // subtitle
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // form + buttons (full height — no clipping)
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // grid

        var title = new Label
        {
            Text = "إدارة الفروع (مرجع)",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
            Font = UiFontTitle,
            ForeColor = UiTextPrimary,
            RightToLeft = RightToLeft.Yes
        };
        root.Controls.Add(title, 0, 0);

        var subtitle = new Label
        {
            Text = "إضافة وتعديل وتعطيل فروع البيع فقط. المستودع الرئيسي يُدار تلقائياً ولا يُعرض هنا.",
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Margin = new Padding(0, 0, 0, 16),
            ForeColor = UiTextSecondary,
            Font = UiFont,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes
        };
        root.Controls.Add(subtitle, 0, 1);

        var formHost = new Panel { Dock = DockStyle.Fill };
        var formFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 14, 12, 14),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        void SyncBranchFormWidth(object? _, EventArgs __)
        {
            var w = formHost.ClientSize.Width;
            if (w > 0)
                formFlow.Width = w;
        }
        formHost.Resize += SyncBranchFormWidth;
        formHost.HandleCreated += SyncBranchFormWidth;
        _branchName.MinimumSize = new Size(360, 36);
        var nameRow = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false, Padding = new Padding(0, 0, 0, 6) };
        nameRow.Controls.Add(_branchName);
        nameRow.Controls.Add(new Label { Text = "اسم الفرع", AutoSize = true, Padding = new Padding(12, 10, 0, 0), Font = UiFontCaption, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes });

        formFlow.Controls.Add(nameRow);
        formFlow.Controls.Add(_branchActive);

        var btnRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0),
            WrapContents = true,
            Padding = new Padding(0, 4, 0, 0)
        };
        var addBranch = BuildButton("إضافة فرع", Color.FromArgb(39, 174, 96));
        addBranch.Click += async (_, _) => await SaveBranchAsync(createNew: true);
        var saveBranch = BuildButton("حفظ التعديل", Color.FromArgb(243, 156, 18));
        saveBranch.Click += async (_, _) => await SaveBranchAsync(createNew: false);
        var newBranch = BuildButton("جديد", Color.FromArgb(52, 73, 94));
        newBranch.Click += (_, _) => ClearBranchForm();
        var refreshBranches = BuildButton("تحديث القائمة", Color.FromArgb(52, 152, 219));
        refreshBranches.Click += async (_, _) =>
        {
            await RefreshBranchesGridAsync();
            await LoadWarehousesAsync();
        };
        btnRow.Controls.Add(refreshBranches);
        btnRow.Controls.Add(newBranch);
        btnRow.Controls.Add(saveBranch);
        btnRow.Controls.Add(addBranch);
        formFlow.Controls.Add(btnRow);
        formHost.Controls.Add(formFlow);
        root.Controls.Add(formHost, 0, 2);

        StyleGrid(_branchesGrid);
        ConfigureBranchesAdminColumns();
        _branchesGrid.Dock = DockStyle.Fill;
        _branchesGrid.Margin = new Padding(0, 12, 0, 0);
        _branchesGrid.SelectionChanged += (_, _) => LoadSelectedBranchRow();
        root.Controls.Add(_branchesGrid, 0, 3);

        tab.Controls.Add(root);
        return tab;
    }

    private void ConfigureBranchesAdminColumns()
    {
        _branchesGrid.AutoGenerateColumns = false;
        _branchesGrid.Columns.Clear();
        _branchesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _branchesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WarehouseDto.Name),
            HeaderText = "اسم الفرع",
            FillWeight = 62,
            ReadOnly = true
        });
        _branchesGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(WarehouseDto.IsActive),
            HeaderText = "نشط",
            FillWeight = 18,
            ReadOnly = true
        });
    }

    private async Task RefreshBranchesGridAsync()
    {
        if (_currentUser.Role != UserRole.Admin)
            return;
        var list = await _warehouseService.ListBranchesForAdminAsync();
        _suppressBranchRowLoad = true;
        try
        {
            _branchesGrid.DataSource = list;
            if (list.Count > 0)
            {
                _branchesGrid.ClearSelection();
                var first = _branchesGrid.Rows[0];
                first.Selected = true;
                _branchesGrid.CurrentCell = first.Cells[0];
            }
        }
        finally
        {
            _suppressBranchRowLoad = false;
        }
        if (list.Count == 0)
            ClearBranchForm();
        else
            LoadSelectedBranchRow();
    }

    private void LoadSelectedBranchRow()
    {
        if (_suppressBranchRowLoad) return;
        if (_branchesGrid.CurrentRow is null || _branchesGrid.CurrentRow.IsNewRow
                                             || _branchesGrid.CurrentRow.DataBoundItem is not WarehouseDto row)
        {
            _selectedBranchId = null;
            return;
        }

        _selectedBranchId = row.Id;
        _branchName.Text = row.Name;
        _branchActive.Checked = row.IsActive;
    }

    private void ClearBranchForm()
    {
        _suppressBranchRowLoad = true;
        try
        {
            _selectedBranchId = null;
            _branchName.Clear();
            _branchActive.Checked = true;
            _branchesGrid.ClearSelection();
        }
        finally
        {
            _suppressBranchRowLoad = false;
        }
    }

    private async Task SaveBranchAsync(bool createNew)
    {
        if (_currentUser.Role != UserRole.Admin)
            return;

        var name = _branchName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("أدخل اسم الفرع.", "الفروع", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        try
        {
            if (createNew)
            {
                await _warehouseService.CreateBranchAsync(name, _currentUser.Id);
                MessageBox.Show("تمت إضافة الفرع.", "الفروع", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            }
            else
            {
                if (!_selectedBranchId.HasValue)
                {
                    MessageBox.Show("اختر فرعاً من الجدول للتعديل، أو استخدم «إضافة فرع» لفرع جديد.", "الفروع",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                    return;
                }

                await _warehouseService.UpdateBranchAsync(_selectedBranchId.Value, name, _branchActive.Checked, _currentUser.Id);
                MessageBox.Show("تم حفظ التعديلات.", "الفروع", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            }

            await RefreshBranchesGridAsync();
            await LoadWarehousesAsync();
            UpdatePosStockLocationLabel();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "الفروع", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MsgRtl);
        }
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

    private TabPage BuildTransferTab()
    {
        var tab = new TabPage("التحويلات");
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 24, 20), BackColor = Color.FromArgb(245, 247, 250), RightToLeft = RightToLeft.Yes, AutoScroll = true };
        var title = new Label { Text = "تحويل بين المستودعات", Dock = DockStyle.Top, Height = 40, Font = UiFontSection, ForeColor = UiTextPrimary, TextAlign = ContentAlignment.TopRight, RightToLeft = RightToLeft.Yes, Padding = new Padding(0, 0, 0, 6) };
        var mainWarehouseHint = new Label
        {
            Text =
                "المستودع الرئيسي يستقبل المشتريات؛ لكل فرع رصيده. التحويل من الرئيسي للفرع يستهلك أقدم تاريخ إنتاج أولاً (FEFO) وقد يظهر عدة أسطر في السجل. التحويل بين الفروع غير مسموح. اختر «من المستودع» أولاً؛ الأصناف المعروضة لها رصيد هناك.",
            Dock = DockStyle.Top,
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Margin = new Padding(0, 0, 0, 18),
            ForeColor = UiTextSecondary,
            Font = new Font(UiFont, FontStyle.Italic),
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false
        };
        _transferFromWarehouseCombo.SelectedIndexChanged += async (_, _) => await RefreshTransferProductsAsync();
        _transferProductCombo.SelectedIndexChanged += (_, _) => SyncTransferQtyLimitFromSelection();
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 5,
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
        form.Controls.Add(transferButton, 1, 4);

        root.Controls.Add(form);
        root.Controls.Add(mainWarehouseHint);
        root.Controls.Add(title);
        tab.Controls.Add(root);
        return tab;
    }

    private TabPage BuildReportsTab()
    {
        var tab = new TabPage("التقارير");
        tab.BackColor = Color.FromArgb(245, 247, 250);
        tab.RightToLeft = RightToLeft.Yes;
        _reportFromPicker.Value = DateTime.Today.AddDays(-7);
        _reportToPicker.Value = DateTime.Today;
        foreach (var p in new[] { _reportFromPicker, _reportToPicker })
        {
            p.Font = UiFont;
            p.Format = DateTimePickerFormat.Custom;
            p.CustomFormat = "yyyy/MM/dd";
            p.RightToLeft = RightToLeft.Yes;
        }

        _reportWarehouseCombo.RightToLeft = RightToLeft.Yes;
        foreach (var g in new[]
                 {
                     _salesByWarehouseGrid, _topProductsGrid, _slowMovingGrid, _reportLowStockGrid, _transferReportGrid,
                     _warehouseInventoryGrid, _profitByInvoiceGrid, _profitByProductGrid, _stockFromMovementsGrid, _stockHistoryGrid,
                     _transferFullGrid, _cashFlowGrid, _expenseReportGrid
                 })
            g.RightToLeft = RightToLeft.Yes;
        _profitAllBranchesCheck.Visible = _currentUser.Role == UserRole.Admin;
        _expenseDateInput.Value = DateTime.Today;
        _expenseSaveButton.Enabled = _currentUser.Role == UserRole.Admin;
        _expenseSaveButton.Click += async (_, _) => await SaveExpenseFromReportsAsync();

        var brTitle = UiFontTitle;
        var brSub = UiFont;
        var lblFont = UiFontCaption;
        var refresh = BuildButton("تحديث", Color.FromArgb(41, 128, 185));
        refresh.Click += async (_, _) => await RefreshReportsAsync();

        // Single auto-sizing chrome: avoids clipped RTL subtitles and cramped fixed-height toolbars.
        var reportsChrome = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(16, 12, 16, 14),
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes
        };
        reportsChrome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        reportsChrome.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reportsChrome.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reportsChrome.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        reportsChrome.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reportsChrome.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var hdrTitleLbl = new Label
        {
            Text = "التحليلات والتقارير",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = brTitle,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 0, 0, 6),
            UseCompatibleTextRendering = false
        };
        var hdrSubLbl = new Label
        {
            Text = "أداء الفروع، تقدير الهامش، حركة الأصناف، التحويلات، والمخزون على مستوى الشركة.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = brSub,
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 0, 0, 12),
            UseCompatibleTextRendering = false
        };
        var toolbarHintLbl = new Label
        {
            Text = "البطاقات وقوائم المنتجات أدناه للمستودع المحدد. جدول المقارنة يشمل كل المواقع (صفر طبيعي إن لم تُستخدم نقطة البيع بعد هناك).",
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = UiTextSecondary,
            Font = new Font(UiFont, FontStyle.Italic),
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 4, 0, 10),
            UseCompatibleTextRendering = false
        };

        void SyncReportsChromeWrapWidths()
        {
            var w = Math.Max(320, reportsChrome.ClientSize.Width - reportsChrome.Padding.Horizontal);
            hdrSubLbl.MaximumSize = new Size(w, 0);
            toolbarHintLbl.MaximumSize = new Size(w, 0);
        }
        reportsChrome.HandleCreated += (_, _) => SyncReportsChromeWrapWidths();
        reportsChrome.Resize += (_, _) => SyncReportsChromeWrapWidths();

        var filterFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            RightToLeft = RightToLeft.Yes,
            WrapContents = true,
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(0, 0, 0, 0),
            AutoScroll = false,
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle
        };
        var lblFrom = new Label
        {
            Text = "من",
            AutoSize = true,
            Font = lblFont,
            ForeColor = UiTextPrimary,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(8, 12, 6, 8),
            UseCompatibleTextRendering = false
        };
        var lblTo = new Label
        {
            Text = "إلى",
            AutoSize = true,
            Font = lblFont,
            ForeColor = UiTextPrimary,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(8, 12, 6, 8),
            UseCompatibleTextRendering = false
        };
        var lblWh = new Label
        {
            Text = "المستودع المحدد",
            AutoSize = true,
            Font = lblFont,
            ForeColor = UiTextPrimary,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(8, 12, 12, 8),
            UseCompatibleTextRendering = false
        };

        _reportFromPicker.Margin = new Padding(4, 8, 4, 8);
        _reportFromPicker.Width = 160;
        _reportToPicker.Margin = new Padding(4, 8, 4, 8);
        _reportToPicker.Width = 160;
        _reportWarehouseCombo.Width = 268;
        _reportWarehouseCombo.Margin = new Padding(4, 6, 12, 8);
        refresh.Margin = new Padding(16, 6, 8, 8);

        filterFlow.Controls.Add(refresh);
        filterFlow.Controls.Add(_reportWarehouseCombo);
        filterFlow.Controls.Add(lblWh);
        filterFlow.Controls.Add(_reportToPicker);
        filterFlow.Controls.Add(lblTo);
        filterFlow.Controls.Add(_reportFromPicker);
        filterFlow.Controls.Add(lblFrom);

        const int reportModuleRowHeight = 54;
        var exportExcelBtn = BuildSizedButton("تصدير Excel — جميع التحليلات", Color.FromArgb(39, 174, 96), 220, reportModuleRowHeight);
        exportExcelBtn.Click += async (_, _) => await ExportAnalyticsReportsToExcelAsync();
        exportExcelBtn.Margin = new Padding(8, 4, 8, 4);
        exportExcelBtn.RightToLeft = RightToLeft.Yes;
        // Single RTL row: equal-width module pills from the visual start (right), then Excel. Horizontal scroll if narrow.
        var exportAndNavRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(4, 4, 4, 4),
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };

        reportsChrome.Controls.Add(hdrTitleLbl, 0, 0);
        reportsChrome.Controls.Add(hdrSubLbl, 0, 1);
        reportsChrome.Controls.Add(filterFlow, 0, 2);
        reportsChrome.Controls.Add(toolbarHintLbl, 0, 3);
        reportsChrome.Controls.Add(exportAndNavRow, 0, 4);

        var kpiFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 124,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(14, 8, 14, 10),
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes,
            FlowDirection = FlowDirection.RightToLeft
        };
        kpiFlow.Controls.Add(BuildAnalyticsKpiCard("صافي المبيعات", _kpiNetSalesVal, Color.FromArgb(39, 174, 96), true));
        kpiFlow.Controls.Add(BuildAnalyticsKpiCard("عدد الفواتير", _kpiInvoicesVal, Color.FromArgb(52, 152, 219), true));
        kpiFlow.Controls.Add(BuildAnalyticsKpiCard("متوسط قيمة الفاتورة", _kpiAvgTicketVal, Color.FromArgb(22, 160, 133), true));
        kpiFlow.Controls.Add(BuildAnalyticsKpiCard("إجمالي الربح التقديري", _kpiEstProfitVal, Color.FromArgb(155, 89, 182), true));
        kpiFlow.Controls.Add(BuildAnalyticsKpiCard("قيمة المخزون (الموقع المحدد)", _kpiStockValueVal, Color.FromArgb(230, 126, 34), true));
        kpiFlow.Controls.Add(BuildAnalyticsKpiCard("أصناف منخفضة المخزون", _kpiLowStockVal, Color.FromArgb(192, 57, 43), true));

        _reportMetricsFootnote.Text =
            "يُحسب الهامش بمتوسط تكلفة الشراء المرجح في المستودع الرئيسي لكل منتج (تقدير). الخصومات تُنقص صافي المبيعات. قارن الفروع في الجدول، ثم راجع الأكثر مبيعاً والبطيئة الحركة والتحويلات.";
        _reportMetricsFootnote.Height = 52;
        _reportMetricsFootnote.Padding = new Padding(18, 10, 18, 10);

        var branchPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 4, 18, 12), RightToLeft = RightToLeft.Yes };
        var branchTitle = new Label
        {
            Text = "١ · مقارنة المبيعات · كل المستودعات",
            Dock = DockStyle.Top,
            Height = 30,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight
        };
        StyleReportGrid(_salesByWarehouseGrid);
        ConfigureSalesByWarehouseReportColumns();
        branchPanel.Controls.Add(_salesByWarehouseGrid);
        branchPanel.Controls.Add(branchTitle);

        var summaryBlock = new Panel { Dock = DockStyle.Top, Height = 428, RightToLeft = RightToLeft.Yes };
        _reportPeriodBanner.Dock = DockStyle.Top;
        kpiFlow.Dock = DockStyle.Top;
        _reportMetricsFootnote.Dock = DockStyle.Top;
        branchPanel.Dock = DockStyle.Fill;
        summaryBlock.Controls.Add(branchPanel);
        summaryBlock.Controls.Add(_reportMetricsFootnote);
        summaryBlock.Controls.Add(kpiFlow);
        summaryBlock.Controls.Add(_reportPeriodBanner);

        var topProductsContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), RightToLeft = RightToLeft.Yes };
        var topTitle = new Label
        {
            Text = "٢ · الأكثر مبيعاً (المستودع المحدد)",
            Dock = DockStyle.Top,
            Height = 28,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight
        };
        StyleReportGrid(_topProductsGrid);
        ConfigureTopProductsReportColumns();
        topProductsContainer.Controls.Add(_topProductsGrid);
        topProductsContainer.Controls.Add(topTitle);

        var slowPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), RightToLeft = RightToLeft.Yes };
        var slowTitle = new Label
        {
            Text = "٣ · بطيئة الحركة · أعد الطلب أو انقل المخزون (رصيد ≥ 1، مبيعات < 1 في المستودع المحدد خلال الفترة)",
            Dock = DockStyle.Top,
            Height = 28,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight
        };
        StyleReportGrid(_slowMovingGrid);
        ConfigureSlowMoversReportColumns();
        slowPanel.Controls.Add(_slowMovingGrid);
        slowPanel.Controls.Add(slowTitle);

        var midSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, RightToLeft = RightToLeft.Yes };
        ApplyInitialSplitterDistance(midSplit, 380);
        midSplit.Panel1.Controls.Add(topProductsContainer);
        midSplit.Panel2.Controls.Add(slowPanel);

        var lowStockPanel = new Panel { Dock = DockStyle.Top, Height = 220, Padding = new Padding(12, 4, 12, 8), RightToLeft = RightToLeft.Yes };
        var lowTitle = new Label
        {
            Text = "٤ · مراقبة إعادة الطلب · أصناف عند الحد أو أقل (المستودع المحدد)",
            Dock = DockStyle.Top,
            Height = 28,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight
        };
        StyleReportGrid(_reportLowStockGrid);
        ConfigureReportLowStockColumns();
        lowStockPanel.Controls.Add(_reportLowStockGrid);
        lowStockPanel.Controls.Add(lowTitle);

        var transferPanel = new Panel { Dock = DockStyle.Top, Height = 220, Padding = new Padding(12, 4, 12, 8), RightToLeft = RightToLeft.Yes };
        var xferTitle = new Label
        {
            Text = "٥ · التحويلات · وارد أو صادر للمستودع المحدد خلال الفترة",
            Dock = DockStyle.Top,
            Height = 28,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight
        };
        StyleReportGrid(_transferReportGrid);
        ConfigureTransferReportColumns(_transferReportGrid);
        transferPanel.Controls.Add(_transferReportGrid);
        transferPanel.Controls.Add(xferTitle);

        var warehouseInventoryContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 12), RightToLeft = RightToLeft.Yes };
        var warehouseInvTitle = new Label
        {
            Text = "٦ · أرصدة المخزون (من إجمالي حركات المخزون) · غير الصفر فقط",
            Dock = DockStyle.Top,
            Height = 28,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight
        };
        StyleReportGrid(_warehouseInventoryGrid);
        ConfigureWarehouseInventorySnapshotColumns();
        warehouseInventoryContainer.Controls.Add(_warehouseInventoryGrid);
        warehouseInventoryContainer.Controls.Add(warehouseInvTitle);

        var detailStack = new Panel { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
        detailStack.Controls.Add(warehouseInventoryContainer);
        detailStack.Controls.Add(transferPanel);
        detailStack.Controls.Add(lowStockPanel);

        var innerVertical = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, RightToLeft = RightToLeft.Yes };
        ApplyInitialSplitterDistance(innerVertical, 380);
        innerVertical.Panel1.Controls.Add(midSplit);
        innerVertical.Panel2.Controls.Add(detailStack);

        var outer = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, RightToLeft = RightToLeft.Yes };
        ApplyInitialSplitterDistance(outer, 448);
        outer.Panel1.Controls.Add(summaryBlock);
        outer.Panel2.Controls.Add(innerVertical);

        var overviewPanel = new Panel { RightToLeft = RightToLeft.Yes };
        outer.Dock = DockStyle.Fill;
        overviewPanel.Controls.Add(outer);

        var profitPanel = new Panel { RightToLeft = RightToLeft.Yes };
        var profitHost = new Panel { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
        var profitTop = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 4, 8, 4),
            WrapContents = false
        };
        profitTop.Controls.Add(_profitAllBranchesCheck);
        StyleReportGrid(_profitByInvoiceGrid);
        StyleReportGrid(_profitByProductGrid);
        ConfigureProfitByInvoiceColumns();
        ConfigureProfitByProductColumns();
        var profitSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, RightToLeft = RightToLeft.Yes };
        ApplyInitialSplitterDistance(profitSplit, 360);
        profitSplit.Panel1.Controls.Add(_profitByInvoiceGrid);
        profitSplit.Panel2.Controls.Add(_profitByProductGrid);
        _profitRollupLabel.Dock = DockStyle.Top;
        profitHost.Controls.Add(profitTop);
        profitHost.Controls.Add(_profitRollupLabel);
        profitHost.Controls.Add(profitSplit);
        profitHost.Dock = DockStyle.Fill;
        profitPanel.Controls.Add(profitHost);

        var stockPanel = new Panel { RightToLeft = RightToLeft.Yes };
        var stockHost = new Panel { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
        var stockFilter = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 4, 8, 4)
        };
        _reportHistoryProductCombo.RightToLeft = RightToLeft.Yes;
        stockFilter.Controls.Add(_reportHistoryProductCombo);
        stockFilter.Controls.Add(new Label { Text = "الصنف لعرض الحركة:", AutoSize = true, Margin = new Padding(12, 8, 0, 0), TextAlign = ContentAlignment.MiddleRight, Font = UiFontCaption, RightToLeft = RightToLeft.Yes });
        StyleReportGrid(_stockFromMovementsGrid);
        StyleReportGrid(_stockHistoryGrid);
        ConfigureStockFromMovementsColumns();
        ConfigureStockHistoryColumns();
        var stockSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, RightToLeft = RightToLeft.Yes };
        ApplyInitialSplitterDistance(stockSplit, 300);
        stockSplit.Panel1.Controls.Add(_stockFromMovementsGrid);
        stockSplit.Panel2.Controls.Add(_stockHistoryGrid);
        stockHost.Controls.Add(stockSplit);
        stockHost.Controls.Add(stockFilter);
        stockHost.Dock = DockStyle.Fill;
        stockPanel.Controls.Add(stockHost);

        var xferPanel = new Panel { RightToLeft = RightToLeft.Yes };
        StyleReportGrid(_transferFullGrid);
        ConfigureTransferReportColumns(_transferFullGrid);
        _transferFullGrid.Dock = DockStyle.Fill;
        xferPanel.Controls.Add(_transferFullGrid);

        var cashPanel = new Panel { RightToLeft = RightToLeft.Yes };
        var cashHost = new Panel { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
        StyleReportGrid(_cashFlowGrid);
        StyleReportGrid(_expenseReportGrid);
        ConfigureCashFlowColumns();
        ConfigureExpenseReportColumns();
        var expenseBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8, 4, 8, 4),
            WrapContents = false
        };
        expenseBar.Controls.Add(_expenseSaveButton);
        expenseBar.Controls.Add(_expenseDateInput);
        expenseBar.Controls.Add(new Label { Text = "التاريخ", AutoSize = true, Margin = new Padding(0, 8, 0, 0), RightToLeft = RightToLeft.Yes });
        _expenseDescriptionInput.RightToLeft = RightToLeft.Yes;
        expenseBar.Controls.Add(_expenseDescriptionInput);
        expenseBar.Controls.Add(new Label { Text = "الوصف", AutoSize = true, Margin = new Padding(0, 8, 0, 0), RightToLeft = RightToLeft.Yes });
        _expenseCategoryInput.RightToLeft = RightToLeft.Yes;
        expenseBar.Controls.Add(_expenseCategoryInput);
        expenseBar.Controls.Add(new Label { Text = "البند", AutoSize = true, Margin = new Padding(0, 8, 0, 0), RightToLeft = RightToLeft.Yes });
        expenseBar.Controls.Add(_expenseAmountInput);
        expenseBar.Controls.Add(new Label { Text = "المبلغ", AutoSize = true, Margin = new Padding(0, 8, 0, 0), RightToLeft = RightToLeft.Yes });
        var cashSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, RightToLeft = RightToLeft.Yes };
        ApplyInitialSplitterDistance(cashSplit, 320);
        cashSplit.Panel1.Controls.Add(_cashFlowGrid);
        cashSplit.Panel2.Controls.Add(_expenseReportGrid);
        cashHost.Controls.Add(cashSplit);
        cashHost.Controls.Add(expenseBar);
        cashHost.Dock = DockStyle.Fill;
        cashPanel.Controls.Add(cashHost);

        _reportHistoryProductCombo.SelectedIndexChanged += async (_, _) => await RefreshReportStockHistoryOnlyAsync();

        var reportViewsHost = new Panel { Dock = DockStyle.Fill, Padding = Padding.Empty };
        var reportModuleRoot = new Panel { Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes };
        reportModuleRoot.Controls.Add(reportViewsHost);

        var reportPanels = new[] { overviewPanel, profitPanel, stockPanel, xferPanel, cashPanel };
        var reportNavTitles = new[]
        {
            "ملخص الأداء",
            "الربحية" + Environment.NewLine + "فاتورة / صنف",
            "المخزون" + Environment.NewLine + "وحركة الصنف",
            "كل التحويلات",
            "التدفق النقدي" + Environment.NewLine + "والمصروفات"
        };
        var navButtons = new Button[reportPanels.Length];
        void SelectReportModuleView(int index)
        {
            for (var i = 0; i < reportPanels.Length; i++)
            {
                reportPanels[i].Visible = i == index;
                if (i == index)
                    reportPanels[i].BringToFront();
                navButtons[i].BackColor = i == index ? Color.FromArgb(213, 224, 237) : Color.White;
                navButtons[i].Font = i == index ? new Font(UiFont, FontStyle.Bold) : UiFont;
            }
        }

        for (var i = 0; i < reportPanels.Length; i++)
        {
            var idx = i;
            var b = CreateReportPillButton(reportNavTitles[i]);
            b.Click += (_, _) => SelectReportModuleView(idx);
            exportAndNavRow.Controls.Add(b);
            navButtons[i] = b;
        }

        exportAndNavRow.Controls.Add(exportExcelBtn);

        foreach (var p in reportPanels)
        {
            p.Dock = DockStyle.Fill;
            p.Visible = false;
            reportViewsHost.Controls.Add(p);
        }

        SelectReportModuleView(0);

        tab.Controls.Add(reportModuleRoot);
        tab.Controls.Add(reportsChrome);
        return tab;
    }

    private void ConfigureSalesByWarehouseReportColumns()
    {
        _salesByWarehouseGrid.AutoGenerateColumns = false;
        _salesByWarehouseGrid.Columns.Clear();
        _salesByWarehouseGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _salesByWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SalesByWarehouseSummaryDto.WarehouseName), HeaderText = "الموقع", FillWeight = 26, ReadOnly = true });
        _salesByWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SalesByWarehouseSummaryDto.SiteType), HeaderText = "النوع", FillWeight = 12, ReadOnly = true });
        _salesByWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SalesByWarehouseSummaryDto.InvoiceCount), HeaderText = "الفواتير", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _salesByWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SalesByWarehouseSummaryDto.NetSales), HeaderText = "صافي المبيعات", FillWeight = 18, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _salesByWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SalesByWarehouseSummaryDto.GrossSales), HeaderText = "الإجمالي قبل الخصم", FillWeight = 16, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _salesByWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SalesByWarehouseSummaryDto.TotalDiscounts), HeaderText = "الخصومات", FillWeight = 16, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureTopProductsReportColumns()
    {
        _topProductsGrid.AutoGenerateColumns = false;
        _topProductsGrid.Columns.Clear();
        _topProductsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _topProductsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TopSellingProductDto.ProductName), HeaderText = "المنتج", FillWeight = 45, ReadOnly = true });
        _topProductsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TopSellingProductDto.QuantitySold), HeaderText = "الكمية المباعة", FillWeight = 20, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _topProductsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TopSellingProductDto.SalesAmount), HeaderText = "قيمة المبيعات", FillWeight = 22, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureSlowMoversReportColumns()
    {
        _slowMovingGrid.AutoGenerateColumns = false;
        _slowMovingGrid.Columns.Clear();
        _slowMovingGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _slowMovingGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SlowMovingProductDto.ProductName), HeaderText = "المنتج", FillWeight = 50, ReadOnly = true });
        _slowMovingGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SlowMovingProductDto.OnHandAtWarehouse), HeaderText = "الرصيد هنا", FillWeight = 22, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _slowMovingGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(SlowMovingProductDto.QuantitySoldInPeriod), HeaderText = "المباع في الفترة", FillWeight = 22, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureReportLowStockColumns()
    {
        _reportLowStockGrid.AutoGenerateColumns = false;
        _reportLowStockGrid.Columns.Clear();
        _reportLowStockGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _reportLowStockGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LowStockItemDto.ProductName), HeaderText = "المنتج", FillWeight = 55, ReadOnly = true });
        _reportLowStockGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LowStockItemDto.CurrentStock), HeaderText = "الكمية الحالية", FillWeight = 20, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _reportLowStockGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(LowStockItemDto.Threshold), HeaderText = "تنبيه عند ≤", FillWeight = 15, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureTransferReportColumns(DataGridView grid)
    {
        grid.AutoGenerateColumns = false;
        grid.Columns.Clear();
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TransferLedgerRowDto.MovementUtc), HeaderText = "الوقت (UTC)", FillWeight = 18, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TransferLedgerRowDto.ProductName), HeaderText = "المنتج", FillWeight = 28, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TransferLedgerRowDto.Quantity), HeaderText = "الكمية", FillWeight = 10, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TransferLedgerRowDto.FromWarehouseName), HeaderText = "من المستودع", FillWeight = 15, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TransferLedgerRowDto.ToWarehouseName), HeaderText = "إلى المستودع", FillWeight = 15, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(TransferLedgerRowDto.Notes), HeaderText = "ملاحظات", FillWeight = 22, ReadOnly = true });
    }

    private void ConfigureProfitByInvoiceColumns()
    {
        _profitByInvoiceGrid.AutoGenerateColumns = false;
        _profitByInvoiceGrid.Columns.Clear();
        _profitByInvoiceGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.InvoiceNumber), HeaderText = "فاتورة", FillWeight = 14, ReadOnly = true });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.InvoiceDateUtc), HeaderText = "الوقت (UTC)", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.WarehouseName), HeaderText = "فرع", FillWeight = 12, ReadOnly = true });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.NetRevenue), HeaderText = "صافي", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.EstimatedCogs), HeaderText = "تكلفة مقدرة", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.EstimatedGrossProfit), HeaderText = "ربح مقدر", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.MarginPercent), HeaderText = "هامش %", FillWeight = 10, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureProfitByProductColumns()
    {
        _profitByProductGrid.AutoGenerateColumns = false;
        _profitByProductGrid.Columns.Clear();
        _profitByProductGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.ProductName), HeaderText = "الصنف", FillWeight = 28, ReadOnly = true });
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.QuantitySold), HeaderText = "كمية مباعة", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.Revenue), HeaderText = "إيراد", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.EstimatedCogs), HeaderText = "تكلفة مقدرة", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.EstimatedGrossProfit), HeaderText = "ربح مقدر", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureStockFromMovementsColumns()
    {
        _stockFromMovementsGrid.AutoGenerateColumns = false;
        _stockFromMovementsGrid.Columns.Clear();
        _stockFromMovementsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _stockFromMovementsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseStockMovementRowDto.ProductName), HeaderText = "المنتج", FillWeight = 22, ReadOnly = true });
        _stockFromMovementsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseStockMovementRowDto.Category), HeaderText = "تصنيف", FillWeight = 10, ReadOnly = true });
        _stockFromMovementsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseStockMovementRowDto.WarehouseName), HeaderText = "مستودع", FillWeight = 14, ReadOnly = true });
        _stockFromMovementsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseStockMovementRowDto.SiteTypeLabel), HeaderText = "نوع", FillWeight = 10, ReadOnly = true });
        _stockFromMovementsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseStockMovementRowDto.QuantityOnHand), HeaderText = "رصيد", FillWeight = 10, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _stockFromMovementsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseStockMovementRowDto.RetailUnitPrice), HeaderText = "سعر بيع", FillWeight = 10, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _stockFromMovementsGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseStockMovementRowDto.RetailStockValue), HeaderText = "قيمة تقديرية", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureStockHistoryColumns()
    {
        _stockHistoryGrid.AutoGenerateColumns = false;
        _stockHistoryGrid.Columns.Clear();
        _stockHistoryGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _stockHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(StockMovementHistoryRowDto.MovementDateUtc), HeaderText = "الوقت (UTC)", FillWeight = 14, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        _stockHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(StockMovementHistoryRowDto.MovementType), HeaderText = "النوع", FillWeight = 10, ReadOnly = true });
        _stockHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(StockMovementHistoryRowDto.Quantity), HeaderText = "كمية", FillWeight = 8, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _stockHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(StockMovementHistoryRowDto.FromWarehouseName), HeaderText = "من", FillWeight = 12, ReadOnly = true });
        _stockHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(StockMovementHistoryRowDto.ToWarehouseName), HeaderText = "إلى", FillWeight = 12, ReadOnly = true });
        _stockHistoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(StockMovementHistoryRowDto.Notes), HeaderText = "ملاحظات", FillWeight = 22, ReadOnly = true });
    }

    private void ConfigureCashFlowColumns()
    {
        _cashFlowGrid.AutoGenerateColumns = false;
        _cashFlowGrid.Columns.Clear();
        _cashFlowGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _cashFlowGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DailyCashFlowRowDto.DayLocal), HeaderText = "اليوم", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd" } });
        _cashFlowGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DailyCashFlowRowDto.SalesIncome), HeaderText = "مبيعات", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _cashFlowGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DailyCashFlowRowDto.ServiceIncome), HeaderText = "خدمات", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _cashFlowGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DailyCashFlowRowDto.PurchaseCashOut), HeaderText = "مشتريات", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _cashFlowGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DailyCashFlowRowDto.OperatingExpenses), HeaderText = "مصروفات تشغيل", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _cashFlowGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(DailyCashFlowRowDto.NetCashIndicator), HeaderText = "صافي مؤشر", FillWeight = 14, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureExpenseReportColumns()
    {
        _expenseReportGrid.AutoGenerateColumns = false;
        _expenseReportGrid.Columns.Clear();
        _expenseReportGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _expenseReportGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ExpenseReportRowDto.ExpenseDateUtc), HeaderText = "الوقت (UTC)", FillWeight = 14, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        _expenseReportGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ExpenseReportRowDto.Amount), HeaderText = "مبلغ", FillWeight = 10, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _expenseReportGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ExpenseReportRowDto.Category), HeaderText = "البند", FillWeight = 14, ReadOnly = true });
        _expenseReportGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ExpenseReportRowDto.Description), HeaderText = "وصف", FillWeight = 22, ReadOnly = true });
        _expenseReportGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ExpenseReportRowDto.WarehouseName), HeaderText = "فرع", FillWeight = 12, ReadOnly = true });
        _expenseReportGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ExpenseReportRowDto.CreatedByUsername), HeaderText = "المستخدم", FillWeight = 12, ReadOnly = true });
    }

    private void ConfigureWarehouseInventorySnapshotColumns()
    {
        _warehouseInventoryGrid.AutoGenerateColumns = false;
        _warehouseInventoryGrid.Columns.Clear();
        _warehouseInventoryGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _warehouseInventoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseInventoryRow.ProductName), HeaderText = "المنتج", FillWeight = 28, ReadOnly = true });
        _warehouseInventoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseInventoryRow.Category), HeaderText = "التصنيف", FillWeight = 12, ReadOnly = true });
        _warehouseInventoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseInventoryRow.PackageSize), HeaderText = "التعبئة", FillWeight = 10, ReadOnly = true });
        _warehouseInventoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseInventoryRow.Warehouse), HeaderText = "المستودع", FillWeight = 18, ReadOnly = true });
        _warehouseInventoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseInventoryRow.WarehouseType), HeaderText = "نوع الموقع", FillWeight = 10, ReadOnly = true });
        _warehouseInventoryGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(WarehouseInventoryRow.Stock), HeaderText = "الرصيد", FillWeight = 12, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

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
                await RefreshDailyKpisAsync();
            };
            _inventoryWarehouseCombo.SelectedIndexChanged += async (_, _) =>
            {
                await RefreshInventoryAsync();
                await RefreshAuditViewAsync();
                await RefreshReportsAsync();
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

    private async Task RefreshReportsAsync()
    {
        var from = _reportFromPicker.Value.Date;
        var to = _reportToPicker.Value.Date;
        if (to < from)
        {
            MessageBox.Show("لا يمكن أن يكون تاريخ «إلى» قبل تاريخ «من».", "تقارير", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }

        var (resolved, reportWarehouseId) = await TryResolveStockContextWarehouseIdAsync(useReportWarehouseScope: true);
        if (!resolved) return;
        var stockScopeWh = TryGetWarehouseIdFromCombo(_reportWarehouseCombo, out var stockWhId) ? stockWhId : (int?)null;
        var report = await _reportService.GetSalesDashboardAsync(from, to, reportWarehouseId);
        var byWh = await _reportService.GetSalesSummariesByWarehouseAsync(from, to);
        var focusName = TryGetWarehouseIdFromCombo(_reportWarehouseCombo, out _)
            ? (_reportWarehouseCombo.SelectedItem as WarehouseDto)?.Name ?? "المستودع المحدد"
            : "المستودع المحدد";

        var fromAr = report.FromUtc.ToString("d", ReportsCulture);
        var toAr = report.ToUtc.ToString("d", ReportsCulture);
        _reportPeriodBanner.Text =
            $"   من {fromAr}  إلى  {toAr}   ·   المستودع المحدد: {focusName}";
        _kpiNetSalesVal.Text = report.NetSales.ToString("n2", ReportsCulture);
        _kpiInvoicesVal.Text = report.InvoiceCount.ToString("n0", ReportsCulture);
        _kpiAvgTicketVal.Text = report.AverageInvoice.ToString("n2", ReportsCulture);
        _kpiEstProfitVal.Text = report.EstimatedGrossProfit.ToString("n2", ReportsCulture);
        _kpiStockValueVal.Text = report.InventoryValue.ToString("n2", ReportsCulture);
        _kpiLowStockVal.Text = report.LowStockCount.ToString("n0", ReportsCulture);
        _reportMetricsFootnote.Text =
            $"الإجمالي {report.GrossSales.ToString("n2", ReportsCulture)}  ·  الخصومات {report.TotalDiscounts.ToString("n2", ReportsCulture)}  ·  تكلفة البضاعة المقدرة {report.EstimatedCogs.ToString("n2", ReportsCulture)}  ·  " +
            "تُشتق تكلفة البضاعة من متوسط تكلفة الشراء المرجح المسجّل من المستودع الرئيسي (تقدير فقط).";

        _salesByWarehouseGrid.DataSource = byWh;
        _topProductsGrid.DataSource = report.TopProducts;
        _slowMovingGrid.DataSource = report.SlowMovingProducts;
        _transferReportGrid.DataSource = report.TransfersInPeriod;
        var lowRows = await _inventoryService.GetLowStockAsync(reportWarehouseId);
        _reportLowStockGrid.DataSource = lowRows;
        await RefreshWarehouseInventoryGridAsync(stockScopeWh);

        var profitWh = ResolveReportProfitWarehouseFilter();
        _profitByInvoiceGrid.DataSource = await _reportService.GetInvoiceProfitBreakdownAsync(from, to, profitWh);
        _profitByProductGrid.DataSource = await _reportService.GetProductProfitBreakdownAsync(from, to, profitWh);
        var rollup = await _reportService.GetProfitRollupAsync(from, to, profitWh);
        _profitRollupLabel.Text =
            $"إجمالي الإيراد {rollup.TotalRevenue.ToString("n2", ReportsCulture)} · تكلفة مقدرة (متوسط شراء رئيسي) {rollup.TotalEstimatedCogs.ToString("n2", ReportsCulture)} · ربح مقدر {rollup.TotalEstimatedGrossProfit.ToString("n2", ReportsCulture)}";

        _stockFromMovementsGrid.DataSource = await _reportService.GetCurrentStockFromMovementsAsync(stockScopeWh);
        await LoadReportHistoryProductComboAsync();
        await RefreshReportStockHistoryCoreAsync(from, to);

        _transferFullGrid.DataSource = await _reportService.GetTransfersReportAsync(from, to, null, null);

        _cashFlowGrid.DataSource = await _reportService.GetDailyCashFlowAsync(from, to);
        _expenseReportGrid.DataSource = await _reportService.GetExpensesInPeriodAsync(from, to, stockScopeWh);
    }

    private int? ResolveReportProfitWarehouseFilter()
    {
        if (_currentUser.Role == UserRole.Admin && _profitAllBranchesCheck.Checked)
            return null;
        return TryGetWarehouseIdFromCombo(_reportWarehouseCombo, out var w) ? w : null;
    }

    private async Task LoadReportHistoryProductComboAsync()
    {
        int? keep = _reportHistoryProductCombo.SelectedValue is int ix ? ix : null;
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        var items = new List<ReportHistoryProductItem> { new() { Id = 0, Caption = "— اختر صنفاً —" } };
        items.AddRange(rows.Select(p => new ReportHistoryProductItem { Id = p.Id, Caption = p.Name }));
        _reportHistoryProductCombo.DataSource = items;
        _reportHistoryProductCombo.DisplayMember = nameof(ReportHistoryProductItem.Caption);
        _reportHistoryProductCombo.ValueMember = nameof(ReportHistoryProductItem.Id);
        if (keep is > 0)
        {
            for (var i = 0; i < _reportHistoryProductCombo.Items.Count; i++)
            {
                if (_reportHistoryProductCombo.Items[i] is ReportHistoryProductItem it && it.Id == keep.Value)
                {
                    _reportHistoryProductCombo.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private async Task RefreshReportStockHistoryOnlyAsync()
    {
        var from = _reportFromPicker.Value.Date;
        var to = _reportToPicker.Value.Date;
        await RefreshReportStockHistoryCoreAsync(from, to);
    }

    private async Task RefreshReportStockHistoryCoreAsync(DateTime from, DateTime to)
    {
        var wh = TryGetWarehouseIdFromCombo(_reportWarehouseCombo, out var w) ? (int?)w : null;
        var v = _reportHistoryProductCombo.SelectedValue;
        var pid = v is int i ? i : 0;
        if (pid <= 0)
        {
            _stockHistoryGrid.DataSource = Array.Empty<StockMovementHistoryRowDto>();
            return;
        }

        _stockHistoryGrid.DataSource = await _reportService.GetStockMovementHistoryAsync(pid, from, to, wh);
    }

    private async Task SaveExpenseFromReportsAsync()
    {
        if (_currentUser.Role != UserRole.Admin)
        {
            MessageBox.Show("يسجّل المسؤولون (مدير النظام) مصروفات التشغيل فقط.", "تقارير", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }

        try
        {
            var amt = _expenseAmountInput.Value;
            var cat = _expenseCategoryInput.Text.Trim();
            var desc = _expenseDescriptionInput.Text.Trim();
            int? wh = TryGetWarehouseIdFromCombo(_reportWarehouseCombo, out var wid) ? wid : null;
            await _expenseService.RecordExpenseAsync(amt, cat, desc, _expenseDateInput.Value.Date, wh, _currentUser.Id);
            MessageBox.Show("تم حفظ المصروف.", "تقارير", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            var from = _reportFromPicker.Value.Date;
            var to = _reportToPicker.Value.Date;
            _cashFlowGrid.DataSource = await _reportService.GetDailyCashFlowAsync(from, to);
            _expenseReportGrid.DataSource = await _reportService.GetExpensesInPeriodAsync(from, to, wh);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "مصروف", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
    }

    private async Task ExportAnalyticsReportsToExcelAsync()
    {
        var from = _reportFromPicker.Value.Date;
        var to = _reportToPicker.Value.Date;
        if (to < from)
        {
            MessageBox.Show("تاريخ غير صالح.", "تصدير", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }

        using var dlg = new SaveFileDialog
        {
            Title = "تصدير التقارير إلى Excel",
            Filter = "ملف Excel|*.xlsx",
            FileName = $"OilChangePOS-تقارير-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var profitWh = ResolveReportProfitWarehouseFilter();
        int? scopeWh = TryGetWarehouseIdFromCombo(_reportWarehouseCombo, out var wid) ? wid : null;
        var byWh = await _reportService.GetSalesSummariesByWarehouseAsync(from, to);
        var invProf = await _reportService.GetInvoiceProfitBreakdownAsync(from, to, profitWh);
        var pProf = await _reportService.GetProductProfitBreakdownAsync(from, to, profitWh);
        var stock = await _reportService.GetCurrentStockFromMovementsAsync(null);
        var xfer = await _reportService.GetTransfersReportAsync(from, to, null, null);
        var cash = await _reportService.GetDailyCashFlowAsync(from, to);
        var exp = await _reportService.GetExpensesInPeriodAsync(from, to, scopeWh);
        var top = await _reportService.GetTopSellingProductsAsync(from, to, profitWh, 50);
        var slowWh = scopeWh ?? (await _warehouseService.GetBranchesAsync()).FirstOrDefault()?.Id ?? 0;
        var slow = slowWh > 0
            ? await _reportService.GetSlowMovingProductsAsync(from, to, slowWh, 50)
            : new List<SlowMovingProductDto>();

        using var wb = new XLWorkbook();
        void Sheet<T>(string name, IEnumerable<T> rows, params (string Header, Func<T, object?> Getter)[] cols)
        {
            var ws = wb.Worksheets.Add(name);
            for (var c = 0; c < cols.Length; c++)
                ws.Cell(1, c + 1).Value = cols[c].Header;
            var r = 2;
            foreach (var row in rows)
            {
                for (var c = 0; c < cols.Length; c++)
                    ws.Cell(r, c + 1).Value = cols[c].Getter(row)?.ToString() ?? string.Empty;
                r++;
            }
            ws.Columns().AdjustToContents();
        }

        Sheet("مبيعات_الفروع", byWh,
            ("المستودع", x => x.WarehouseName),
            ("النوع", x => x.SiteType),
            ("الفواتير", x => x.InvoiceCount),
            ("صافي المبيعات", x => x.NetSales),
            ("الإجمالي قبل الخصم", x => x.GrossSales),
            ("الخصومات", x => x.TotalDiscounts));
        Sheet("ربحية_الفواتير", invProf,
            ("رقم الفاتورة", x => x.InvoiceNumber),
            ("الوقت UTC", x => x.InvoiceDateUtc),
            ("الفرع", x => x.WarehouseName),
            ("صافي الإيراد", x => x.NetRevenue),
            ("تكلفة البضاعة تقديري", x => x.EstimatedCogs),
            ("الربح التقديري", x => x.EstimatedGrossProfit),
            ("هامش %", x => x.MarginPercent));
        Sheet("ربحية_الأصناف", pProf,
            ("الصنف", x => x.ProductName),
            ("الكمية", x => x.QuantitySold),
            ("الإيراد", x => x.Revenue),
            ("تكلفة تقديرية", x => x.EstimatedCogs),
            ("ربح تقديري", x => x.EstimatedGrossProfit));
        Sheet("المخزون_من_الحركة", stock,
            ("الصنف", x => x.ProductName),
            ("المستودع", x => x.WarehouseName),
            ("الكمية", x => x.QuantityOnHand),
            ("سعر البيع", x => x.RetailUnitPrice),
            ("قيمة تقديرية", x => x.RetailStockValue));
        Sheet("التحويلات", xfer,
            ("الوقت UTC", x => x.MovementUtc),
            ("الصنف", x => x.ProductName),
            ("الكمية", x => x.Quantity),
            ("من", x => x.FromWarehouseName),
            ("إلى", x => x.ToWarehouseName),
            ("ملاحظات", x => x.Notes));
        Sheet("التدفق_النقدي", cash,
            ("اليوم", x => x.DayLocal),
            ("مبيعات", x => x.SalesIncome),
            ("خدمات", x => x.ServiceIncome),
            ("مشتريات", x => x.PurchaseCashOut),
            ("مصروفات تشغيل", x => x.OperatingExpenses),
            ("صافي المؤشر", x => x.NetCashIndicator));
        Sheet("المصروفات", exp,
            ("الوقت UTC", x => x.ExpenseDateUtc),
            ("المبلغ", x => x.Amount),
            ("البند", x => x.Category),
            ("الوصف", x => x.Description),
            ("المستودع", x => x.WarehouseName),
            ("المستخدم", x => x.CreatedByUsername));
        Sheet("الأكثر_مبيعا", top,
            ("الصنف", x => x.ProductName),
            ("الكمية", x => x.QuantitySold),
            ("قيمة المبيعات", x => x.SalesAmount));
        Sheet("بطيئة_الحركة", slow,
            ("الصنف", x => x.ProductName),
            ("الرصيد", x => x.OnHandAtWarehouse),
            ("المباع بالفترة", x => x.QuantitySoldInPeriod));

        wb.SaveAs(dlg.FileName);
        MessageBox.Show($"تم الحفظ:\n{dlg.FileName}", "تصدير", MessageBoxButtons.OK, MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }

    private sealed class ReportHistoryProductItem
    {
        public int Id { get; init; }
        public string Caption { get; init; } = string.Empty;
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

    private sealed class CatalogCompanyRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int ProductCount { get; set; }
    }

    private sealed class CatalogProductRow
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ProductCategory { get; set; } = string.Empty;
        public string PackageSize { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    private sealed class CompanyListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
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
