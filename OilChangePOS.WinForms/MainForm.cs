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
    /// <summary>Currency label shown next to amounts in POS (Egyptian pound).</summary>
    private const string UiCurrencySuffix = " ج.م";
    /// <summary>Main Warehouse captions/inputs with this <see cref="Control.Tag"/> skip <see cref="ApplyUnifiedFont"/> so sizes stay readable.</summary>
    private const string MainWarehouseUiLabelTag = "MW_UI";
    private readonly IDbContextFactory<OilChangePosDbContext> _dbFactory;
    private readonly ISalesService _salesService;
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
    /// <summary>POS cart: empty-state overlay panel (shown when <see cref="_cartBinding"/> has no rows).</summary>
    private Panel? _posCartEmptyOverlay;
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
        AutoSize = true,
        ForeColor = UiTextSecondary,
        BackColor = Color.FromArgb(248, 249, 250),
        TextAlign = ContentAlignment.TopRight,
        Padding = new Padding(16, 10, 16, 12),
        Margin = new Padding(0, 0, 0, 8),
        Font = new Font(UiFont, FontStyle.Regular),
        RightToLeft = RightToLeft.Yes,
        BorderStyle = BorderStyle.FixedSingle,
        UseCompatibleTextRendering = false
    };
    private readonly Label _kpiNetSalesVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiInvoicesVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiAvgTicketVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiEstProfitVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiStockValueVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _kpiLowStockVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    /// <summary>ملخص الأداء — تفصيل الإجمالي / الخصومات / تكلفة البضاعة (يُحدَّث في <see cref="RefreshReportsAsync"/>).</summary>
    private readonly Label _overviewGrossVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _overviewDiscountsVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _overviewCogsVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
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
    /// <summary>Profit module KPI values (updated in <see cref="RefreshReportsAsync"/>).</summary>
    private readonly Label _profitKpiRevenueVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _profitKpiCogsVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly Label _profitKpiProfitVal = new() { Text = "—", AutoSize = false, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.No };
    private readonly CheckBox _profitAllBranchesCheck = new() { Text = "ربحية جميع الفروع (تجاهل المستودع المحدد)", AutoSize = true, RightToLeft = RightToLeft.Yes };
    private readonly DataGridView _stockFromMovementsGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly ComboBox _reportHistoryProductCombo = new() { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _stockHistoryGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _transferFullGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _cashFlowGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _expenseReportGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _branchSalesLinesGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _branchIncomingGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _branchSellersGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    /// <summary>Branch-user tab: same register grids as admin «حصر الفرع» but bound independently (separate <see cref="TabPage"/>).</summary>
    private readonly DateTimePicker _branchOnlyFromPicker = new() { Width = 150 };
    private readonly DateTimePicker _branchOnlyToPicker = new() { Width = 150 };
    private readonly Label _branchOnlyPeriodBanner = new()
    {
        Dock = DockStyle.Top,
        Height = 40,
        ForeColor = Color.White,
        BackColor = Color.FromArgb(44, 62, 80),
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(0, 0, 16, 0),
        RightToLeft = RightToLeft.Yes
    };
    private readonly DataGridView _branchOnlySalesLinesGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _branchOnlyIncomingGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
    private readonly DataGridView _branchOnlySellersGrid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true };
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
    private readonly TextBox _posSearchBox = new()
    {
        Width = 280,
        PlaceholderText = "بحث عن صنف...",
        RightToLeft = RightToLeft.Yes,
        BorderStyle = BorderStyle.FixedSingle,
        BackColor = Color.White
    };
    private readonly Label _subtotalValueLabel = new() { AutoSize = false, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(44, 62, 80) };
    private readonly Label _discountValueLabel = new() { AutoSize = false, Font = new Font("Segoe UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(192, 57, 43) };
    private readonly Label _totalValueLabel = new() { AutoSize = false, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.FromArgb(39, 174, 96) };
    /// <summary>POS cart: one-line recap (line count + subtotal + payable) above the pay button.</summary>
    private readonly Label _posCartQuickSummary = new()
    {
        Dock = DockStyle.Top,
        Height = 44,
        TextAlign = ContentAlignment.MiddleRight,
        RightToLeft = RightToLeft.Yes,
        Font = new Font("Segoe UI", 11.25f, FontStyle.Bold, GraphicsUnit.Point),
        ForeColor = Color.FromArgb(25, 55, 75),
        BackColor = Color.FromArgb(230, 242, 252),
        Padding = new Padding(10, 8, 10, 8),
        UseCompatibleTextRendering = false
    };
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
    private readonly Label _breadcrumbLabel = new() { Dock = DockStyle.Top, Height = 22, ForeColor = UiTextSecondary, Text = "الرئيسية / الطلب", TextAlign = ContentAlignment.TopRight, RightToLeft = RightToLeft.Yes };
    private readonly Label _orderTitleLabel = new() { Dock = DockStyle.Top, Height = ModuleHeaderTitleHeight, Font = UiFontTitle, ForeColor = UiTextPrimary, Text = "البيع", TextAlign = ContentAlignment.TopRight, RightToLeft = RightToLeft.No };
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



    private void ApplyRoleToUi()
    {
        var admin = _currentUser.Role == UserRole.Admin;
        Text = admin
            ? $"نقطة بيع تغيير الزيت — {_currentUser.Username} (مدير)"
            : $"نقطة بيع تغيير الزيت — {_currentUser.Username} (فرع)";

        if (admin)
        {
            if (_sidebarNavButtons.Count > 2)
            {
                _sidebarNavButtons[0].Visible = false;
                _sidebarNavButtons[1].Visible = false;
                _sidebarNavButtons[2].Visible = false;
            }
            _mainTabs.SelectedIndex = 5;
            ApplySidebarNavHighlight(_mainTabs.SelectedIndex);
            return;
        }

        for (var i = 3; i < _sidebarNavButtons.Count; i++)
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
            if (e.TabPageIndex < 3)
                e.Cancel = true;
            return;
        }

        if (e.TabPageIndex > 2)
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



























}
