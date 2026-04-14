using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm
{
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
        root.Controls.Add(transferHeader);
        tab.Controls.Add(root);
        return tab;
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
    private sealed class TransferProductRow
    {
        public int ProductId { get; set; }
        public decimal AvailableQty { get; set; }
        public string Caption { get; set; } = string.Empty;
    }
}
