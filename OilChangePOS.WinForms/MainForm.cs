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
        menu.Controls.Add(BuildMenuButton("تقارير الفرع", 2));
        menu.Controls.Add(BuildMenuButton("الشركات والأصناف", 3));
        menu.Controls.Add(BuildMenuButton("الفروع", 4));
        menu.Controls.Add(BuildMenuButton("المستودع الرئيسي", 5));
        menu.Controls.Add(BuildMenuButton("التحويلات", 6));
        menu.Controls.Add(BuildMenuButton("جرد المخزون", 7));
        menu.Controls.Add(BuildMenuButton("التقارير", 8));

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
        _mainTabs.TabPages.Add(BuildBranchReportsTab());
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
        _mainTabs.SelectedIndexChanged += async (_, _) =>
        {
            ApplySidebarNavHighlight(_mainTabs.SelectedIndex);
            if (_mainTabs.SelectedIndex == 2)
                await RefreshBranchOnlyReportsAsync();
        };

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
                if (targetTab < 3) return;
            }
            else if (targetTab > 2)
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
            // Valid splitter range is [panel1MinSize, maxDist]; if maxDist < panel1MinSize (tight layout),
            // pick the largest distance that still fits instead of forcing panel1MinSize (which throws).
            var lo = Math.Min(panel1MinSize, maxDist);
            var hi = Math.Max(panel1MinSize, maxDist);
            var dist = Math.Clamp(preferredDistance, lo, hi);

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

    private sealed class ReportHistoryProductItem
    {
        public int Id { get; init; }
        public string Caption { get; init; } = string.Empty;
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

}
