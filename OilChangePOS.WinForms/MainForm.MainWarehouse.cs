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
        void apply()
        {
            var t = string.IsNullOrEmpty(text) ? "—" : text;
            _mwAvailableStockLabel.Text = t;
            _mainWarehouseToolTip.SetToolTip(_mwAvailableStockLabel, t == "—" ? string.Empty : t);
        }
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
            RowCount = 3,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(16, 14, 16, 16),
            RightToLeft = RightToLeft.No
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90f)); // standard module header card
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f)); // excel
        // Form + grid stack scroll together when the window is short (same idea as analytics tabs).
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var roleAr = _currentUser.Role == UserRole.Admin ? "مدير" : "فرع";
        var headerPanel = BuildStandardModuleHeaderCard(
            "المستودع الرئيسي — المشتريات والدفعات",
            $"المستخدم: {_currentUser.Username}   ·   {roleAr}",
            subtitleItalic: false,
            DockStyle.Fill,
            autoSizeHeight: false,
            out _mwHeaderTitleLabel,
            out _mwHeaderUserLabel);
        headerPanel.Margin = new Padding(0, 0, 0, 10);

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
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.White,
            RightToLeft = RightToLeft.No
        };
        var importExcelButton = BuildSizedButton("استيراد من اكسل", Color.FromArgb(39, 174, 96), 168, 40);
        var exportExcelButton = BuildSizedButton("تصدير الى اكسل", Color.FromArgb(52, 152, 219), 168, 40);
        ApplyMainWarehouseFixedToolbarButtonTypography(importExcelButton, 168);
        ApplyMainWarehouseFixedToolbarButtonTypography(exportExcelButton, 168);
        // RTL row: first control sits on the physical right.
        importExcelButton.Margin = new Padding(10, 0, 0, 0);
        exportExcelButton.Margin = new Padding(10, 0, 0, 0);
        importExcelButton.TabIndex = 9;
        exportExcelButton.TabIndex = 10;
        importExcelButton.Click += async (_, _) => await ImportMainWarehouseExcelAsync();
        exportExcelButton.Click += async (_, _) => await ExportMainWarehouseExcelAsync();
        excelFlow.Controls.Add(exportExcelButton);
        excelFlow.Controls.Add(importExcelButton);
        excelCard.Controls.Add(excelFlow);
        _mainWarehouseToolTip.SetToolTip(importExcelButton, "استيراد كميات أو أصناف من ملف Excel إلى المستودع الرئيسي.");
        _mainWarehouseToolTip.SetToolTip(exportExcelButton, "تصدير أرصدة الأصناف في المستودع الرئيسي إلى ملف Excel.");

        // Split form vs grid so only the grid scrolls internally — avoids stacked page scroll + form scroll + grid scroll.
        var formGridSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            RightToLeft = RightToLeft.No,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(245, 247, 250),
            SplitterWidth = 6
        };
        // Do not set Panel*MinSize here — the control often has no real height yet and throws
        // InvalidOperationException. ApplyInitialSplitterDistance applies mins after measuring client size.
        // Favor the grid: keep the purchase form compact by default (Panel1 height ≈ preferred).
        formGridSplit.HandleCreated += (_, _) => ApplyInitialSplitterDistance(formGridSplit, 168, 110, 120);

        var middle = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1,
            Margin = Padding.Empty,
            RightToLeft = RightToLeft.No
        };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var leftCard = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = Color.White,
            Padding = new Padding(12, 8, 12, 8),
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.No,
            AutoScroll = true
        };

        var formShell = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.White,
            RightToLeft = RightToLeft.No,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        formShell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        formShell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formShell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formShell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formShell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        formShell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        void RefitMainWarehouseFormPanel()
        {
            if (!formGridSplit.IsHandleCreated || formGridSplit.ClientSize.Height <= 0) return;
            var desired = formShell.PreferredSize.Height + leftCard.Padding.Vertical + 10;
            var max = Math.Max(120, formGridSplit.ClientSize.Height - 120);
            var clamped = Math.Clamp(desired, 110, max);
            if (Math.Abs(formGridSplit.SplitterDistance - clamped) > 2)
                formGridSplit.SplitterDistance = clamped;
        }
        formGridSplit.HandleCreated += (_, _) => BeginInvoke((Action)RefitMainWarehouseFormPanel);

        var purchaseHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            RowCount = 1,
            RightToLeft = RightToLeft.No,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Height = 26
        };
        purchaseHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
        purchaseHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));

        ApplyModernWarehouseNumeric(_mwQuantity);
        ApplyModernWarehouseNumeric(_mwPurchasePrice);
        ApplyModernWarehouseDatePicker(_mwProductionDate);
        ApplyModernWarehouseDatePicker(_mwPurchaseDate);

        _mwProductionDate.Value = DateTime.Today;
        _mwPurchaseDate.Value = DateTime.Today;

        _mwProductLookup.SelectedIndexChanged += async (_, _) =>
        {
            if (_syncingMainWarehouseProductCombo) return;
            await RefreshMainWarehouseAvailableStockHintAsync();
            await SyncMainWarehouseBranchPricePanelAsync();
            SyncMainWarehouseCommandStates();
        };

        void MainWarehouseAddInputsChanged(object? s, EventArgs e) => SyncMainWarehouseCommandStates();
        _mwQuantity.TextChanged += MainWarehouseAddInputsChanged;
        _mwQuantity.ValueChanged += MainWarehouseAddInputsChanged;
        _mwPurchasePrice.TextChanged += MainWarehouseAddInputsChanged;
        _mwPurchasePrice.ValueChanged += MainWarehouseAddInputsChanged;
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
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0, 0, 0, 0),
            UseCompatibleTextRendering = false,
            Tag = MainWarehouseUiLabelTag
        };
        _mwAvailableStockLabel = new Label
        {
            Text = "—",
            Dock = DockStyle.Fill,
            AutoSize = false,
            AutoEllipsis = true,
            Font = MwFontSubtitle,
            ForeColor = UiTextSecondary,
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0, 0, 0, 0),
            UseCompatibleTextRendering = false,
            UseMnemonic = false,
            Tag = MainWarehouseUiLabelTag
        };
        purchaseHeader.Controls.Add(_mwAvailableStockLabel, 0, 0);
        purchaseHeader.Controls.Add(lblSectionInputs, 1, 0);

        _mwProductLookup.Font = MwFontInput;
        _mwProductLookup.ItemHeight = 20;
        _mwProductLookup.TabIndex = 0;
        // One RTL row: صنف، كمية، تكلفة الشراء، إنتاج الدفعة، تاريخ الشراء (بدون سعر بيع — يُضبط عند التحويل/الفرع).
        const int mwFieldGap = 6;
        var gap = new Padding(0, 0, mwFieldGap, 0);
        var purchaseFieldsRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 5,
            RowCount = 2,
            Padding = new Padding(0, 0, 0, 0),
            Margin = Padding.Empty,
            BackColor = Color.White,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RightToLeft = RightToLeft.Yes
        };
        purchaseFieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f)); // الصنف
        purchaseFieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12f)); // الكمية
        purchaseFieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14f)); // سعر الشراء
        purchaseFieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f)); // إنتاج الدفعة
        purchaseFieldsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f)); // تاريخ الشراء
        purchaseFieldsRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f)); // labels
        purchaseFieldsRow.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f)); // controls
        Label BuildTopLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Font = new Font(MwFontFieldLabel.FontFamily, 9f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = UiTextSecondary,
                BackColor = Color.White,
                TextAlign = ContentAlignment.MiddleRight,
                // Keep Arabic text but disable mirrored label layout so MiddleRight stays physically right.
                RightToLeft = RightToLeft.No,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 6, 0),
                UseCompatibleTextRendering = true,
                Tag = MainWarehouseUiLabelTag
            };
        }
        _mwProductLookup.Margin = gap;
        _mwQuantity.Margin = Padding.Empty;
        _mwPurchasePrice.Margin = Padding.Empty;
        _mwQuantity.TabIndex = 1;
        _mwPurchasePrice.TabIndex = 2;
        _mwQuantity.Margin = gap;
        _mwPurchasePrice.Margin = gap;
        _mwProductionDate.Margin = Padding.Empty;
        _mwPurchaseDate.Margin = Padding.Empty;
        _mwProductionDate.Margin = gap;
        _mwProductionDate.TabIndex = 3;
        _mwPurchaseDate.TabIndex = 4;
        _mwPurchaseDate.Margin = Padding.Empty;
        _mwProductLookup.Dock = DockStyle.Fill;
        _mwQuantity.Dock = DockStyle.Fill;
        _mwPurchasePrice.Dock = DockStyle.Fill;
        _mwProductionDate.Dock = DockStyle.Fill;
        _mwPurchaseDate.Dock = DockStyle.Fill;
        // Apply height caps after field layout is set.
        const int mwCtlH = 26;
        _mwProductLookup.MaximumSize = new Size(0, mwCtlH);
        _mwProductLookup.DropDownWidth = 420;
        _mwQuantity.MaximumSize = new Size(0, mwCtlH);
        _mwPurchasePrice.MaximumSize = new Size(0, mwCtlH);
        _mwProductionDate.MaximumSize = new Size(0, mwCtlH);
        _mwPurchaseDate.MaximumSize = new Size(0, mwCtlH);
        _mainWarehouseToolTip.SetToolTip(_mwQuantity, "كمية الشراء لهذه الدفعة.");
        _mainWarehouseToolTip.SetToolTip(_mwProductionDate, "تاريخ إنتاج الدفعة كما يُسجَّل في المستودع الرئيسي.");
        _mainWarehouseToolTip.SetToolTip(_mwPurchaseDate, "تاريخ تسجيل عملية الشراء لهذه الدفعة.");
        purchaseFieldsRow.Controls.Add(BuildTopLabel("الصنف (من الكتالوج)"), 0, 0);
        purchaseFieldsRow.Controls.Add(BuildTopLabel("الكمية"), 1, 0);
        purchaseFieldsRow.Controls.Add(BuildTopLabel("سعر الشراء"), 2, 0);
        purchaseFieldsRow.Controls.Add(BuildTopLabel("إنتاج الدفعة"), 3, 0);
        purchaseFieldsRow.Controls.Add(BuildTopLabel("تاريخ الشراء"), 4, 0);
        purchaseFieldsRow.Controls.Add(_mwProductLookup, 0, 1);
        purchaseFieldsRow.Controls.Add(_mwQuantity, 1, 1);
        purchaseFieldsRow.Controls.Add(_mwPurchasePrice, 2, 1);
        purchaseFieldsRow.Controls.Add(_mwProductionDate, 3, 1);
        purchaseFieldsRow.Controls.Add(_mwPurchaseDate, 4, 1);

        var formRowDivider = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            Margin = new Padding(0, 2, 0, 2),
            BackColor = Color.FromArgb(230, 230, 230)
        };

        ApplyModernWarehouseNumeric(_mwBranchRetailOverride);
        _mwBranchPriceWarehouseCombo.Font = MwFontInput;
        const int mwBranchBtnW = 112;
        const int mwBranchBtnH = 30;
        _mwBranchPriceSaveBtn = BuildSizedButton("حفظ", Color.FromArgb(39, 174, 96), mwBranchBtnW, mwBranchBtnH);
        _mwBranchPriceResetBtn = BuildSizedButton("إعادة", Color.FromArgb(108, 117, 125), mwBranchBtnW, mwBranchBtnH);
        ApplyMainWarehousePrimaryButtonTypography(_mwBranchPriceSaveBtn, mwBranchBtnW, mwBranchBtnW);
        ApplyMainWarehousePrimaryButtonTypography(_mwBranchPriceResetBtn, mwBranchBtnW, mwBranchBtnW);
        _mwBranchPriceSaveBtn.MinimumSize = new Size(mwBranchBtnW, mwBranchBtnH);
        _mwBranchPriceSaveBtn.MaximumSize = new Size(mwBranchBtnW, mwBranchBtnH);
        _mwBranchPriceResetBtn.MinimumSize = new Size(mwBranchBtnW, mwBranchBtnH);
        _mwBranchPriceResetBtn.MaximumSize = new Size(mwBranchBtnW, mwBranchBtnH);
        _mwBranchPriceSaveBtn.TextAlign = ContentAlignment.MiddleCenter;
        _mwBranchPriceResetBtn.TextAlign = ContentAlignment.MiddleCenter;
        _mwBranchPriceSaveBtn.UseCompatibleTextRendering = true;
        _mwBranchPriceResetBtn.UseCompatibleTextRendering = true;
        _mwBranchPriceSaveBtn.Margin = new Padding(3, 0, 3, 0);
        _mwBranchPriceResetBtn.Margin = new Padding(3, 0, 3, 0);
        _mwBranchPriceSaveBtn.Click += async (_, _) => await SaveMainWarehouseBranchPriceAsync();
        _mwBranchPriceResetBtn.Click += async (_, _) => await ResetMainWarehouseBranchPriceAsync();

        var branchMuted = Color.FromArgb(248, 249, 252);
        var branchBody = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = Padding.Empty,
            BackColor = branchMuted,
            RightToLeft = RightToLeft.Yes,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        branchBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
        branchBody.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16f));
        branchBody.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, (mwBranchBtnW * 2) + 18f));
        branchBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f)); // labels
        branchBody.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f)); // controls
        Label BuildBranchTopLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = false,
                Font = new Font(MwFontFieldLabel.FontFamily, 9f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = UiTextSecondary,
                BackColor = branchMuted,
                TextAlign = ContentAlignment.MiddleRight,
                // Keep physical right alignment for the Arabic caption.
                RightToLeft = RightToLeft.No,
                Margin = Padding.Empty,
                Padding = new Padding(0, 0, 6, 0),
                UseCompatibleTextRendering = true,
                Tag = MainWarehouseUiLabelTag
            };
        }
        _mwBranchPriceWarehouseCombo.Dock = DockStyle.Fill;
        _mwBranchRetailOverride.Dock = DockStyle.Fill;
        _mwBranchPriceWarehouseCombo.Margin = gap;
        _mwBranchRetailOverride.Margin = gap;
        _mwBranchPriceWarehouseCombo.MaximumSize = new Size(0, mwCtlH);
        _mwBranchRetailOverride.MaximumSize = new Size(0, mwCtlH);
        var branchActionsWrap = new Panel { Dock = DockStyle.Fill, BackColor = branchMuted };
        var branchActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = branchMuted,
            RightToLeft = RightToLeft.Yes
        };
        branchActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, mwBranchBtnW + 9f));
        branchActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, mwBranchBtnW + 9f));
        branchActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _mwBranchPriceSaveBtn.Dock = DockStyle.Fill;
        _mwBranchPriceResetBtn.Dock = DockStyle.Fill;
        branchActions.Controls.Add(_mwBranchPriceSaveBtn, 0, 0);
        branchActions.Controls.Add(_mwBranchPriceResetBtn, 1, 0);
        branchActionsWrap.Controls.Add(branchActions);
        branchBody.Controls.Add(BuildBranchTopLabel("الفرع"), 0, 0);
        branchBody.Controls.Add(BuildBranchTopLabel("سعر الفرع"), 1, 0);
        branchBody.Controls.Add(new Label { Dock = DockStyle.Fill, BackColor = branchMuted, Margin = Padding.Empty }, 2, 0);
        branchBody.Controls.Add(_mwBranchPriceWarehouseCombo, 0, 1);
        branchBody.Controls.Add(_mwBranchRetailOverride, 1, 1);
        branchBody.Controls.Add(branchActionsWrap, 2, 1);

        var branchHost = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.White
        };
        var branchExpanded = false;
        var branchToggle = new LinkLabel
        {
            Text = "\u25B6 سعر الفرع (اختياري)",
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes,
            LinkColor = Color.FromArgb(52, 152, 219),
            ActiveLinkColor = Color.FromArgb(41, 128, 185),
            VisitedLinkColor = Color.FromArgb(52, 152, 219),
            Font = MwFontFieldLabel,
            BackColor = branchMuted,
            Margin = new Padding(0, 0, 0, 0),
            Padding = new Padding(10, 0, 10, 2),
            AutoSize = false,
            LinkBehavior = LinkBehavior.NeverUnderline,
            Cursor = Cursors.Hand,
            UseCompatibleTextRendering = false,
            Tag = MainWarehouseUiLabelTag
        };
        _mainWarehouseToolTip.SetToolTip(branchToggle,
            "يتجاوز سعر القائمة للصنف في نقطة بيع الفرع المختار. «إعادة» تلغي التخصيص وتعود لسعر القائمة.");
        var branchInner = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Visible = false,
            Padding = new Padding(10, 8, 10, 8),
            Margin = new Padding(0, 2, 0, 0),
            BackColor = branchMuted,
            BorderStyle = BorderStyle.None,
            Tag = MainWarehouseUiLabelTag
        };
        branchInner.Paint += (s, e) =>
        {
            if (s is not Control c) return;
            using var pen = new Pen(Color.FromArgb(224, 226, 230), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        };
        branchInner.Controls.Add(branchBody);
        branchToggle.Click += (_, _) =>
        {
            branchExpanded = !branchExpanded;
            branchInner.Visible = branchExpanded;
            branchToggle.Text = branchExpanded ? "\u25BC سعر الفرع (اختياري)" : "\u25B6 سعر الفرع (اختياري)";
            BeginInvoke((Action)RefitMainWarehouseFormPanel);
        };
        branchHost.Controls.Add(branchInner);
        branchHost.Controls.Add(branchToggle);

        const int mwBtnW = 118;
        const int mwBtnH = 30;
        var opsBar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 2, 0, 0),
            Margin = Padding.Empty,
            BackColor = Color.FromArgb(248, 249, 252),
            RightToLeft = RightToLeft.Yes
        };
        for (var i = 0; i < 4; i++)
            opsBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        opsBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _mwCmdAdd = BuildSizedButton("إضافة دفعة", Color.FromArgb(39, 174, 96), mwBtnW, mwBtnH);
        _mwCmdUpdate = BuildSizedButton("حفظ التعديل", Color.FromArgb(41, 128, 185), mwBtnW, mwBtnH);
        _mwCmdDelete = BuildSizedButton("حذف الدفعة", Color.FromArgb(192, 57, 43), mwBtnW, mwBtnH);
        _mwCmdClear = BuildSizedButton("مسح الحقول", Color.FromArgb(108, 117, 125), mwBtnW, mwBtnH);
        ApplyMainWarehousePrimaryButtonTypography(_mwCmdAdd, 108, 200);
        ApplyMainWarehousePrimaryButtonTypography(_mwCmdUpdate, 108, 200);
        ApplyMainWarehousePrimaryButtonTypography(_mwCmdDelete, 108, 200);
        ApplyMainWarehousePrimaryButtonTypography(_mwCmdClear, 108, 200);
        _mwCmdAdd.Dock = DockStyle.Fill;
        _mwCmdUpdate.Dock = DockStyle.Fill;
        _mwCmdDelete.Dock = DockStyle.Fill;
        _mwCmdClear.Dock = DockStyle.Fill;
        var mwActPad = new Padding(3, 0, 3, 0);
        _mwCmdAdd.Margin = mwActPad;
        _mwCmdUpdate.Margin = mwActPad;
        _mwCmdDelete.Margin = mwActPad;
        _mwCmdClear.Margin = mwActPad;
        _mwCmdAdd.TabIndex = 5;
        _mwCmdUpdate.TabIndex = 6;
        _mwCmdDelete.TabIndex = 7;
        _mwCmdClear.TabIndex = 8;
        _mwCmdAdd.Click += async (_, _) => await AddMainWarehouseManualAsync();
        _mwCmdUpdate.Click += async (_, _) => await UpdateMainWarehouseManualAsync();
        _mwCmdDelete.Click += async (_, _) => await DeleteMainWarehouseManualAsync();
        _mwCmdClear.Click += (_, _) => ClearMainWarehouseForm();
        opsBar.Controls.Add(_mwCmdAdd, 0, 0);
        opsBar.Controls.Add(_mwCmdUpdate, 1, 0);
        opsBar.Controls.Add(_mwCmdDelete, 2, 0);
        opsBar.Controls.Add(_mwCmdClear, 3, 0);
        opsBar.Height = mwBtnH + 8;
        _mainWarehouseToolTip.SetToolTip(_mwCmdAdd, "تسجيل كمية شراء جديدة للصنف المختار (دفعة جديدة في المستودع الرئيسي).");
        _mainWarehouseToolTip.SetToolTip(_mwCmdUpdate, "تحديث الكمية أو تكلفة الشراء أو التواريخ لسطر الشراء المحدد في الجدول.");
        _mainWarehouseToolTip.SetToolTip(_mwCmdDelete, "حذف سجل الشراء المحدد في الجدول.");
        _mainWarehouseToolTip.SetToolTip(_mwCmdClear, "إفراغ الحقول دون حفظ. لا يغيّر بيانات الجدول.");
        _mainWarehouseToolTip.SetToolTip(_mwProductLookup, "اختر الصنف من الكتالوج؛ يظهر الاسم والشركة والنوع والعبوة في سطر واحد.");
        _mainWarehouseToolTip.SetToolTip(_mwPurchasePrice, "سعر الشراء (تكلفة الوحدة) لهذه الدفعة.");
        _mainWarehouseToolTip.SetToolTip(_mwBranchPriceSaveBtn, "حفظ سعر البيع لهذا الصنف في الفرع المختار (يتجاوز سعر القائمة للصنف في نقطة بيع الفرع).");
        _mainWarehouseToolTip.SetToolTip(_mwBranchPriceResetBtn, "إزالة تخصيص الفرع والعودة لسعر القائمة للصنف.");

        formShell.Controls.Add(purchaseHeader, 0, 0);
        formShell.Controls.Add(purchaseFieldsRow, 0, 1);
        formShell.Controls.Add(formRowDivider, 0, 2);
        formShell.Controls.Add(branchHost, 0, 3);
        formShell.Controls.Add(opsBar, 0, 4);
        leftCard.Controls.Add(formShell);
        leftCard.Resize += (_, _) => RefitMainWarehouseFormPanel();

        middle.Controls.Add(leftCard, 0, 0);

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
            Padding = new Padding(8, 6, 8, 6),
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes
        };
        var lblSectionGrid = new Label
        {
            Text = "السجل",
            Dock = DockStyle.Top,
            Height = 22,
            AutoSize = false,
            Font = MwFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 0, 0, 4),
            BackColor = Color.White,
            RightToLeft = RightToLeft.No,
            UseCompatibleTextRendering = false,
            Tag = MainWarehouseUiLabelTag
        };
        var pagerPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
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

        formGridSplit.Panel1.Controls.Add(middle);
        formGridSplit.Panel2.Controls.Add(gridCard);

        AttachWarehouseInputFocusCue(_mwQuantity);
        AttachWarehouseInputFocusCue(_mwPurchasePrice);
        AttachWarehouseInputFocusCue(_mwProductionDate);
        AttachWarehouseInputFocusCue(_mwPurchaseDate);
        AttachWarehouseInputFocusCue(_mwProductLookup);
        AttachWarehouseInputFocusCue(_mwBranchPriceWarehouseCombo);
        AttachWarehouseInputFocusCue(_mwBranchRetailOverride);

        root.Controls.Add(headerPanel, 0, 0);
        root.Controls.Add(excelCard, 0, 1);
        root.Controls.Add(formGridSplit, 0, 2);

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
        _mainWarehouseGrid.ColumnHeadersHeight = 46;
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _mainWarehouseGrid.RowTemplate.Height = 34;
        _mainWarehouseGrid.DefaultCellStyle.Font = MwFontGridCell;
        _mainWarehouseGrid.DefaultCellStyle.ForeColor = UiTextPrimary;
        _mainWarehouseGrid.DefaultCellStyle.Padding = new Padding(10, 6, 10, 6);
        _mainWarehouseGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 248, 251);
        _mainWarehouseGrid.AlternatingRowsDefaultCellStyle.Font = MwFontGridCell;
        _mainWarehouseGrid.AlternatingRowsDefaultCellStyle.ForeColor = UiTextPrimary;
        _mainWarehouseGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(200, 230, 255);
        _mainWarehouseGrid.DefaultCellStyle.SelectionForeColor = Color.Black;
        var headerBg = Color.FromArgb(45, 62, 80);
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.BackColor = headerBg;
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.Font = MwFontGridHeader;
        _mainWarehouseGrid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 6, 10, 6);
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
        TryGetMainWarehouseAddInputs(out _, out _, out _, out _, out _, out _);

    private bool TryGetMainWarehouseAddInputs(
        out MainWarehouseCatalogRow? catalog,
        out decimal quantity,
        out decimal purchasePrice,
        out DateTime productionDate,
        out DateTime purchaseDate,
        out string? errorMessage)
    {
        catalog = null;
        quantity = 0;
        purchasePrice = 0;
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

        return true;
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
        n.Height = 26;
        n.MinimumSize = new Size(Math.Max(56, n.MinimumSize.Width), 26);
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
        d.Height = 26;
        d.MinimumSize = new Size(Math.Max(78, d.MinimumSize.Width), 26);
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
            HeaderText = "التعبئة",
            FillWeight = 8,
            MinimumWidth = 88,
            ReadOnly = true
        });
        _mainWarehouseGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MainWarehouseRow.ProductCategory),
            HeaderText = "تصنيف المنتج",
            FillWeight = 8,
            MinimumWidth = 96,
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
                product = new Domain.Product
                {
                    CompanyId = companyRow.Id,
                    Name = name,
                    ProductCategory = category,
                    PackageSize = pack,
                    UnitPrice = 0,
                    IsActive = true
                };
                db.Products.Add(product);
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
            r++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(dialog.FileName);
        MessageBox.Show("تم تصدير مخزون المستودع الرئيسي.", "اكتمل التصدير", MessageBoxButtons.OK, MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1, MsgRtl);
    }

    private async Task AddMainWarehouseManualAsync()
    {
        if (!TryGetMainWarehouseAddInputs(out var catalog, out var quantity, out var purchasePrice, out var productionDate, out var purchaseDate, out var err))
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
        _ = RefreshMainWarehouseAvailableStockHintAsync();
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
            PackageSize = p.PackageSize
        }).ToList();

        var bulkComboList = new List<MainWarehouseCatalogRow>
        {
            new() { Id = 0, IsPlaceholder = true }
        };
        bulkComboList.AddRange(list);
        _bulkPurchaseProductComboDataSource = bulkComboList;

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

        ApplyBulkPurchaseProductColumnDataSource();

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
            _mwHeaderTitleLabel.Font = UiFontTitle;
            _mwHeaderTitleLabel.ForeColor = UiTextPrimary;
            _mwHeaderTitleLabel.BackColor = Color.White;
            _mwHeaderTitleLabel.RightToLeft = RightToLeft.No;
            _mwHeaderTitleLabel.UseCompatibleTextRendering = false;
        }

        if (_mwHeaderUserLabel is not null)
        {
            _mwHeaderUserLabel.Font = ModuleHeaderSubtitleFont;
            _mwHeaderUserLabel.ForeColor = ModuleHeaderSubtitleForeColor;
            _mwHeaderUserLabel.BackColor = Color.White;
            _mwHeaderUserLabel.RightToLeft = RightToLeft.No;
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
        /// <summary>When true, shown as the empty option in bulk purchase line combos.</summary>
        public bool IsPlaceholder { get; set; }
        public string Caption =>
            IsPlaceholder
                ? "— اختر الصنف —"
                : string.IsNullOrWhiteSpace(CompanyName)
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
        public DateTime PurchaseDate { get; set; }
        public string ProductCategory { get; set; } = string.Empty;
        public string PackageSize { get; set; } = string.Empty;
    }
}
