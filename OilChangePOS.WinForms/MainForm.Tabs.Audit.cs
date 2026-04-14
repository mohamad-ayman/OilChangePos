using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm
{
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
    private sealed class AuditRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal SystemQuantity { get; set; }
        public decimal ActualQuantity { get; set; }
        public string ReasonCode { get; set; } = StockAuditReasonCodes.PhysicalCount;
    }
}
