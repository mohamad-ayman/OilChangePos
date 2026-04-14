using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OilChangePOS.Business;

namespace OilChangePOS.WinForms;

/// <summary>Point-of-sale tab layout, product grid, cart actions, and POS DTOs.</summary>
public partial class MainForm
{
    private static string CategoryChipDisplay(string category) => category switch
    {
        "All" => "الكل",
        "Oil" => "زيت",
        "Filter" => "فلتر",
        "Grease" => "شحم",
        "Other" => "أخرى",
        _ => category
    };
    private async Task RefreshDailyKpisAsync()
    {
        try
        {
            var (whOk, branchId) = await TryResolvePosSaleWarehouseIdAsync();
            var whName = _posWarehouseCombo.SelectedItem is WarehouseDto w ? w.Name : "—";
            _kpiBranchNameVal.Text = whName;

            if (!whOk)
            {
                _kpiBranchSalesVal.Text = "—";
                _kpiBranchInvVal.Text = "—";
                _kpiBranchLowVal.Text = "—";
                return;
            }

            var report = await _reportService.GetDailySalesReportForWarehouseAsync(DateTime.UtcNow, branchId);
            _kpiBranchSalesVal.Text = $"{report.TotalSales:n0}{UiCurrencySuffix}";
            _kpiBranchInvVal.Text = report.InvoiceCount.ToString();

            var low = await _inventoryService.GetLowStockAsync(branchId);
            _kpiBranchLowVal.Text = low.Count.ToString();
        }
        catch
        {
            /* non-critical */
        }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // FINAL BuildPosTab — Dock-only layout, fully RTL-safe
    //
    // Strategy: use DockStyle for the main split; product area uses one RTL
    // TableLayoutPanel row for branch + qty + categories + search. Dock is not affected by RightToLeft.
    //
    // Structure:
    //   tab
    //   └─ host
    //      ├─ posTopBundle — title + subtitle + white KPI shell
    //      └─ workspace (Fill) — TableLayout RTL: col0 «السلة» | col1 «المنتجات»
    //           each cell: cool-gray pad + white inner card
    //
    // APPLY IN CLAUDE CODE:
    //   "Replace BuildPosTab() and RenderProductCards() in MainForm.cs with the
    //    code in /mnt/user-data/outputs/integrated/BuildPosTab_Final.cs
    //    Then run dotnet build and fix any errors."
    // ════════════════════════════════════════════════════════════════════════════

    private TabPage BuildPosTab()
    {
        var tab = new TabPage("البيع")
        {
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.FromArgb(241, 243, 248),
            UseVisualStyleBackColor = false
        };

        // ── Host: neutral container, no RTL interference ───────────────────────
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 243, 248),
            Padding = new Padding(14, 14, 14, 14),
            RightToLeft = RightToLeft.No // ← critical: prevents Dock mirroring
        };

        // ══════════════════════════════════════════════════════════════════════
        // 1. TOP CHROME (reports-style): title + subtitle + KPI strip
        // ══════════════════════════════════════════════════════════════════════
        var posTopBundle = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.FromArgb(241, 243, 248),
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(0, 0, 0, 0),
            RightToLeft = RightToLeft.Yes
        };
        posTopBundle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        posTopBundle.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f)); // matches ModuleHeaderTitleHeight
        posTopBundle.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        posTopBundle.RowStyles.Add(new RowStyle(SizeType.Absolute, 98f));

        var posTitleLbl = new Label
        {
            Text = "البيع",
            Dock = DockStyle.Fill,
            Font = UiFontTitle,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.No,
            Margin = new Padding(0, 0, 0, 2),
            UseCompatibleTextRendering = false
        };
        var posSubtitleLbl = new Label
        {
            Text = "يسار الشاشة: الأصناف المتاحة. يمين الشاشة: سلة الطلب ثم الملخص والدفع.",
            Dock = DockStyle.Fill,
            Font = ModuleHeaderSubtitleFont,
            ForeColor = ModuleHeaderSubtitleForeColor,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.No,
            Margin = new Padding(0, 0, 0, 4),
            UseCompatibleTextRendering = false
        };
        void SyncPosSubtitleWrap(object? _, EventArgs __)
        {
            var w = Math.Max(280, posTopBundle.ClientSize.Width - posTopBundle.Padding.Horizontal);
            posSubtitleLbl.MaximumSize = new Size(w, 0);
        }
        posTopBundle.HandleCreated += SyncPosSubtitleWrap;
        posTopBundle.Resize += SyncPosSubtitleWrap;

        var kpiShell = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.White
        };
        kpiShell.Paint += (s, e) =>
        {
            if (s is not Control c) return;
            using var pen = new Pen(Color.FromArgb(210, 214, 223));
            e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        };

        var kpiPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 0),
            BackColor = Color.White
        };

        // Build 4 equal KPI cards using Left/Width calculated on Resize
        var kpiCards = new[]
        {
            BuildKpiTile("مبيعات اليوم", _kpiBranchSalesVal, Color.FromArgb(39, 174, 96)),
            BuildKpiTile("عدد الفواتير", _kpiBranchInvVal, Color.FromArgb(33, 150, 243)),
            BuildKpiTile("أصناف منخفضة", _kpiBranchLowVal, Color.FromArgb(192, 57, 43)),
            BuildKpiTile("الفرع الحالي", _kpiBranchNameVal, Color.FromArgb(80, 80, 120)),
        };
        foreach (var c in kpiCards) kpiPanel.Controls.Add(c);

        void LayoutKpis(object? _, EventArgs __)
        {
            int gap = 8;
            int total = kpiPanel.ClientSize.Width;
            int w = (total - gap * 3) / 4;
            for (int i = 0; i < kpiCards.Length; i++)
            {
                kpiCards[i].SetBounds(i * (w + gap), 0, w, kpiPanel.ClientSize.Height);
            }
        }

        kpiPanel.Resize += LayoutKpis;
        kpiPanel.HandleCreated += LayoutKpis;

        posTopBundle.Controls.Add(posTitleLbl, 0, 0);
        posTopBundle.Controls.Add(posSubtitleLbl, 0, 1);
        kpiShell.Controls.Add(kpiPanel);
        posTopBundle.Controls.Add(kpiShell, 0, 2);

        // ══════════════════════════════════════════════════════════════════════
        // 2–3. WORKSPACE: two modules (RTL col0 = cart | col1 = products) on a cool gray pad
        // ══════════════════════════════════════════════════════════════════════
        var workspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 243, 248),
            Padding = new Padding(0, 2, 0, 0)
        };
        var mainSplit = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            RightToLeft = RightToLeft.Yes,
            BackColor = Color.Transparent,
            Padding = Padding.Empty
        };
        // Wider cart column so price/total columns are not truncated before payment.
        mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 468f));
        mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var cartModule = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 6, 0),
            Padding = new Padding(12),
            BackColor = Color.FromArgb(226, 230, 238)
        };
        var prodModule = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 10, 0),
            Padding = new Padding(14),
            BackColor = Color.FromArgb(226, 230, 238)
        };

        var cartCard = BuildCard();
        cartCard.Dock = DockStyle.Fill;

        // Cart header
        var cartHead = new Panel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = Color.White
        };
        cartHead.Paint += (s, e) =>
        {
            BorderBottom(s, e);
            if (s is Control c && c.Width > 4)
            {
                using var accent = new SolidBrush(Color.FromArgb(39, 174, 96));
                e.Graphics.FillRectangle(accent, c.Width - 4, 0, 4, c.Height);
            }
        };
        var cartHeadText = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12, 6, 14, 6),
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };
        cartHeadText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cartHeadText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        cartHeadText.Controls.Add(new Label
        {
            Text = "سلة الطلب",
            Dock = DockStyle.Fill,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes,
            AutoSize = true,
            UseCompatibleTextRendering = false
        }, 0, 0);
        cartHeadText.Controls.Add(new Label
        {
            Text = "أسطر الفاتورة الحالية",
            Dock = DockStyle.Fill,
            Font = new Font(UiFont.FontFamily, 9.25f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
            UseCompatibleTextRendering = false
        }, 0, 1);
        cartHead.Controls.Add(cartHeadText);

        // Remove button
        var removeBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(254, 245, 245)
        };
        removeBar.Paint += (s, e) =>
        {
            BorderBottom(s, e);
            if (s is Control c)
            {
                using var pen = new Pen(Color.FromArgb(252, 214, 214));
                e.Graphics.DrawLine(pen, 0, 0, c.Width, 0);
            }
        };
        var removeBtn = new Button
        {
            Text = "× إزالة الصنف المحدد",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(254, 235, 235),
            ForeColor = Color.FromArgb(180, 28, 28),
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false
        };
        removeBtn.FlatAppearance.BorderColor = Color.FromArgb(248, 187, 187);
        removeBtn.FlatAppearance.BorderSize = 1;
        removeBtn.Click += (_, _) => RemoveSelectedCartItem();
        removeBar.Controls.Add(removeBtn);

        // Cart grid — same RTL/header treatment as report grids
        StyleReportGrid(_cartGrid);
        _cartGrid.Dock = DockStyle.Fill;
        _cartGrid.DataSource = _cartBinding;
        _cartGrid.BackgroundColor = Color.White;
        _cartGrid.RowTemplate.Height = 46;
        _cartGrid.Font = new Font("Segoe UI", 11.5f, FontStyle.Regular, GraphicsUnit.Point);
        _cartGrid.DefaultCellStyle.Padding = new Padding(8, 5, 8, 5);
        _cartGrid.BorderStyle = BorderStyle.FixedSingle;
        _cartGrid.GridColor = Color.FromArgb(230, 232, 236);
        _cartGrid.RightToLeft = RightToLeft.Yes;
        _cartGrid.AutoGenerateColumns = false;
        _cartGrid.Columns.Clear();
        _cartGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CartRow.ProductName),
            HeaderText = "الصنف", ReadOnly = true,
            FillWeight = 38, MinimumWidth = 110
        });
        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CartRow.Quantity),
            HeaderText = "الكمية", FillWeight = 18, MinimumWidth = 76,
            DefaultCellStyle = new DataGridViewCellStyle
                { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CartRow.UnitPrice),
            HeaderText = "سعر الوحدة", ReadOnly = true,
            FillWeight = 22, MinimumWidth = 108,
            DefaultCellStyle = new DataGridViewCellStyle
                { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CartRow.LineTotal),
            HeaderText = "صافي السطر", ReadOnly = true,
            FillWeight = 24, MinimumWidth = 118,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Format = "N2",
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold, GraphicsUnit.Point)
            }
        });
        _cartGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);
        _cartGrid.ColumnHeadersHeight = 48;
        _cartGrid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 253, 255);
        _cartGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 236, 255);
        _cartGrid.DefaultCellStyle.SelectionForeColor = UiTextPrimary;

        // Cart line-items zone: caption + bordered host so empty cart is not a blank void.
        var cartLinesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(238, 242, 248),
            Padding = new Padding(8, 8, 8, 8),
            RightToLeft = RightToLeft.Yes
        };
        cartLinesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        cartLinesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        cartLinesLayout.MinimumSize = new Size(0, 140);

        var cartTableCaption = new Label
        {
            Text = "جدول أسطر السلة — الصنف، الكمية، سعر الوحدة، صافي السطر",
            Dock = DockStyle.Fill,
            Font = new Font(UiFont.FontFamily, 10.75f, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(10, 8, 10, 8),
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false
        };
        cartTableCaption.Paint += (s, e) =>
        {
            if (s is not Control c) return;
            using var pen = new Pen(Color.FromArgb(200, 208, 220));
            e.Graphics.DrawLine(pen, 0, c.Height - 1, c.Width, c.Height - 1);
        };
        cartLinesLayout.Controls.Add(cartTableCaption, 0, 0);

        var cartGridHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(186, 196, 210),
            Padding = new Padding(1)
        };

        var cartEmptyOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Visible = true
        };
        cartEmptyOverlay.Controls.Add(new Label
        {
            Text = "السلة فارغة.\nاضغط + بجانب أحد المنتجات لإضافته هنا.",
            Dock = DockStyle.Fill,
            Font = new Font(UiFont.FontFamily, 11f, FontStyle.Italic, GraphicsUnit.Point),
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(12),
            UseCompatibleTextRendering = false
        });

        _posCartEmptyOverlay = cartEmptyOverlay;
        _cartBinding.ListChanged += (_, _) => SyncPosCartEmptyOverlay();

        cartGridHost.Controls.Add(_cartGrid);
        cartGridHost.Controls.Add(cartEmptyOverlay);
        cartLinesLayout.Controls.Add(cartGridHost, 0, 1);

        // Bottom area — Dock=Top stacking: first added at top (عميل → خصم → ملخص → تنبيه قبل الدفع → زر الدفع).
        var cartBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 278,
            BackColor = Color.White,
            Padding = new Padding(10, 8, 10, 8)
        };
        cartBottom.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(Color.FromArgb(218, 220, 228)), 0, 0, cartBottom.Width, 0);

        // Customer row
        var custTbl = BuildTwoColRow("العميل:");
        _posCustomerCombo.Dock = DockStyle.Fill;
        _posCustomerCombo.Font = UiFont;
        _posCustomerCombo.Height = 30;
        custTbl.Controls.Add(_posCustomerCombo, 1, 0);

        // Discount row
        var discTbl = BuildTwoColRow("خصم:");
        _posDiscount.Dock = DockStyle.Fill;
        _posDiscount.Font = UiFont;
        _posDiscount.Height = 30;
        _posDiscount.DecimalPlaces = 2;
        _posDiscount.Maximum = 100000;
        _posDiscount.ValueChanged += (_, _) => RefreshCartSummary();
        discTbl.Controls.Add(_posDiscount, 1, 0);

        // Summary box
        var sumBox = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.FromArgb(247, 249, 251),
            Margin = new Padding(0, 0, 0, 6)
        };
        sumBox.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(218, 222, 230));
            e.Graphics.DrawRectangle(pen, 0, 0, sumBox.Width - 1, sumBox.Height - 1);
        };

        _subtotalValueLabel.ForeColor = Color.FromArgb(30, 30, 30);
        _discountValueLabel.ForeColor = Color.FromArgb(192, 57, 43);
        _totalValueLabel.ForeColor = Color.FromArgb(27, 94, 32);

        static void StyleSumValueCell(Label val, Font font)
        {
            val.AutoSize = false;
            val.Dock = DockStyle.Fill;
            val.Font = font;
            val.TextAlign = ContentAlignment.MiddleRight;
            val.RightToLeft = RightToLeft.Yes;
            val.UseCompatibleTextRendering = false;
        }

        var sumTbl = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(4, 2, 4, 2),
            BackColor = Color.FromArgb(247, 249, 251)
        };
        sumTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
        sumTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
        sumTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        sumTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        sumTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));

        static Label SumCaption(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(100, 100, 110),
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false
        };

        StyleSumValueCell(_subtotalValueLabel, new Font("Segoe UI", 11.5f, FontStyle.Bold, GraphicsUnit.Point));
        StyleSumValueCell(_discountValueLabel, new Font("Segoe UI", 11.5f, FontStyle.Bold, GraphicsUnit.Point));
        StyleSumValueCell(_totalValueLabel, new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Point));

        sumTbl.Controls.Add(SumCaption("المجموع الفرعي:"), 0, 0);
        sumTbl.Controls.Add(_subtotalValueLabel, 1, 0);
        sumTbl.Controls.Add(SumCaption("الخصم:"), 0, 1);
        sumTbl.Controls.Add(_discountValueLabel, 1, 1);
        sumTbl.Controls.Add(SumCaption("الإجمالي:"), 0, 2);
        sumTbl.Controls.Add(_totalValueLabel, 1, 2);
        sumBox.Controls.Add(sumTbl);

        _posCartQuickSummary.Paint += (s, e) =>
        {
            if (s is not Control c) return;
            using var pen = new Pen(Color.FromArgb(170, 200, 220));
            e.Graphics.DrawLine(pen, 0, 0, c.Width, 0);
            e.Graphics.DrawLine(pen, 0, c.Height - 1, c.Width, c.Height - 1);
        };

        // Pay button
        var payBtn = new Button
        {
            Text = "دفع وإصدار الفاتورة",
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false,
            Margin = new Padding(0, 0, 0, 4)
        };
        payBtn.FlatAppearance.BorderSize = 0;
        payBtn.Click += async (_, _) => await CompleteSaleAsync();

        cartBottom.Controls.Add(custTbl);
        cartBottom.Controls.Add(discTbl);
        cartBottom.Controls.Add(sumBox);
        cartBottom.Controls.Add(_posCartQuickSummary);
        cartBottom.Controls.Add(payBtn);

        cartCard.Controls.Add(cartBottom);
        cartCard.Controls.Add(cartLinesLayout);
        cartCard.Controls.Add(removeBar);
        cartCard.Controls.Add(cartHead);
        cartModule.Controls.Add(cartCard);

        var prodCard = BuildCard();
        prodCard.Dock = DockStyle.Fill;

        var prodHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.White
        };
        prodHeader.Paint += (s, e) =>
        {
            if (s is not Control c) return;
            using var accent = new SolidBrush(Color.FromArgb(41, 128, 185));
            e.Graphics.FillRectangle(accent, c.Width - 4, 0, 4, c.Height);
            using var line = new Pen(Color.FromArgb(218, 222, 230));
            e.Graphics.DrawLine(line, 0, c.Height - 1, c.Width, c.Height - 1);
        };
        var prodHeaderText = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12, 6, 14, 6),
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };
        prodHeaderText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        prodHeaderText.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        prodHeaderText.Controls.Add(new Label
        {
            Text = "الأصناف المتاحة",
            Dock = DockStyle.Fill,
            Font = UiFontSection,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes,
            AutoSize = true,
            UseCompatibleTextRendering = false
        }, 0, 0);
        prodHeaderText.Controls.Add(new Label
        {
            Text = "يُعرض فقط ما له رصيد في الفرع المختار. استخدم البحث والتصنيف ثم + لإضافة الكمية المعروضة.",
            Dock = DockStyle.Fill,
            Font = new Font(UiFont.FontFamily, 10.25f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
            UseCompatibleTextRendering = false
        }, 0, 1);
        prodHeader.Controls.Add(prodHeaderText);

        // Filter strip (reports-style toolbar: light panel + border)
        var filterStrip = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 56,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.FromArgb(252, 253, 255),
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(12, 10, 12, 10),
            Margin = new Padding(10, 0, 10, 10)
        };
        filterStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 198f));
        filterStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        filterStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232f));
        filterStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        filterStrip.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var branchCell = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(0, 2, 0, 0),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        branchCell.Controls.Add(new Label
        {
            Text = "الفرع:",
            AutoSize = true,
            Font = UiFontCaption,
            ForeColor = UiTextPrimary,
            Padding = new Padding(0, 4, 6, 0),
            Margin = new Padding(0, 0, 0, 0)
        });
        _posWarehouseCombo.Width = 148;
        _posWarehouseCombo.Height = 30;
        _posWarehouseCombo.Font = UiFont;
        _posWarehouseCombo.Margin = new Padding(0, 0, 0, 0);
        branchCell.Controls.Add(_posWarehouseCombo);

        var qtyCell = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 0),
            BackColor = Color.Transparent
        };
        qtyCell.Controls.Add(new Label
        {
            Text = "الكمية:",
            AutoSize = true,
            Font = UiFontCaption,
            ForeColor = UiTextPrimary,
            Padding = new Padding(0, 4, 6, 0)
        });
        _posAddQty.Width = 74;
        _posAddQty.Height = 30;
        _posAddQty.Margin = new Padding(0, 0, 0, 0);
        _posAddQty.Font = UiFont;
        _posAddQty.DecimalPlaces = 3;
        _posAddQty.Minimum = 0.001m;
        qtyCell.Controls.Add(_posAddQty);

        _categoryPanel.Dock = DockStyle.Fill;
        _categoryPanel.Margin = new Padding(0);
        _categoryPanel.Padding = new Padding(2, 2, 2, 0);
        _categoryPanel.BackColor = Color.Transparent;
        _categoryPanel.FlowDirection = FlowDirection.RightToLeft;
        _categoryPanel.WrapContents = false;
        _categoryPanel.AutoScroll = true;
        _categoryPanel.Height = 30;

        var searchHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 0, 0, 0), BackColor = Color.Transparent };
        _posSearchBox.Dock = DockStyle.Fill;
        _posSearchBox.MinimumSize = new Size(120, 28);
        _posSearchBox.Font = UiFont;
        _posSearchBox.PlaceholderText = "بحث عن صنف...";
        _posSearchBox.TextChanged += (_, _) => RenderProductCards();
        searchHost.Controls.Add(_posSearchBox);

        filterStrip.Controls.Add(branchCell, 0, 0);
        filterStrip.Controls.Add(qtyCell, 1, 0);
        filterStrip.Controls.Add(_categoryPanel, 2, 0);
        filterStrip.Controls.Add(searchHost, 3, 0);

        // Product cards scroll — distinct content well from toolbar (reports module body tone)
        _productCardsPanel.Dock = DockStyle.Fill;
        _productCardsPanel.AutoScroll = true;
        _productCardsPanel.WrapContents = true;
        _productCardsPanel.Padding = new Padding(14, 14, 14, 16);
        _productCardsPanel.BackColor = Color.FromArgb(248, 249, 252);

        prodCard.Controls.Add(_productCardsPanel);
        prodCard.Controls.Add(filterStrip);
        prodCard.Controls.Add(prodHeader);
        prodModule.Controls.Add(prodCard);

        mainSplit.Controls.Add(cartModule, 0, 0);
        mainSplit.Controls.Add(prodModule, 1, 0);
        workspace.Controls.Add(mainSplit);

        // ── Assemble host: top chrome first, then Fill workspace ─────────────────
        host.Controls.Add(posTopBundle);
        host.Controls.Add(workspace);

        tab.Controls.Add(host);
        return tab;
    }
    private void UpdatePosStockLocationLabel()
    {
        if (_posWarehouseCombo.SelectedItem is WarehouseDto w)
        {
            _mainWarehouseInfoLabel.Text =
                $"الفرع: {w.Name} — البيع من رصيد هذا الفرع فقط. ما في المستودع الرئيسي يظهر هنا بعد التحويل.";
            return;
        }

        _mainWarehouseInfoLabel.Text = "اختر فرعاً. المستودع الرئيسي منفصل — الاستلام هناك ثم التحويل للفرع.";
    }

    private async Task LoadPosCustomersAsync()
    {
        var list = new List<CustomerListDto> { new(0, "زبون عابر / بدون سجل") };
        list.AddRange(await _customerService.ListActiveAsync());
        _posCustomerCombo.DataSource = list;
        _posCustomerCombo.DisplayMember = nameof(CustomerListDto.DisplayName);
        _posCustomerCombo.ValueMember = nameof(CustomerListDto.Id);
    }

    private int? ResolvePosCustomerId()
    {
        var v = _posCustomerCombo.SelectedValue;
        if (v is int i && i > 0)
            return i;
        if (v is not null && int.TryParse(Convert.ToString(v), out var p) && p > 0)
            return p;
        return null;
    }
    private async Task<(bool Ok, int WarehouseId)> TryResolvePosSaleWarehouseIdAsync()
    {
        if (TryGetWarehouseIdFromCombo(_posWarehouseCombo, out var id))
            return (true, id);

        var branches = await _warehouseService.GetBranchesAsync();
        if (branches.FirstOrDefault() is { } b)
        {
            SyncWarehouseComboToWarehouseId(_posWarehouseCombo, b.Id);
            if (TryGetWarehouseIdFromCombo(_posWarehouseCombo, out id))
                return (true, id);
            return (true, b.Id);
        }

        var all = await _warehouseService.GetAllAsync();
        if (all.FirstOrDefault() is { } w)
        {
            SyncWarehouseComboToWarehouseId(_posWarehouseCombo, w.Id);
            if (TryGetWarehouseIdFromCombo(_posWarehouseCombo, out id))
                return (true, id);
            return (true, w.Id);
        }

        return (false, 0);
    }
    private async Task RefreshAvailableProductsAsync()
    {
        var (resolved, branchWarehouseId) = await TryResolvePosSaleWarehouseIdAsync();
        if (!resolved)
        {
            _availableProducts = [];
            BuildCategoryButtons();
            RenderProductCards();
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var products = await db.Products.AsNoTracking().Include(p => p.Company).Where(x => x.IsActive).ToListAsync();
        var productIds = products.ConvertAll(p => p.Id);
        var overrides = await _inventoryService.GetBranchSalePriceOverridesAsync(branchWarehouseId, productIds);
        var rows = new List<AvailableProductRow>();
        foreach (var product in products)
        {
            var stock = await _inventoryService.GetCurrentStockAsync(product.Id, branchWarehouseId);
            var unit = overrides.TryGetValue(product.Id, out var o) ? o : product.UnitPrice;
            rows.Add(new AvailableProductRow
            {
                ProductId = product.Id,
                CompanyName = product.Company?.Name ?? string.Empty,
                ProductName = product.Name,
                UnitPrice = unit,
                AvailableStock = stock,
                ProductType = product.ProductCategory
            });
        }

        // Order screen: only list SKUs with quantity at this branch (no out-of-stock cards).
        _availableProducts = rows
            .Where(x => x.AvailableStock > 0)
            .OrderBy(x => x.CompanyName)
            .ThenBy(x => x.ProductName)
            .ToList();
        BuildCategoryButtons();
        RenderProductCards();
    }
    private void AddProductToCart(AvailableProductRow selected)
    {
        var quantity = _posAddQty.Value;
        if (quantity <= 0 || quantity > selected.AvailableStock)
        {
            MessageBox.Show("الكمية يجب أن تكون أكبر من صفر ولا تتجاوز الرصيد المتاح.", "البيع", MessageBoxButtons.OK,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var cart = _cartBinding.List.Cast<CartRow>().ToList();
        var existing = cart.FirstOrDefault(x => x.ProductId == selected.ProductId);
        if (existing is null)
        {
            cart.Add(new CartRow
            {
                ProductId = selected.ProductId,
                ProductName = selected.ProductName,
                UnitPrice = selected.UnitPrice,
                Quantity = quantity
            });
        }
        else
        {
            var newQty = existing.Quantity + quantity;
            if (newQty > selected.AvailableStock)
            {
                MessageBox.Show("مجموع الكمية في السلة يتجاوز الرصيد المتاح.", "البيع", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                return;
            }
            existing.Quantity = newQty;
        }

        _cartBinding.DataSource = cart;
        RefreshCartSummary();
    }

    private void RemoveSelectedCartItem()
    {
        if (_cartGrid.CurrentRow?.DataBoundItem is not CartRow selected) return;
        var cart = _cartBinding.List.Cast<CartRow>().Where(x => x.ProductId != selected.ProductId).ToList();
        _cartBinding.DataSource = cart;
        RefreshCartSummary();
    }

    private async Task CompleteSaleAsync()
    {
        var cart = _cartBinding.List.Cast<CartRow>().ToList();
        if (!cart.Any())
        {
            MessageBox.Show("سلة البيع فارغة.", "البيع", MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var items = cart.Select(x => new SaleItemRequest(x.ProductId, x.Quantity)).ToList();
        int? customerId = ResolvePosCustomerId();

        try
        {
            var (whOk, branchWarehouseId) = await TryResolvePosSaleWarehouseIdAsync();
            if (!whOk)
                throw new InvalidOperationException("يرجى اختيار مستودع الفرع.");
            var invoiceId = await _salesService.CompleteSaleAsync(new CompleteSaleRequest(customerId, _posDiscount.Value, _currentUser.Id, branchWarehouseId, items));
            MessageBox.Show($"اكتمل البيع. رقم الفاتورة: {invoiceId}", "تم", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MsgRtl);
            _cartBinding.DataSource = new List<CartRow>();
            _posDiscount.Value = 0;
            await RefreshAllStockViewsAsync();
            RefreshCartSummary();
            await RefreshDailyKpisAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "فشل البيع", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }
    private void RefreshCartSummary()
    {
        var cart = _cartBinding.List.Cast<CartRow>().ToList();
        var subtotal = cart.Sum(x => x.LineTotal);
        var total = subtotal - _posDiscount.Value;

        _subtotalValueLabel.Text = subtotal.ToString("n2");
        _discountValueLabel.Text = _posDiscount.Value.ToString("n2");
        _totalValueLabel.Text = total.ToString("n2");

        _posCartQuickSummary.Text = cart.Count == 0
            ? "لا توجد أصناف في السلة بعد."
            : cart.Count == 1
                ? $"صنف واحد  ·  فرعي {subtotal:n2}{UiCurrencySuffix}  ·  للدفع {total:n2}{UiCurrencySuffix}"
                : $"{cart.Count} أصناف  ·  فرعي {subtotal:n2}{UiCurrencySuffix}  ·  للدفع {total:n2}{UiCurrencySuffix}";

        SyncPosCartEmptyOverlay();
    }

    private void SyncPosCartEmptyOverlay()
    {
        if (_posCartEmptyOverlay is null) return;
        try
        {
            _posCartEmptyOverlay.Visible = _cartBinding.Count == 0;
        }
        catch
        {
            // ignore: cart list may be transiently invalid during DataSource swaps
        }
    }
    private string ProductImagesMapPath => Path.Combine(AppContext.BaseDirectory, "product-images-map.json");

    private void LoadProductImageMap()
    {
        if (!File.Exists(ProductImagesMapPath))
        {
            _productImageMap = [];
            return;
        }

        var json = File.ReadAllText(ProductImagesMapPath);
        _productImageMap = JsonSerializer.Deserialize<Dictionary<int, string>>(json) ?? [];
    }

    private void SaveProductImageMap()
    {
        var json = JsonSerializer.Serialize(_productImageMap);
        File.WriteAllText(ProductImagesMapPath, json);
    }

    private void ShowImagePreview(string path, string title)
    {
        var dialog = new Form
        {
            Text = title,
            Width = 840,
            Height = 620,
            StartPosition = FormStartPosition.CenterParent
        };
        var box = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = Image.FromFile(path),
            BackColor = Color.Black
        };
        dialog.Controls.Add(box);
        dialog.ShowDialog(this);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // PRODUCT CARDS RENDERER — 190px wide cards, 3+ per row, price visible
    // Replace the entire body of RenderProductCards()
    // ════════════════════════════════════════════════════════════════════════════
    private void RenderProductCards()
    {
        _productCardsPanel.SuspendLayout();
        _productCardsPanel.Controls.Clear();

        var search = _posSearchBox.Text.Trim();

        IEnumerable<AvailableProductRow> source = _selectedCategory == "All"
            ? _availableProducts
            : _availableProducts.Where(x =>
                string.Equals(x.ProductType, _selectedCategory, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(search))
            source = source.Where(x =>
                x.ProductName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.CompanyName.Contains(search, StringComparison.OrdinalIgnoreCase));

        var list = source.ToList();

        foreach (var p in list)
        {
            bool isLow = p.AvailableStock > 0 && p.AvailableStock <= 5;
            bool outStock = p.AvailableStock <= 0;
            var borderClr = isLow ? Color.FromArgb(251, 140, 0) : Color.FromArgb(218, 222, 230);

            var card = new Panel
            {
                Width = 204,
                Height = 190,
                Margin = new Padding(7, 7, 7, 10),
                BackColor = Color.White
            };
            card.Paint += (s, e) =>
            {
                using var pen = new Pen(borderClr);
                e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
            };

            // Image placeholder
            var img = new PictureBox
            {
                Bounds = new Rectangle(1, 1, 202, 76),
                BackColor = Color.FromArgb(236, 242, 250),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            if (_productImageMap.TryGetValue(p.ProductId, out var imgPath) && File.Exists(imgPath))
            {
                img.Image = Image.FromFile(imgPath);
                img.Cursor = Cursors.Hand;
                img.Click += (_, _) => ShowImagePreview(imgPath, p.DisplayTitle);
            }
            else
            {
                var initials = (string.IsNullOrWhiteSpace(p.CompanyName) ? p.ProductName : p.CompanyName)
                    .Trim().Substring(0, Math.Min(2, (string.IsNullOrWhiteSpace(p.CompanyName) ? p.ProductName : p.CompanyName).Trim().Length));
                img.Paint += (s, e) =>
                {
                    e.Graphics.Clear(Color.FromArgb(230, 237, 248));
                    using var f = new Font("Segoe UI", 16f, FontStyle.Bold);
                    var sz = e.Graphics.MeasureString(initials, f);
                    e.Graphics.DrawString(initials, f,
                        new SolidBrush(Color.FromArgb(148, 172, 210)),
                        (img.Width - sz.Width) / 2f, (img.Height - sz.Height) / 2f);
                };
            }

            // Name
            var nameLbl = new Label
            {
                Text = p.DisplayTitle,
                Bounds = new Rectangle(8, 80, 188, 38),
                Font = new Font("Segoe UI", 11.25f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(25, 25, 35),
                RightToLeft = RightToLeft.Yes,
                TextAlign = ContentAlignment.TopRight
            };

            // Price
            var priceLbl = new Label
            {
                Text = $"{p.UnitPrice:n2}{UiCurrencySuffix}",
                Bounds = new Rectangle(8, 118, 188, 28),
                Font = new Font("Segoe UI", 12.5f, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.FromArgb(41, 128, 185),
                RightToLeft = RightToLeft.Yes,
                TextAlign = ContentAlignment.MiddleRight
            };

            // Stock
            string stockTxt = outStock ? "غير متاح"
                : isLow ? $"⚠ متاح: {p.AvailableStock:n0}"
                    : $"متاح: {p.AvailableStock:n0}";
            Color stockClr = outStock ? Color.FromArgb(150, 150, 150)
                : isLow ? Color.FromArgb(230, 81, 0)
                    : Color.FromArgb(46, 125, 50);

            var stockLbl = new Label
            {
                Text = stockTxt,
                Bounds = new Rectangle(8, 150, 150, 24),
                Font = new Font("Segoe UI", 10.25f, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = stockClr,
                RightToLeft = RightToLeft.Yes,
                TextAlign = ContentAlignment.MiddleRight
            };

            // Add button
            var addBtn = new Button
            {
                Text = "+",
                Bounds = new Rectangle(166, 148, 32, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = outStock ? Color.FromArgb(240, 240, 240) : Color.FromArgb(232, 244, 252),
                ForeColor = outStock ? Color.FromArgb(160, 160, 160) : Color.FromArgb(41, 128, 185),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Cursor = outStock ? Cursors.Default : Cursors.Hand,
                Enabled = !outStock,
                UseCompatibleTextRendering = false
            };
            addBtn.FlatAppearance.BorderColor = outStock
                ? Color.FromArgb(210, 210, 210)
                : Color.FromArgb(174, 214, 241);
            addBtn.FlatAppearance.BorderSize = 1;
            if (!outStock) addBtn.Click += (_, _) => AddProductToCart(p);

            card.Controls.AddRange(new Control[] { img, nameLbl, priceLbl, stockLbl, addBtn });
            _productCardsPanel.Controls.Add(card);
        }

        if (list.Count == 0)
        {
            _productCardsPanel.Controls.Add(new Label
            {
                Text = "لا توجد أصناف مطابقة. جرّب بحثاً آخر أو غيّر التصنيف أو الفرع.",
                AutoSize = true,
                Font = new Font(UiFont.FontFamily, 11.25f, FontStyle.Italic, GraphicsUnit.Point),
                ForeColor = UiTextSecondary,
                Margin = new Padding(20, 24, 20, 12),
                RightToLeft = RightToLeft.Yes,
                MaximumSize = new Size(Math.Max(260, _productCardsPanel.ClientSize.Width - 48), 0)
            });
        }

        _productCardsPanel.ResumeLayout();
    }

    private void BuildCategoryButtons()
    {
        _categoryPanel.Controls.Clear();
        var categories = _availableProducts
            .Select(x => x.ProductType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
        categories.Insert(0, "All");

        foreach (var category in categories)
        {
            var btn = new Button
            {
                Text = CategoryChipDisplay(category),
                Height = 34,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = _selectedCategory == category ? Color.FromArgb(41, 128, 185) : Color.White,
                ForeColor = _selectedCategory == category ? Color.White : Color.FromArgb(55, 65, 81),
                Font = new Font(UiFont.FontFamily, 10.75f, FontStyle.Bold, GraphicsUnit.Point)
            };
            btn.FlatAppearance.BorderSize = _selectedCategory == category ? 0 : 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(200, 206, 216);
            btn.Margin = new Padding(3, 0, 3, 0);
            btn.Click += (_, _) =>
            {
                _selectedCategory = category;
                BuildCategoryButtons();
                RenderProductCards();
            };
            _categoryPanel.Controls.Add(btn);
        }
    }

    private sealed class AvailableProductRow
    {
        public int ProductId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal AvailableStock { get; set; }
        public string ProductType { get; set; } = string.Empty;
        public string DisplayTitle =>
            string.IsNullOrWhiteSpace(CompanyName) ? ProductName : $"{CompanyName} — {ProductName}";
    }

    private sealed class CartRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
