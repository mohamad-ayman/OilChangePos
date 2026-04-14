using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

/// <summary>Reports / analytics tab UI, grid columns, refresh and Excel export.</summary>
public partial class MainForm
{
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
        _overviewGrossVal.Text = report.GrossSales.ToString("n2", ReportsCulture);
        _overviewDiscountsVal.Text = report.TotalDiscounts.ToString("n2", ReportsCulture);
        _overviewCogsVal.Text = report.EstimatedCogs.ToString("n2", ReportsCulture);

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
        _profitKpiRevenueVal.Text = rollup.TotalRevenue.ToString("n2", ReportsCulture);
        _profitKpiCogsVal.Text = rollup.TotalEstimatedCogs.ToString("n2", ReportsCulture);
        _profitKpiProfitVal.Text = rollup.TotalEstimatedGrossProfit.ToString("n2", ReportsCulture);

        _stockFromMovementsGrid.DataSource = await _reportService.GetCurrentStockFromMovementsAsync(stockScopeWh);
        await LoadReportHistoryProductComboAsync();
        await RefreshReportStockHistoryCoreAsync(from, to);

        _transferFullGrid.DataSource = await _reportService.GetTransfersReportAsync(from, to, null, null);

        _cashFlowGrid.DataSource = await _reportService.GetDailyCashFlowAsync(from, to);
        _expenseReportGrid.DataSource = await _reportService.GetExpensesInPeriodAsync(from, to, stockScopeWh);

        _branchSalesLinesGrid.DataSource = await _reportService.GetBranchSalesLineRegisterAsync(from, to, reportWarehouseId);
        _branchIncomingGrid.DataSource = await _reportService.GetBranchIncomingRegisterAsync(from, to, reportWarehouseId);
        _branchSellersGrid.DataSource = await _reportService.GetBranchSalesBySellerAsync(from, to, reportWarehouseId);
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

        var branchWh = scopeWh ?? 0;
        var branchLines = branchWh > 0
            ? await _reportService.GetBranchSalesLineRegisterAsync(from, to, branchWh)
            : new List<BranchSalesLineRegisterDto>();
        var branchIn = branchWh > 0
            ? await _reportService.GetBranchIncomingRegisterAsync(from, to, branchWh)
            : new List<BranchIncomingRegisterDto>();
        var branchSell = branchWh > 0
            ? await _reportService.GetBranchSalesBySellerAsync(from, to, branchWh)
            : new List<BranchSellerSalesSummaryDto>();

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
        Sheet("حصر_اسطر_البيع_فرع", branchLines,
            ("وقت الفاتورة UTC", x => x.InvoiceDateUtc),
            ("رقم الفاتورة", x => x.InvoiceNumber),
            ("الفرع", x => x.WarehouseName),
            ("العميل", x => x.CustomerDisplay),
            ("البائع", x => x.SellerUsername),
            ("الصنف", x => x.ProductName),
            ("الكمية", x => x.Quantity),
            ("سعر الوحدة", x => x.UnitPrice),
            ("صافي السطر", x => x.LineTotal),
            ("إجمالي الفاتورة قبل الخصم", x => x.InvoiceSubtotal),
            ("خصم الفاتورة", x => x.InvoiceDiscount),
            ("صافي الفاتورة", x => x.InvoiceTotal));
        Sheet("وارد_الفرع", branchIn,
            ("التاريخ UTC", x => x.EntryDateUtc),
            ("النوع", x => x.EntryType),
            ("الصنف", x => x.ProductName),
            ("الكمية", x => x.Quantity),
            ("قيمة الشراء", x => x.AmountValue),
            ("المصدر", x => x.SourceDetail),
            ("ملاحظات", x => x.Notes),
            ("المستخدم", x => x.CreatedByDisplay));
        Sheet("ملخص_البائعين_فرع", branchSell,
            ("المستخدم", x => x.SellerUsername),
            ("عدد الفواتير", x => x.InvoiceCount),
            ("عدد الأسطر", x => x.LineItemCount),
            ("إجمالي قبل الخصم", x => x.InvoicesGrossSubtotal),
            ("الخصومات", x => x.InvoicesDiscountTotal),
            ("صافي المبيعات", x => x.InvoicesNetTotal));

        wb.SaveAs(dlg.FileName);
        MessageBox.Show($"تم الحفظ:\n{dlg.FileName}", "تصدير", MessageBoxButtons.OK, MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }

    /// <summary>Branch-role tab: حصر البيع والوارد والبائع للمستودع الحالي في نقطة البيع/المخزون.</summary>
    private async Task RefreshBranchOnlyReportsAsync()
    {
        if (!IsHandleCreated)
            return;

        var from = _branchOnlyFromPicker.Value.Date;
        var to = _branchOnlyToPicker.Value.Date;
        if (to < from)
        {
            MessageBox.Show("لا يمكن أن يكون تاريخ «إلى» قبل تاريخ «من».", "تقارير الفرع", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
            return;
        }

        if (!TryGetWarehouseIdFromCombo(_posWarehouseCombo, out var reportWh)
            && !TryGetWarehouseIdFromCombo(_inventoryWarehouseCombo, out reportWh))
        {
            _branchOnlyPeriodBanner.Text = "   لم يُحدد مستودع فرع — اختر المستودع في «طلب / بيع» أو «المخزون».";
            _branchOnlySalesLinesGrid.DataSource = Array.Empty<BranchSalesLineRegisterDto>();
            _branchOnlyIncomingGrid.DataSource = Array.Empty<BranchIncomingRegisterDto>();
            _branchOnlySellersGrid.DataSource = Array.Empty<BranchSellerSalesSummaryDto>();
            return;
        }

        var whName = TryGetWarehouseIdFromCombo(_posWarehouseCombo, out var posWh) && posWh == reportWh
            ? (_posWarehouseCombo.SelectedItem as WarehouseDto)?.Name ?? "الفرع"
            : (_inventoryWarehouseCombo.SelectedItem as WarehouseDto)?.Name ?? "الفرع";
        var fromAr = from.ToString("d", ReportsCulture);
        var toAr = to.ToString("d", ReportsCulture);
        _branchOnlyPeriodBanner.Text = $"   من {fromAr}  إلى  {toAr}   ·   {whName}";

        _branchOnlySalesLinesGrid.DataSource = await _reportService.GetBranchSalesLineRegisterAsync(from, to, reportWh);
        _branchOnlyIncomingGrid.DataSource = await _reportService.GetBranchIncomingRegisterAsync(from, to, reportWh);
        _branchOnlySellersGrid.DataSource = await _reportService.GetBranchSalesBySellerAsync(from, to, reportWh);
    }

    private TabPage BuildBranchReportsTab()
    {
        var tab = new TabPage("تقارير الفرع");
        tab.BackColor = Color.FromArgb(245, 247, 250);
        tab.RightToLeft = RightToLeft.Yes;

        _branchOnlyFromPicker.Value = DateTime.Today.AddDays(-7);
        _branchOnlyToPicker.Value = DateTime.Today;
        foreach (var p in new[] { _branchOnlyFromPicker, _branchOnlyToPicker })
        {
            p.Font = UiFont;
            p.Format = DateTimePickerFormat.Custom;
            p.CustomFormat = "yyyy/MM/dd";
            p.RightToLeft = RightToLeft.Yes;
        }

        foreach (var g in new[] { _branchOnlySalesLinesGrid, _branchOnlyIncomingGrid, _branchOnlySellersGrid })
            g.RightToLeft = RightToLeft.Yes;

        var refresh = BuildButton("تحديث", Color.FromArgb(41, 128, 185));
        refresh.Click += async (_, _) => await RefreshBranchOnlyReportsAsync();

        var chrome = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16, 12, 16, 12),
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes
        };
        chrome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        chrome.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f)); // standard module header
        chrome.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f)); // filter toolbar
        chrome.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var branchReportsHeader = BuildStandardModuleHeaderCard(
            "حصر الفرع — البيع والوارد والبائع",
            "المستودع هو نفس المستودع المختار في «طلب / بيع» و«المخزون». غيّر الفترة ثم اضغط «تحديث».",
            subtitleItalic: false,
            DockStyle.Fill,
            autoSizeHeight: false,
            out _,
            out _);
        branchReportsHeader.Margin = new Padding(0, 0, 0, 8);
        chrome.Controls.Add(branchReportsHeader, 0, 0);

        var filterFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            RightToLeft = RightToLeft.No,
            WrapContents = true,
            Padding = new Padding(10, 6, 10, 6),
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle
        };
        var lblFrom = new Label
        {
            Text = "من",
            AutoSize = true,
            Font = UiFontCaption,
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
            Font = UiFontCaption,
            ForeColor = UiTextPrimary,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(8, 12, 6, 8),
            UseCompatibleTextRendering = false
        };
        _branchOnlyFromPicker.Margin = new Padding(4, 8, 4, 8);
        _branchOnlyFromPicker.Width = 160;
        _branchOnlyToPicker.Margin = new Padding(4, 8, 4, 8);
        _branchOnlyToPicker.Width = 160;
        refresh.Margin = new Padding(16, 6, 8, 8);
        filterFlow.Controls.Add(refresh);
        filterFlow.Controls.Add(_branchOnlyToPicker);
        filterFlow.Controls.Add(lblTo);
        filterFlow.Controls.Add(_branchOnlyFromPicker);
        filterFlow.Controls.Add(lblFrom);

        _branchOnlyFromPicker.ValueChanged += async (_, _) => await RefreshBranchOnlyReportsAsync();
        _branchOnlyToPicker.ValueChanged += async (_, _) => await RefreshBranchOnlyReportsAsync();

        chrome.Controls.Add(filterFlow, 0, 1);

        var bannerHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 6, 14, 4), BackColor = Color.FromArgb(245, 247, 250), RightToLeft = RightToLeft.Yes };
        _branchOnlyPeriodBanner.Dock = DockStyle.Fill;
        _branchOnlyPeriodBanner.Font = UiFontSection;
        bannerHost.Controls.Add(_branchOnlyPeriodBanner);
        chrome.Controls.Add(bannerHost, 0, 2);

        ApplyProfitModuleGridChrome(_branchOnlySalesLinesGrid);
        ApplyProfitModuleGridChrome(_branchOnlyIncomingGrid);
        ApplyProfitModuleGridChrome(_branchOnlySellersGrid);
        ConfigureBranchSalesLinesRegisterColumns(_branchOnlySalesLinesGrid);
        ConfigureBranchIncomingRegisterColumns(_branchOnlyIncomingGrid);
        ConfigureBranchSellersSummaryColumns(_branchOnlySellersGrid);

        var branchScrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes
        };
        var branchLayout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 3,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(14, 10, 14, 16),
            BackColor = Color.Transparent
        };
        branchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        branchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 360f));
        branchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 340f));
        branchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));

        void SyncBranchScrollLayout(object? _, EventArgs __)
        {
            branchLayout.Width = Math.Max(320, branchScrollHost.DisplayRectangle.Width);
        }
        branchScrollHost.HandleCreated += SyncBranchScrollLayout;
        branchScrollHost.Resize += SyncBranchScrollLayout;

        var salesSection = BuildReportModuleGridSection(
            "١ · حصر أسطر البيع — تفصيل كل صنف مع الفاتورة والعميل والبائع",
            _branchOnlySalesLinesGrid,
            new Padding(0, 0, 0, 12));
        var incomingSection = BuildReportModuleGridSection(
            "٢ · وارد الفرع — شراء للمستودع + تحويلات واردة خلال الفترة",
            _branchOnlyIncomingGrid,
            new Padding(0, 0, 0, 12));
        var sellersSection = BuildReportModuleGridSection(
            "٣ · ملخص البائعين — عدد الفواتير والأسطر والإجماليات حسب منشئ الفاتورة",
            _branchOnlySellersGrid,
            Padding.Empty);

        branchLayout.Controls.Add(salesSection, 0, 0);
        branchLayout.Controls.Add(incomingSection, 0, 1);
        branchLayout.Controls.Add(sellersSection, 0, 2);
        branchScrollHost.Controls.Add(branchLayout);

        tab.Controls.Add(branchScrollHost);
        tab.Controls.Add(chrome);
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
                     _transferFullGrid, _cashFlowGrid, _expenseReportGrid,
                     _branchSalesLinesGrid, _branchIncomingGrid, _branchSellersGrid
                 })
            g.RightToLeft = RightToLeft.Yes;
        _profitAllBranchesCheck.Visible = _currentUser.Role == UserRole.Admin;
        _expenseDateInput.Value = DateTime.Today;
        _expenseSaveButton.Enabled = _currentUser.Role == UserRole.Admin;
        _expenseSaveButton.Click += async (_, _) => await SaveExpenseFromReportsAsync();

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
            RowCount = 4,
            Padding = new Padding(16, 12, 16, 14),
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes
        };
        reportsChrome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        reportsChrome.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f)); // standard module header
        reportsChrome.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        reportsChrome.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        reportsChrome.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var reportsPageHeader = BuildStandardModuleHeaderCard(
            "التحليلات والتقارير",
            "أداء الفروع، تقدير الهامش، حركة الأصناف، التحويلات، والمخزون على مستوى الشركة — ومن تبويب «حصر الفرع» تفاصيل البيع والوارد والبائع للمستودع المحدد.",
            subtitleItalic: false,
            DockStyle.Fill,
            autoSizeHeight: false,
            out _,
            out _);
        reportsPageHeader.Margin = new Padding(0, 0, 0, 8);
        reportsChrome.Controls.Add(reportsPageHeader, 0, 0);

        var toolbarHintLbl = new Label
        {
            Text = "البطاقات وقوائم المنتجات أدناه للمستودع المحدد. جدول المقارنة يشمل كل المواقع (صفر طبيعي إن لم تُستخدم نقطة البيع بعد هناك).",
            AutoSize = true,
            Dock = DockStyle.Fill,
            ForeColor = ModuleHeaderSubtitleForeColor,
            Font = new Font(ModuleHeaderSubtitleFont, FontStyle.Italic),
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.No,
            Margin = new Padding(0, 4, 0, 10),
            UseCompatibleTextRendering = false
        };

        void SyncReportsChromeWrapWidths()
        {
            var w = Math.Max(320, reportsChrome.ClientSize.Width - reportsChrome.Padding.Horizontal);
            toolbarHintLbl.MaximumSize = new Size(w, 0);
        }
        reportsChrome.HandleCreated += (_, _) => SyncReportsChromeWrapWidths();
        reportsChrome.Resize += (_, _) => SyncReportsChromeWrapWidths();

        var filterFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            // LTR host: first control in collection is placed on the physical right (matches Arabic toolbar).
            RightToLeft = RightToLeft.No,
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
            RightToLeft = RightToLeft.No,
            Padding = new Padding(4, 4, 4, 4),
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent
        };

        reportsChrome.Controls.Add(filterFlow, 0, 1);
        reportsChrome.Controls.Add(toolbarHintLbl, 0, 2);
        reportsChrome.Controls.Add(exportAndNavRow, 0, 3);

        var kpiFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 132,
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
            "يُحسب الهامش بمتوسط تكلفة الشراء المرجح في المستودع الرئيسي لكل منتج (تقدير). الخصومات تُنقص صافي المبيعات. الأرقام أعلاه (الإجمالي / الخصومات / تكلفة البضاعة) تُحدَّث مع الفترة والمستودع. قارن الفروع في الجدول، ثم راجع الأكثر مبيعاً والبطيئة الحركة والتحويلات.";

        var overviewDetailFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 118,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(14, 6, 14, 8),
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes,
            FlowDirection = FlowDirection.RightToLeft
        };
        overviewDetailFlow.Controls.Add(BuildAnalyticsKpiCard("الإجمالي قبل الخصم", _overviewGrossVal, Color.FromArgb(52, 73, 94), true));
        overviewDetailFlow.Controls.Add(BuildAnalyticsKpiCard("الخصومات", _overviewDiscountsVal, Color.FromArgb(192, 57, 43), true));
        overviewDetailFlow.Controls.Add(BuildAnalyticsKpiCard("تكلفة البضاعة المقدرة", _overviewCogsVal, Color.FromArgb(230, 126, 34), true));

        ApplyProfitModuleGridChrome(_salesByWarehouseGrid);
        ApplyProfitModuleGridChrome(_topProductsGrid);
        ApplyProfitModuleGridChrome(_slowMovingGrid);
        ApplyProfitModuleGridChrome(_reportLowStockGrid);
        ApplyProfitModuleGridChrome(_transferReportGrid);
        ApplyProfitModuleGridChrome(_warehouseInventoryGrid);
        ConfigureSalesByWarehouseReportColumns();
        ConfigureTopProductsReportColumns();
        ConfigureSlowMoversReportColumns();
        ConfigureReportLowStockColumns();
        ConfigureTransferReportColumns(_transferReportGrid);
        ConfigureWarehouseInventorySnapshotColumns();

        var branchSection = BuildReportModuleGridSection("١ · مقارنة المبيعات · كل المستودعات", _salesByWarehouseGrid, new Padding(0, 0, 0, 12));
        var topSection = BuildReportModuleGridSection("٢ · الأكثر مبيعاً (المستودع المحدد)", _topProductsGrid, new Padding(0, 0, 0, 12));
        var slowSection = BuildReportModuleGridSection(
            "٣ · بطيئة الحركة · أعد الطلب أو انقل المخزون (رصيد ≥ 1، مبيعات < 1 في المستودع المحدد خلال الفترة)",
            _slowMovingGrid,
            new Padding(0, 0, 0, 12));
        var lowStockSection = BuildReportModuleGridSection("٤ · مراقبة إعادة الطلب · أصناف عند الحد أو أقل (المستودع المحدد)", _reportLowStockGrid, new Padding(0, 0, 0, 12));
        var transferSection = BuildReportModuleGridSection("٥ · التحويلات · وارد أو صادر للمستودع المحدد خلال الفترة", _transferReportGrid, new Padding(0, 0, 0, 12));
        var warehouseInvSection = BuildReportModuleGridSection("٦ · أرصدة المخزون (من إجمالي حركات المخزون) · غير الصفر فقط", _warehouseInventoryGrid, Padding.Empty);

        var overviewScrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes
        };
        var overviewLayout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 10,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0, 0, 0, 16),
            BackColor = Color.Transparent
        };
        overviewLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132f));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126f));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 340f));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 320f));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 320f));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 268f));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 268f));
        overviewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 340f));

        void SyncOverviewScrollLayout(object? _, EventArgs __)
        {
            var innerW = overviewScrollHost.DisplayRectangle.Width;
            var w = Math.Max(320, innerW);
            overviewLayout.Width = w;
            _reportMetricsFootnote.MaximumSize = new Size(Math.Max(200, w - 24), 0);
        }

        overviewScrollHost.HandleCreated += SyncOverviewScrollLayout;
        overviewScrollHost.Resize += SyncOverviewScrollLayout;

        var bannerHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 6, 14, 4), BackColor = Color.FromArgb(245, 247, 250), RightToLeft = RightToLeft.Yes };
        _reportPeriodBanner.Dock = DockStyle.Fill;
        bannerHost.Controls.Add(_reportPeriodBanner);

        kpiFlow.Dock = DockStyle.Fill;
        _reportMetricsFootnote.Dock = DockStyle.Fill;

        overviewLayout.Controls.Add(bannerHost, 0, 0);
        overviewLayout.Controls.Add(kpiFlow, 0, 1);
        overviewLayout.Controls.Add(overviewDetailFlow, 0, 2);
        overviewLayout.Controls.Add(_reportMetricsFootnote, 0, 3);
        overviewLayout.Controls.Add(branchSection, 0, 4);
        overviewLayout.Controls.Add(topSection, 0, 5);
        overviewLayout.Controls.Add(slowSection, 0, 6);
        overviewLayout.Controls.Add(lowStockSection, 0, 7);
        overviewLayout.Controls.Add(transferSection, 0, 8);
        overviewLayout.Controls.Add(warehouseInvSection, 0, 9);

        overviewScrollHost.Controls.Add(overviewLayout);

        var overviewPanel = new Panel { RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(245, 247, 250) };
        overviewPanel.Controls.Add(overviewScrollHost);

        // ── Branch register: sales lines + incoming + seller rollup (same filters as report toolbar) ──
        var branchRegisterPanel = new Panel { RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(245, 247, 250) };
        var branchScrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes
        };
        var branchLayout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(14, 10, 14, 16),
            BackColor = Color.Transparent
        };
        branchLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        branchLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        branchLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        branchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 360f));
        branchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 340f));
        branchLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300f));

        var branchRegTitle = new Label
        {
            Text = "حصر الفرع — المبيعات والوارد والبائع",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 0, 0, 4),
            UseCompatibleTextRendering = false
        };
        var branchRegSubtitle = new Label
        {
            Text = "نفس «المستودع المحدد» و«من / إلى» في شريط التقرير أعلاه. ١) أسطر البيع مع العميل والبائع (منشئ الفاتورة). ٢) وارد الفرع: مشتريات مسجلة على هذا المستودع + تحويلات واردة. ٣) تجميع صافي المبيعات لكل بائع.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font(UiFont, FontStyle.Regular),
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 0, 0, 10),
            MaximumSize = new Size(980, 0),
            UseCompatibleTextRendering = false
        };

        void SyncBranchScrollLayout(object? _, EventArgs __)
        {
            var innerW = branchScrollHost.DisplayRectangle.Width;
            branchLayout.Width = Math.Max(320, innerW);
            branchRegSubtitle.MaximumSize = new Size(Math.Max(200, innerW - 28), 0);
        }

        branchScrollHost.HandleCreated += SyncBranchScrollLayout;
        branchScrollHost.Resize += SyncBranchScrollLayout;

        ApplyProfitModuleGridChrome(_branchSalesLinesGrid);
        ApplyProfitModuleGridChrome(_branchIncomingGrid);
        ApplyProfitModuleGridChrome(_branchSellersGrid);
        ConfigureBranchSalesLinesRegisterColumns(_branchSalesLinesGrid);
        ConfigureBranchIncomingRegisterColumns(_branchIncomingGrid);
        ConfigureBranchSellersSummaryColumns(_branchSellersGrid);

        var branchSalesSection = BuildReportModuleGridSection(
            "١ · حصر أسطر البيع — تفصيل كل صنف مع الفاتورة والعميل والبائع",
            _branchSalesLinesGrid,
            new Padding(0, 0, 0, 12));
        var branchIncomingSection = BuildReportModuleGridSection(
            "٢ · وارد الفرع — شراء للمستودع المحدد + تحويلات واردة خلال الفترة",
            _branchIncomingGrid,
            new Padding(0, 0, 0, 12));
        var branchSellersSection = BuildReportModuleGridSection(
            "٣ · ملخص البائعين — عدد الفواتير والأسطر والإجماليات حسب منشئ الفاتورة",
            _branchSellersGrid,
            Padding.Empty);

        branchLayout.Controls.Add(branchRegTitle, 0, 0);
        branchLayout.Controls.Add(branchRegSubtitle, 0, 1);
        branchLayout.Controls.Add(branchSalesSection, 0, 2);
        branchLayout.Controls.Add(branchIncomingSection, 0, 3);
        branchLayout.Controls.Add(branchSellersSection, 0, 4);
        branchScrollHost.Controls.Add(branchLayout);
        branchRegisterPanel.Controls.Add(branchScrollHost);

        var profitPanel = new Panel { RightToLeft = RightToLeft.Yes, Padding = new Padding(14, 10, 14, 12), BackColor = Color.FromArgb(245, 247, 250) };
        // Scroll the whole profitability column: KPI + filter + both grids need more vertical space than the tab often has.
        var profitScrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes
        };
        var profitLayout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.Transparent
        };
        profitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        profitLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        profitLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        profitLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126f));
        profitLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Stacked grids (no split): height comes from content; outer profitScrollHost scrolls when needed.
        profitLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        void SyncProfitScrollLayout(object? _, EventArgs __)
        {
            var innerW = profitScrollHost.DisplayRectangle.Width;
            profitLayout.Width = Math.Max(320, innerW);
        }

        profitScrollHost.HandleCreated += SyncProfitScrollLayout;
        profitScrollHost.Resize += SyncProfitScrollLayout;

        var profitTitle = new Label
        {
            Text = "الربحية التقديرية",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 0, 0, 4),
            UseCompatibleTextRendering = false
        };
        var profitSubtitle = new Label
        {
            Text = "مقارنة صافي الإيراد وتكلفة البضاعة المقدرة (من متوسط شراء المستودع الرئيسي) ثم الربح المقدّر — لكل فاتورة ولتجميع حسب الصنف.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            MaximumSize = new Size(980, 0),
            Font = new Font(UiFont, FontStyle.Regular),
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 0, 0, 10),
            UseCompatibleTextRendering = false
        };

        var profitKpiFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Height = 118,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(4, 4, 4, 6),
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes,
            FlowDirection = FlowDirection.RightToLeft
        };
        profitKpiFlow.Controls.Add(BuildAnalyticsKpiCard("إجمالي الإيراد", _profitKpiRevenueVal, Color.FromArgb(41, 128, 185), true));
        profitKpiFlow.Controls.Add(BuildAnalyticsKpiCard("تكلفة مقدّرة (رئيسي)", _profitKpiCogsVal, Color.FromArgb(230, 126, 34), true));
        profitKpiFlow.Controls.Add(BuildAnalyticsKpiCard("ربح مقدّر", _profitKpiProfitVal, Color.FromArgb(39, 174, 96), true));

        var profitFilterBar = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 52),
            BackColor = Color.White,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 12),
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes
        };
        _profitAllBranchesCheck.Font = UiFont;
        _profitAllBranchesCheck.Margin = new Padding(0, 2, 0, 0);
        _profitAllBranchesCheck.AutoSize = true;
        var profitFilterHint = new Label
        {
            Text = "عند التفعيل: تُحسب الأرقام أعلاه وجداول الأسفل على مستوى كل الفروع (للمدير فقط).",
            AutoSize = true,
            Font = new Font(UiFont, FontStyle.Italic),
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(16, 4, 0, 0),
            MaximumSize = new Size(720, 0),
            UseCompatibleTextRendering = false
        };
        var profitFilterInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoSize = true,
            Padding = Padding.Empty,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };
        profitFilterInner.Controls.Add(profitFilterHint);
        profitFilterInner.Controls.Add(_profitAllBranchesCheck);
        profitFilterBar.Controls.Add(profitFilterInner);

        ApplyProfitModuleGridChrome(_profitByInvoiceGrid);
        ApplyProfitModuleGridChrome(_profitByProductGrid);
        ConfigureProfitByInvoiceColumns();
        ConfigureProfitByProductColumns();

        var invoiceSection = BuildReportModuleGridSection("١ · حسب الفاتورة", _profitByInvoiceGrid, new Padding(0, 0, 0, 14));
        var productSection = BuildReportModuleGridSection("٢ · تجميع حسب الصنف", _profitByProductGrid, Padding.Empty);

        // Fixed row heights give each grid enough vertical room for headers + several rows; more rows scroll inside the grid.
        var profitGridsStack = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.Transparent
        };
        profitGridsStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        profitGridsStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 360f));
        profitGridsStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 360f));
        profitGridsStack.Controls.Add(invoiceSection, 0, 0);
        profitGridsStack.Controls.Add(productSection, 0, 1);

        profitLayout.Controls.Add(profitTitle, 0, 0);
        profitLayout.Controls.Add(profitSubtitle, 0, 1);
        profitLayout.Controls.Add(profitKpiFlow, 0, 2);
        profitLayout.Controls.Add(profitFilterBar, 0, 3);
        profitLayout.Controls.Add(profitGridsStack, 0, 4);
        profitScrollHost.Controls.Add(profitLayout);
        profitPanel.Controls.Add(profitScrollHost);

        var stockPanel = new Panel { RightToLeft = RightToLeft.Yes, BackColor = Color.FromArgb(245, 247, 250) };
        var stockScrollHost = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes
        };
        var stockLayout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(14, 10, 14, 16),
            BackColor = Color.Transparent
        };
        stockLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        stockLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stockLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stockLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        stockLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 360f));
        stockLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 360f));

        var stockTitle = new Label
        {
            Text = "المخزون وحركة الصنف",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 0, 0, 4),
            UseCompatibleTextRendering = false
        };
        var stockSubtitle = new Label
        {
            Text = "الجدول الأول يعرض أرصدة الأصناف مجمّعة من حركات المخزون حسب المستودع المحدد في شريط التقرير. الجدول الثاني يعرض حركات الصنف الذي تختاره أدناه خلال نفس الفترة.",
            AutoSize = true,
            Dock = DockStyle.Fill,
            Font = new Font(UiFont, FontStyle.Regular),
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            Margin = new Padding(0, 0, 0, 10),
            MaximumSize = new Size(980, 0),
            UseCompatibleTextRendering = false
        };

        var stockFilterBar = new Panel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 52),
            BackColor = Color.White,
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(0, 0, 0, 12),
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes
        };
        _reportHistoryProductCombo.RightToLeft = RightToLeft.Yes;
        _reportHistoryProductCombo.Width = 320;
        var stockFilterInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true,
            AutoSize = true,
            Padding = Padding.Empty,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };
        stockFilterInner.Controls.Add(_reportHistoryProductCombo);
        stockFilterInner.Controls.Add(new Label
        {
            Text = "الصنف لعرض الحركة في الجدول الثاني:",
            AutoSize = true,
            Margin = new Padding(12, 8, 0, 0),
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextPrimary,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false
        });
        stockFilterBar.Controls.Add(stockFilterInner);

        ApplyProfitModuleGridChrome(_stockFromMovementsGrid);
        ApplyProfitModuleGridChrome(_stockHistoryGrid);
        ConfigureStockFromMovementsColumns();
        ConfigureStockHistoryColumns();

        var stockMovementsSection = BuildReportModuleGridSection(
            "١ · أرصدة الأصناف من حركات المخزون (حسب المستودع المحدد أعلى التقرير)",
            _stockFromMovementsGrid,
            new Padding(0, 0, 0, 12));
        var stockHistorySection = BuildReportModuleGridSection(
            "٢ · تفصيل حركة الصنف المختار (الفترة من مرشحات التقرير)",
            _stockHistoryGrid,
            Padding.Empty);

        void SyncStockScrollLayout(object? _, EventArgs __)
        {
            var innerW = stockScrollHost.DisplayRectangle.Width;
            var w = Math.Max(320, innerW);
            stockLayout.Width = w;
            stockSubtitle.MaximumSize = new Size(Math.Max(200, w - 28), 0);
        }

        stockScrollHost.HandleCreated += SyncStockScrollLayout;
        stockScrollHost.Resize += SyncStockScrollLayout;

        stockLayout.Controls.Add(stockTitle, 0, 0);
        stockLayout.Controls.Add(stockSubtitle, 0, 1);
        stockLayout.Controls.Add(stockFilterBar, 0, 2);
        stockLayout.Controls.Add(stockMovementsSection, 0, 3);
        stockLayout.Controls.Add(stockHistorySection, 0, 4);
        stockScrollHost.Controls.Add(stockLayout);
        stockPanel.Controls.Add(stockScrollHost);

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

        var reportPanels = new[] { overviewPanel, branchRegisterPanel, profitPanel, stockPanel, xferPanel, cashPanel };
        var reportNavTitles = new[]
        {
            "ملخص الأداء",
            "حصر الفرع" + Environment.NewLine + "بيع · وارد · بائع",
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

        exportAndNavRow.Controls.Add(exportExcelBtn);
        for (var i = 0; i < reportPanels.Length; i++)
        {
            var idx = i;
            var b = CreateReportPillButton(reportNavTitles[i]);
            b.Click += (_, _) => SelectReportModuleView(idx);
            exportAndNavRow.Controls.Add(b);
            navButtons[i] = b;
        }

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

    /// <summary>Report block: title bar + grid (stable layout; avoids title/header overlap).</summary>
    private TableLayoutPanel BuildReportModuleGridSection(string sectionTitle, DataGridView grid, Padding outerMargin)
    {
        var wrap = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12, 12, 12, 12),
            Margin = outerMargin,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes
        };
        wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        wrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        wrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var titleBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(244, 246, 250),
            Padding = new Padding(12, 6, 12, 6)
        };
        titleBar.Controls.Add(new Label
        {
            Text = sectionTitle,
            Dock = DockStyle.Fill,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes,
            AutoSize = false,
            UseCompatibleTextRendering = false
        });

        grid.Dock = DockStyle.Fill;
        wrap.Controls.Add(titleBar, 0, 0);
        wrap.Controls.Add(grid, 0, 1);
        return wrap;
    }

    private static void ApplyProfitModuleGridChrome(DataGridView g)
    {
        StyleReportGrid(g);
        // Taller headers + padding so wrapped Arabic column titles are not clipped vertically.
        g.ColumnHeadersHeight = 64;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 14, 10, 14);
        g.RowTemplate.Height = 40;
        g.DefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
    }

    private void ConfigureProfitByInvoiceColumns()
    {
        _profitByInvoiceGrid.AutoGenerateColumns = false;
        _profitByInvoiceGrid.Columns.Clear();
        _profitByInvoiceGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.InvoiceNumber), HeaderText = "رقم الفاتورة", FillWeight = 18, MinimumWidth = 168, ReadOnly = true });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.InvoiceDateUtc), HeaderText = "الوقت (UTC)", FillWeight = 14, MinimumWidth = 128, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.WarehouseName), HeaderText = "الفرع", FillWeight = 12, MinimumWidth = 88, ReadOnly = true });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.NetRevenue), HeaderText = "صافي الإيراد", FillWeight = 13, MinimumWidth = 102, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.EstimatedCogs), HeaderText = "تكلفة مقدّرة", FillWeight = 13, MinimumWidth = 102, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.EstimatedGrossProfit), HeaderText = "ربح مقدّر", FillWeight = 13, MinimumWidth = 96, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByInvoiceGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvoiceProfitDto.MarginPercent), HeaderText = "هامش تقريبي %", FillWeight = 10, MinimumWidth = 88, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureProfitByProductColumns()
    {
        _profitByProductGrid.AutoGenerateColumns = false;
        _profitByProductGrid.Columns.Clear();
        _profitByProductGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.ProductName), HeaderText = "اسم الصنف", FillWeight = 30, MinimumWidth = 140, ReadOnly = true });
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.QuantitySold), HeaderText = "كمية مباعة", FillWeight = 12, MinimumWidth = 92, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.Revenue), HeaderText = "إجمالي الإيراد", FillWeight = 14, MinimumWidth = 102, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.EstimatedCogs), HeaderText = "تكلفة مقدّرة", FillWeight = 14, MinimumWidth = 102, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        _profitByProductGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(ProductProfitDto.EstimatedGrossProfit), HeaderText = "ربح مقدّر", FillWeight = 14, MinimumWidth = 96, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
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

    private void ConfigureBranchSalesLinesRegisterColumns() =>
        ConfigureBranchSalesLinesRegisterColumns(_branchSalesLinesGrid);

    private void ConfigureBranchSalesLinesRegisterColumns(DataGridView grid)
    {
        grid.AutoGenerateColumns = false;
        grid.Columns.Clear();
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.InvoiceDateUtc), HeaderText = "وقت الفاتورة (UTC)", FillWeight = 12, MinimumWidth = 120, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.InvoiceNumber), HeaderText = "رقم الفاتورة", FillWeight = 10, MinimumWidth = 100, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.WarehouseName), HeaderText = "الفرع", FillWeight = 8, MinimumWidth = 80, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.CustomerDisplay), HeaderText = "العميل", FillWeight = 14, MinimumWidth = 120, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.SellerUsername), HeaderText = "البائع", FillWeight = 10, MinimumWidth = 88, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.ProductName), HeaderText = "الصنف", FillWeight = 16, MinimumWidth = 120, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.Quantity), HeaderText = "الكمية", FillWeight = 8, MinimumWidth = 72, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.UnitPrice), HeaderText = "سعر الوحدة", FillWeight = 8, MinimumWidth = 88, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.LineTotal), HeaderText = "صافي السطر", FillWeight = 8, MinimumWidth = 88, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.InvoiceSubtotal), HeaderText = "إجمالي الفاتورة", FillWeight = 8, MinimumWidth = 88, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.InvoiceDiscount), HeaderText = "خصم الفاتورة", FillWeight = 8, MinimumWidth = 80, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSalesLineRegisterDto.InvoiceTotal), HeaderText = "صافي الفاتورة", FillWeight = 8, MinimumWidth = 88, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
    }

    private void ConfigureBranchIncomingRegisterColumns() =>
        ConfigureBranchIncomingRegisterColumns(_branchIncomingGrid);

    private void ConfigureBranchIncomingRegisterColumns(DataGridView grid)
    {
        grid.AutoGenerateColumns = false;
        grid.Columns.Clear();
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchIncomingRegisterDto.EntryDateUtc), HeaderText = "التاريخ (UTC)", FillWeight = 14, MinimumWidth = 120, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchIncomingRegisterDto.EntryType), HeaderText = "نوع الوارد", FillWeight = 12, MinimumWidth = 100, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchIncomingRegisterDto.ProductName), HeaderText = "الصنف", FillWeight = 22, MinimumWidth = 140, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchIncomingRegisterDto.Quantity), HeaderText = "الكمية", FillWeight = 10, MinimumWidth = 80, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchIncomingRegisterDto.AmountValue), HeaderText = "قيمة الشراء", FillWeight = 10, MinimumWidth = 88, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchIncomingRegisterDto.SourceDetail), HeaderText = "المصدر", FillWeight = 14, MinimumWidth = 100, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchIncomingRegisterDto.Notes), HeaderText = "ملاحظات", FillWeight = 12, MinimumWidth = 88, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchIncomingRegisterDto.CreatedByDisplay), HeaderText = "المستخدم", FillWeight = 10, MinimumWidth = 88, ReadOnly = true });
    }

    private void ConfigureBranchSellersSummaryColumns() =>
        ConfigureBranchSellersSummaryColumns(_branchSellersGrid);

    private void ConfigureBranchSellersSummaryColumns(DataGridView grid)
    {
        grid.AutoGenerateColumns = false;
        grid.Columns.Clear();
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSellerSalesSummaryDto.SellerUsername), HeaderText = "البائع (المستخدم)", FillWeight = 22, MinimumWidth = 120, ReadOnly = true });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSellerSalesSummaryDto.InvoiceCount), HeaderText = "عدد الفواتير", FillWeight = 12, MinimumWidth = 88, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSellerSalesSummaryDto.LineItemCount), HeaderText = "عدد أسطر البيع", FillWeight = 12, MinimumWidth = 96, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSellerSalesSummaryDto.InvoicesGrossSubtotal), HeaderText = "إجمالي قبل الخصم", FillWeight = 14, MinimumWidth = 108, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSellerSalesSummaryDto.InvoicesDiscountTotal), HeaderText = "الخصومات", FillWeight = 12, MinimumWidth = 96, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(BranchSellerSalesSummaryDto.InvoicesNetTotal), HeaderText = "صافي المبيعات", FillWeight = 14, MinimumWidth = 108, ReadOnly = true, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight } });
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
    private void ApplyReportsVisualStyle()
    {
        _reportPeriodBanner.Font = UiFontSection;
        _reportPeriodBanner.RightToLeft = RightToLeft.Yes;
        foreach (var l in new[]
                 {
                     _kpiNetSalesVal, _kpiInvoicesVal, _kpiAvgTicketVal, _kpiEstProfitVal, _kpiStockValueVal, _kpiLowStockVal,
                     _overviewGrossVal, _overviewDiscountsVal, _overviewCogsVal,
                     _profitKpiRevenueVal, _profitKpiCogsVal, _profitKpiProfitVal
                 })
            l.Font = new Font("Segoe UI", 16.5f, FontStyle.Bold, GraphicsUnit.Point);
    }
}
