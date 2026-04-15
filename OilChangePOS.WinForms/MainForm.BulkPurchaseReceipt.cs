using System.ComponentModel;
using System.Globalization;
using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.WinForms;

public partial class MainForm
{
    private sealed class BulkPurchaseLineModel
    {
        public int ProductId { get; set; }
        public decimal Quantity { get; set; } = 1;
        public decimal UnitPurchasePrice { get; set; }
        public DateTime PurchaseDate { get; set; } = DateTime.Today;
        public DateTime ProductionDate { get; set; } = DateTime.Today;
        public string LineNote { get; set; } = string.Empty;
    }

    private TabPage BuildBulkPurchaseTab()
    {
        var tab = new TabPage("استلام مشتريات")
        {
            BackColor = Color.FromArgb(242, 245, 249),
            RightToLeft = RightToLeft.Yes
        };
        _bulkPurchaseTabPage = tab;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16, 12, 16, 14),
            BackColor = Color.FromArgb(242, 245, 249),
            RightToLeft = RightToLeft.No
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var header = BuildStandardModuleHeaderCard(
            "استلام مشتريات — فاتورة مورد (عدة أصناف)",
            "سجّل عدة أصناف في عملية شراء واحدة من نفس المورد (مثال: توريد من شركة الديب). يُرحَّل المخزون إلى المستودع الرئيسي دفعة واحدة عند «تسجيل الاستلام».",
            subtitleItalic: false,
            DockStyle.Fill,
            autoSizeHeight: true,
            out _,
            out _);
        header.Margin = new Padding(0, 0, 0, 12);

        _bulkPurchaseHintLabel.Text =
            "املأ اسم المورد ثم الأسطر: الصنف، الكمية، سعر الشراء، تاريخ الشراء، تاريخ الإنتاج لكل سطر. الصفوف بدون صنف أو بكمية صفر تُتجاهل.";

        var headerFields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(14, 12, 14, 12),
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };
        headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        headerFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        headerFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));

        foreach (var c in new Control[] { _bulkPurchaseSupplierEdit, _bulkPurchaseMemoEdit })
        {
            c.Font = UiFont;
            c.Margin = new Padding(0, 4, 0, 6);
        }

        headerFields.Controls.Add(new Label
        {
            Text = "اسم المورد",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextSecondary,
            Padding = new Padding(0, 8, 10, 0),
            RightToLeft = RightToLeft.No
        }, 0, 0);
        headerFields.SetColumnSpan(_bulkPurchaseSupplierEdit, 3);
        headerFields.Controls.Add(_bulkPurchaseSupplierEdit, 1, 0);

        headerFields.Controls.Add(new Label
        {
            Text = "ملاحظات / رقم الفاتورة",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextSecondary,
            Padding = new Padding(0, 8, 10, 0),
            RightToLeft = RightToLeft.No
        }, 0, 1);
        headerFields.SetColumnSpan(_bulkPurchaseMemoEdit, 3);
        headerFields.Controls.Add(_bulkPurchaseMemoEdit, 1, 1);

        var headerStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 1,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };
        headerStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        headerStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        headerStack.Controls.Add(headerFields, 0, 0);

        var headerCard = BuildCard();
        headerCard.Dock = DockStyle.Fill;
        headerCard.Padding = new Padding(0);
        headerCard.Margin = new Padding(0, 0, 0, 12);
        headerCard.Controls.Add(headerStack);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10),
            RightToLeft = RightToLeft.No
        };
        var postBtn = BuildButton("تسجيل الاستلام في المستودع الرئيسي", Color.FromArgb(39, 174, 96));
        postBtn.Margin = new Padding(10, 0, 0, 0);
        postBtn.RightToLeft = RightToLeft.Yes;
        SizeWarehouseButtonToFitText(postBtn, 220, 420);
        postBtn.Click += async (_, _) => await PostBulkPurchaseReceiptAsync();

        var clearBtn = BuildButton("مسح الأسطر", Color.FromArgb(52, 73, 94));
        clearBtn.Margin = new Padding(10, 0, 0, 0);
        clearBtn.RightToLeft = RightToLeft.Yes;
        clearBtn.Click += (_, _) => ResetBulkPurchaseLines();

        var removeBtn = BuildButton("حذف السطر المحدد", Color.FromArgb(192, 57, 43));
        removeBtn.Margin = new Padding(10, 0, 0, 0);
        removeBtn.RightToLeft = RightToLeft.Yes;
        removeBtn.Click += (_, _) => RemoveSelectedBulkPurchaseLine();

        var addBtn = BuildButton("إضافة سطر", Color.FromArgb(52, 152, 219));
        addBtn.Margin = new Padding(10, 0, 0, 0);
        addBtn.RightToLeft = RightToLeft.Yes;
        addBtn.Click += (_, _) => AddBulkPurchaseLine();

        toolbar.Controls.Add(postBtn);
        toolbar.Controls.Add(clearBtn);
        toolbar.Controls.Add(removeBtn);
        toolbar.Controls.Add(addBtn);

        var gridCard = BuildCard();
        gridCard.Dock = DockStyle.Fill;
        gridCard.Padding = new Padding(12, 10, 12, 12);
        ConfigureBulkPurchaseLinesGrid();
        StyleReportGrid(_bulkPurchaseLinesGrid);
        ApplyReportGridColumnBiDiAlignment(_bulkPurchaseLinesGrid);
        _bulkPurchaseLinesGrid.ColumnHeadersHeight = 52;
        _bulkPurchaseLinesGrid.RowTemplate.Height = 40;
        _bulkPurchaseLinesGrid.Margin = new Padding(0);
        _bulkPurchaseLinesGrid.DataError += (_, e) => e.ThrowException = false;
        _bulkPurchaseLinesGrid.CellParsing += BulkPurchaseLinesGridOnCellParsing;
        gridCard.Controls.Add(_bulkPurchaseLinesGrid);

        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            RightToLeft = RightToLeft.No
        };
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        inner.Controls.Add(header, 0, 0);
        inner.Controls.Add(_bulkPurchaseHintLabel, 0, 1);
        inner.Controls.Add(headerCard, 0, 2);
        inner.Controls.Add(toolbar, 0, 3);
        inner.Controls.Add(gridCard, 0, 4);

        root.Controls.Add(inner, 0, 0);
        root.SetRowSpan(inner, 4);
        tab.Controls.Add(root);

        ResetBulkPurchaseLines();
        return tab;
    }

    private void ConfigureBulkPurchaseLinesGrid()
    {
        var colProduct = new DataGridViewComboBoxColumn
        {
            Name = nameof(BulkPurchaseLineModel.ProductId),
            DataPropertyName = nameof(BulkPurchaseLineModel.ProductId),
            HeaderText = "الصنف",
            DisplayMember = nameof(MainWarehouseCatalogRow.Caption),
            ValueMember = nameof(MainWarehouseCatalogRow.Id),
            FlatStyle = FlatStyle.Flat,
            FillWeight = 36,
            MinimumWidth = 240
        };
        _bulkPurchaseLinesGrid.Columns.Add(colProduct);
        _bulkPurchaseLinesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(BulkPurchaseLineModel.Quantity),
            DataPropertyName = nameof(BulkPurchaseLineModel.Quantity),
            HeaderText = "الكمية",
            FillWeight = 12,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleLeft }
        });
        _bulkPurchaseLinesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(BulkPurchaseLineModel.UnitPurchasePrice),
            DataPropertyName = nameof(BulkPurchaseLineModel.UnitPurchasePrice),
            HeaderText = "سعر الشراء (وحدة)",
            FillWeight = 12,
            DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleLeft }
        });
        var dateStyle = new DataGridViewCellStyle
        {
            Format = "yyyy-MM-dd",
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            NullValue = DateTime.Today
        };
        _bulkPurchaseLinesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(BulkPurchaseLineModel.PurchaseDate),
            DataPropertyName = nameof(BulkPurchaseLineModel.PurchaseDate),
            HeaderText = "تاريخ الشراء",
            FillWeight = 12,
            DefaultCellStyle = dateStyle
        });
        _bulkPurchaseLinesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(BulkPurchaseLineModel.ProductionDate),
            DataPropertyName = nameof(BulkPurchaseLineModel.ProductionDate),
            HeaderText = "تاريخ الإنتاج",
            FillWeight = 12,
            DefaultCellStyle = dateStyle
        });
        _bulkPurchaseLinesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nameof(BulkPurchaseLineModel.LineNote),
            DataPropertyName = nameof(BulkPurchaseLineModel.LineNote),
            HeaderText = "ملاحظة السطر",
            FillWeight = 16
        });
    }

    private void ApplyBulkPurchaseProductColumnDataSource()
    {
        if (_bulkPurchaseLinesGrid.Columns.Count == 0)
            return;
        if (_bulkPurchaseLinesGrid.Columns[nameof(BulkPurchaseLineModel.ProductId)] is not DataGridViewComboBoxColumn col)
            return;
        col.DataSource = null;
        col.DataSource = _bulkPurchaseProductComboDataSource;
        col.DisplayMember = nameof(MainWarehouseCatalogRow.Caption);
        col.ValueMember = nameof(MainWarehouseCatalogRow.Id);
    }

    private void ResetBulkPurchaseLines()
    {
        var list = new BindingList<BulkPurchaseLineModel>();
        for (var i = 0; i < 8; i++)
            list.Add(new BulkPurchaseLineModel());
        _bulkPurchaseBinding.DataSource = list;
        _bulkPurchaseLinesGrid.DataSource = _bulkPurchaseBinding;
        ApplyBulkPurchaseProductColumnDataSource();
    }

    private void AddBulkPurchaseLine()
    {
        if (_bulkPurchaseBinding.DataSource is not BindingList<BulkPurchaseLineModel> list)
            return;
        list.Add(new BulkPurchaseLineModel());
    }

    private void RemoveSelectedBulkPurchaseLine()
    {
        if (_bulkPurchaseBinding.DataSource is not BindingList<BulkPurchaseLineModel> list)
            return;
        if (_bulkPurchaseLinesGrid.CurrentRow is null || _bulkPurchaseLinesGrid.CurrentRow.IsNewRow)
            return;
        var idx = _bulkPurchaseLinesGrid.CurrentRow.Index;
        if (idx < 0 || idx >= list.Count)
            return;
        if (list.Count <= 1)
        {
            MessageBox.Show("يجب أن يبقى سطر واحد على الأقل.", "استلام مشتريات", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        list.RemoveAt(idx);
    }

    private void BulkPurchaseLinesGridOnCellParsing(object? sender, DataGridViewCellParsingEventArgs e)
    {
        if (e.ColumnIndex < 0)
            return;
        var colName = _bulkPurchaseLinesGrid.Columns[e.ColumnIndex].Name;
        if (colName != nameof(BulkPurchaseLineModel.PurchaseDate) && colName != nameof(BulkPurchaseLineModel.ProductionDate))
            return;
        var text = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return;
        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
            || DateTime.TryParse(text, CultureInfo.GetCultureInfo("ar-SA"), DateTimeStyles.None, out d))
        {
            e.Value = d.Date;
            e.ParsingApplied = true;
        }
    }

    private bool TryBuildBulkPurchaseLineRequests(out List<PurchaseReceiptLineInput> lines, out string? error)
    {
        lines = [];
        error = null;
        if (_bulkPurchaseBinding.DataSource is not BindingList<BulkPurchaseLineModel> list)
        {
            error = "بيانات الجدول غير مهيأة.";
            return false;
        }

        foreach (var m in list)
        {
            if (m.ProductId <= 0)
                continue;
            if (m.Quantity <= 0)
            {
                error = "كل سطر بصنف محدد يجب أن تكون كميته أكبر من صفر.";
                return false;
            }

            if (m.UnitPurchasePrice < 0)
            {
                error = "سعر الشراء لا يمكن أن يكون سالباً.";
                return false;
            }

            if (m.PurchaseDate == default || m.ProductionDate == default)
            {
                error = "أدخل تاريخ شراء وتاريخ إنتاج صالحين لكل سطر (صيغة yyyy-MM-dd أو التقويم المحلي).";
                return false;
            }

            lines.Add(new PurchaseReceiptLineInput(
                m.ProductId,
                m.Quantity,
                m.UnitPurchasePrice,
                m.PurchaseDate.Date,
                m.ProductionDate.Date,
                string.IsNullOrWhiteSpace(m.LineNote) ? null : m.LineNote.Trim()));
        }

        if (lines.Count == 0)
        {
            error = "أضف سطراً واحداً على الأقل بصنف وكمية صالحة.";
            return false;
        }

        return true;
    }

    private async Task PostBulkPurchaseReceiptAsync()
    {
        if (_currentUser.Role != UserRole.Admin)
        {
            MessageBox.Show("هذه الشاشة للمدير فقط.", "استلام مشتريات", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        _bulkPurchaseLinesGrid.EndEdit();
        if (!TryBuildBulkPurchaseLineRequests(out var lineInputs, out var parseErr))
        {
            MessageBox.Show(parseErr ?? "تعذر قراءة الأسطر.", "استلام مشتريات", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var main = await _warehouseService.GetMainAsync();
        if (main is null)
        {
            MessageBox.Show("لم يُعثر على المستودع الرئيسي.", "استلام مشتريات", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var supplier = _bulkPurchaseSupplierEdit.Text.Trim();
        if (string.IsNullOrEmpty(supplier))
        {
            MessageBox.Show("أدخل اسم المورد (مثال: شركة الديب).", "استلام مشتريات", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        try
        {
            var result = await _inventoryService.AddPurchaseReceiptBatchAsync(
                _currentUser.Id,
                main.Id,
                supplier,
                string.IsNullOrWhiteSpace(_bulkPurchaseMemoEdit.Text) ? null : _bulkPurchaseMemoEdit.Text.Trim(),
                lineInputs);

            MessageBox.Show(
                $"تم تسجيل {result.LinesPosted} سطراً في المستودع الرئيسي بنجاح.",
                "استلام مشتريات",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                MsgRtl);

            _bulkPurchaseSupplierEdit.Clear();
            _bulkPurchaseMemoEdit.Clear();
            ResetBulkPurchaseLines();
            await RefreshMainWarehouseGridAsync();
            await RefreshAllStockViewsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "استلام مشتريات", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }
}
