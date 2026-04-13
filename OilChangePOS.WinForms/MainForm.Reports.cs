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
    private void ApplyReportsVisualStyle()
    {
        _reportPeriodBanner.Font = UiFontSection;
        _reportPeriodBanner.RightToLeft = RightToLeft.Yes;
        foreach (var l in new[] { _kpiNetSalesVal, _kpiInvoicesVal, _kpiAvgTicketVal, _kpiEstProfitVal, _kpiStockValueVal, _kpiLowStockVal })
            l.Font = new Font("Segoe UI", 16.5f, FontStyle.Bold, GraphicsUnit.Point);
    }
}
