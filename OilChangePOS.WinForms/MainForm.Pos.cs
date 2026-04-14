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
            _kpiBranchSalesVal.Text = $"{report.TotalSales:n0} ر.س";
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
    //   └─ host (Dock=Fill, RightToLeft=No, Padding=10)
    //      ├─ kpiPanel   (Dock=Top, Height=76)
    //      ├─ cartPanel  (Dock=Right, Width=355)   ← RIGHT column
    //      └─ prodPanel  (Dock=Fill)               ← LEFT column (fills remaining)
    //
    // APPLY IN CLAUDE CODE:
    //   "Replace BuildPosTab() and RenderProductCards() in MainForm.cs with the
    //    code in /mnt/user-data/outputs/integrated/BuildPosTab_Final.cs
    //    Then run dotnet build and fix any errors."
    // ════════════════════════════════════════════════════════════════════════════

    private TabPage BuildPosTab()
    {
        var tab = new TabPage("البيع") { RightToLeft = RightToLeft.Yes };

        // ── Host: neutral container, no RTL interference ───────────────────────
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(243, 245, 248),
            Padding = new Padding(8, 8, 8, 8),
            RightToLeft = RightToLeft.No // ← critical: prevents Dock mirroring
        };

        // ══════════════════════════════════════════════════════════════════════
        // 1. KPI STRIP  (Dock=Top)
        // ══════════════════════════════════════════════════════════════════════
        var kpiPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.FromArgb(243, 245, 248),
            Margin = new Padding(0, 0, 0, 6)
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

        // ══════════════════════════════════════════════════════════════════════
        // 2. CART PANEL  (Dock=Right, fixed width)
        // ══════════════════════════════════════════════════════════════════════
        var cartOuter = new Panel
        {
            Dock = DockStyle.Right,
            Width = 332,
            BackColor = Color.FromArgb(243, 245, 248)
        };

        var cartCard = BuildCard();
        cartCard.Dock = DockStyle.Fill;

        // Cart header
        var cartHead = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.White
        };
        cartHead.Paint += BorderBottom;
        cartHead.Controls.Add(new Label
        {
            Text = "سلة الطلب",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 30, 30),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 14, 0),
            RightToLeft = RightToLeft.Yes
        });

        // Remove button
        var removeBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.FromArgb(254, 245, 245)
        };
        removeBar.Paint += BorderBottom;
        var removeBtn = new Button
        {
            Text = "× إزالة الصنف المحدد",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(254, 235, 235),
            ForeColor = Color.FromArgb(180, 28, 28),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false
        };
        removeBtn.FlatAppearance.BorderColor = Color.FromArgb(248, 187, 187);
        removeBtn.FlatAppearance.BorderSize = 1;
        removeBtn.Click += (_, _) => RemoveSelectedCartItem();
        removeBar.Controls.Add(removeBtn);

        // Cart grid
        StyleGrid(_cartGrid);
        _cartGrid.Dock = DockStyle.Fill;
        _cartGrid.DataSource = _cartBinding;
        _cartGrid.BackgroundColor = Color.White;
        _cartGrid.RowTemplate.Height = 36;
        _cartGrid.Font = new Font("Segoe UI", 10f);
        _cartGrid.DefaultCellStyle.Padding = new Padding(6, 3, 6, 3);
        _cartGrid.BorderStyle = BorderStyle.None;
        _cartGrid.GridColor = Color.FromArgb(230, 232, 236);
        _cartGrid.RightToLeft = RightToLeft.Yes;
        _cartGrid.AutoGenerateColumns = false;
        _cartGrid.Columns.Clear();
        _cartGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CartRow.ProductName),
            HeaderText = "الصنف", ReadOnly = true,
            FillWeight = 40, MinimumWidth = 80
        });
        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CartRow.Quantity),
            HeaderText = "الكمية", FillWeight = 20, MinimumWidth = 58,
            DefaultCellStyle = new DataGridViewCellStyle
                { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CartRow.UnitPrice),
            HeaderText = "السعر", ReadOnly = true,
            FillWeight = 20, MinimumWidth = 64,
            DefaultCellStyle = new DataGridViewCellStyle
                { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
        });
        _cartGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(CartRow.LineTotal),
            HeaderText = "الإجمالي", ReadOnly = true,
            FillWeight = 22, MinimumWidth = 72,
            DefaultCellStyle = new DataGridViewCellStyle
                { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
        });

        // Bottom area — Dock=Top stacking: first added sits at top (customer → pay → oil at bottom).
        var cartBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 222,
            BackColor = Color.White,
            Padding = new Padding(10, 8, 10, 8)
        };
        cartBottom.Paint += (s, e) =>
            e.Graphics.DrawLine(new Pen(Color.FromArgb(218, 220, 228)), 0, 0, cartBottom.Width, 0);

        // Customer row
        var custTbl = BuildTwoColRow("العميل:");
        _posCustomerCombo.Dock = DockStyle.Fill;
        _posCustomerCombo.Font = UiFont;
        _posCustomerCombo.Height = 28;
        custTbl.Controls.Add(_posCustomerCombo, 1, 0);

        // Discount row
        var discTbl = BuildTwoColRow("خصم:");
        _posDiscount.Dock = DockStyle.Fill;
        _posDiscount.Font = UiFont;
        _posDiscount.Height = 28;
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

        // Summary rows inside sumBox using absolute layout
        void AddSumRow(string lbl, Label val, int top, bool big = false)
        {
            var l = new Label
            {
                Text = lbl,
                Font = big ? new Font("Segoe UI", 10f, FontStyle.Bold) : UiFont,
                ForeColor = Color.FromArgb(100, 100, 110),
                TextAlign = ContentAlignment.MiddleRight,
                RightToLeft = RightToLeft.Yes,
                Bounds = new Rectangle(sumBox.Width - 140, top, 128, 22)
            };
            l.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            val.Font = big ? new Font("Segoe UI", 14f, FontStyle.Bold) : UiFont;
            val.ForeColor = big ? Color.FromArgb(27, 94, 32) : Color.FromArgb(30, 30, 30);
            val.TextAlign = ContentAlignment.MiddleLeft;
            val.Bounds = new Rectangle(8, top, 100, 22);
            val.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            sumBox.Controls.Add(l);
            sumBox.Controls.Add(val);
        }

        _subtotalValueLabel.ForeColor = Color.FromArgb(30, 30, 30);
        _discountValueLabel.ForeColor = Color.FromArgb(192, 57, 43);
        _totalValueLabel.ForeColor = Color.FromArgb(27, 94, 32);

        sumBox.HandleCreated += (_, _) =>
        {
            sumBox.Controls.Clear();
            AddSumRow("المجموع الفرعي:", _subtotalValueLabel, 6);
            AddSumRow("الخصم:", _discountValueLabel, 26);
            AddSumRow("الإجمالي:", _totalValueLabel, 46, big: true);
        };

        // Pay button
        var payBtn = new Button
        {
            Text = "دفع وإصدار الفاتورة",
            Dock = DockStyle.Top,
            Height = 40,
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false,
            Margin = new Padding(0, 0, 0, 4)
        };
        payBtn.FlatAppearance.BorderSize = 0;
        payBtn.Click += async (_, _) => await CompleteSaleAsync();

        // Oil change row
        var oilRow = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.White,
            Margin = new Padding(0, 2, 0, 0)
        };

        var oilBtn = new Button
        {
            Text = "خدمة تغيير الزيت",
            Width = 130,
            Height = 28,
            BackColor = Color.FromArgb(33, 150, 243),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Left = 2,
            Top = 2,
            UseCompatibleTextRendering = false
        };
        oilBtn.FlatAppearance.BorderSize = 0;
        oilBtn.Click += async (_, _) => await CompleteOilChangeServiceAsync();

        _oilChangeCustomerId.Width = 52; _oilChangeCustomerId.Height = 28; _oilChangeCustomerId.Top = 2; _oilChangeCustomerId.Left = 136; _oilChangeCustomerId.Minimum = 1; _oilChangeCustomerId.Maximum = 999999;
        _oilChangeCarId.Width = 52; _oilChangeCarId.Height = 28; _oilChangeCarId.Top = 2; _oilChangeCarId.Left = 194; _oilChangeCarId.Minimum = 1; _oilChangeCarId.Maximum = 999999;
        _oilChangeOdometer.Width = 64; _oilChangeOdometer.Height = 28; _oilChangeOdometer.Top = 2; _oilChangeOdometer.Left = 252; _oilChangeOdometer.Minimum = 0; _oilChangeOdometer.Maximum = 999999;

        var oilCustLbl = new Label { Text = "زبون", Left = 136, Top = 6, Width = 0, AutoSize = true, Font = new Font("Segoe UI", 8f), ForeColor = UiTextSecondary };
        var oilCarLbl = new Label { Text = "سيارة", Left = 194, Top = 6, Width = 0, AutoSize = true, Font = new Font("Segoe UI", 8f), ForeColor = UiTextSecondary };
        var oilOdoLbl = new Label { Text = "عداد", Left = 252, Top = 6, Width = 0, AutoSize = true, Font = new Font("Segoe UI", 8f), ForeColor = UiTextSecondary };

        // Reposition labels above spinners
        void LayoutOil(object? _, EventArgs __)
        {
            oilCustLbl.Left = _oilChangeCustomerId.Left;
            oilCarLbl.Left = _oilChangeCarId.Left;
            oilOdoLbl.Left = _oilChangeOdometer.Left;
        }

        oilRow.HandleCreated += LayoutOil;

        oilRow.Controls.AddRange(new Control[]
        {
            oilBtn,
            _oilChangeCustomerId, _oilChangeCarId, _oilChangeOdometer
        });

        cartBottom.Controls.Add(custTbl);
        cartBottom.Controls.Add(discTbl);
        cartBottom.Controls.Add(sumBox);
        cartBottom.Controls.Add(payBtn);
        cartBottom.Controls.Add(oilRow);

        cartCard.Controls.Add(cartBottom);
        cartCard.Controls.Add(_cartGrid);
        cartCard.Controls.Add(removeBar);
        cartCard.Controls.Add(cartHead);
        cartOuter.Controls.Add(cartCard);

        // ══════════════════════════════════════════════════════════════════════
        // 3. PRODUCT PANEL  (Dock=Fill — fills whatever space remains after cart)
        // ══════════════════════════════════════════════════════════════════════
        var prodOuter = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(243, 245, 248),
            Padding = new Padding(0, 0, 8, 0)
        };

        var prodCard = BuildCard();
        prodCard.Dock = DockStyle.Fill;

        // Single filter strip: branch | quantity | categories | search (flex) — one visual row like the KPI strip.
        var filterStrip = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(8, 6, 8, 6),
            Margin = Padding.Empty
        };
        filterStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 198f));
        filterStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        filterStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232f));
        filterStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        filterStrip.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        filterStrip.Paint += BorderBottom;

        var branchCell = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = false,
            Padding = new Padding(0, 2, 0, 0),
            Margin = Padding.Empty,
            BackColor = Color.White
        };
        branchCell.Controls.Add(new Label
        {
            Text = "الفرع:",
            AutoSize = true,
            Font = UiFontCaption,
            ForeColor = UiTextSecondary,
            Padding = new Padding(0, 4, 6, 0),
            Margin = new Padding(0, 0, 0, 0)
        });
        _posWarehouseCombo.Width = 148;
        _posWarehouseCombo.Height = 28;
        _posWarehouseCombo.Font = UiFont;
        _posWarehouseCombo.Margin = new Padding(0, 0, 0, 0);
        branchCell.Controls.Add(_posWarehouseCombo);

        var qtyCell = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 0),
            BackColor = Color.White
        };
        qtyCell.Controls.Add(new Label
        {
            Text = "الكمية:",
            AutoSize = true,
            Font = UiFontCaption,
            ForeColor = UiTextSecondary,
            Padding = new Padding(0, 4, 6, 0)
        });
        _posAddQty.Width = 74;
        _posAddQty.Height = 28;
        _posAddQty.Margin = new Padding(0, 0, 0, 0);
        _posAddQty.Font = UiFont;
        _posAddQty.DecimalPlaces = 3;
        _posAddQty.Minimum = 0.001m;
        qtyCell.Controls.Add(_posAddQty);

        _categoryPanel.Dock = DockStyle.Fill;
        _categoryPanel.Margin = new Padding(0);
        _categoryPanel.Padding = new Padding(2, 2, 2, 0);
        _categoryPanel.BackColor = Color.White;
        _categoryPanel.FlowDirection = FlowDirection.RightToLeft;
        _categoryPanel.WrapContents = false;
        _categoryPanel.AutoScroll = true;
        _categoryPanel.Height = 30;

        var searchHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 0, 0, 0), BackColor = Color.White };
        _posSearchBox.Dock = DockStyle.Fill;
        _posSearchBox.MinimumSize = new Size(120, 26);
        _posSearchBox.Font = UiFont;
        _posSearchBox.PlaceholderText = "بحث عن صنف...";
        _posSearchBox.TextChanged += (_, _) => RenderProductCards();
        searchHost.Controls.Add(_posSearchBox);

        filterStrip.Controls.Add(branchCell, 0, 0);
        filterStrip.Controls.Add(qtyCell, 1, 0);
        filterStrip.Controls.Add(_categoryPanel, 2, 0);
        filterStrip.Controls.Add(searchHost, 3, 0);

        // Product cards scroll
        _productCardsPanel.Dock = DockStyle.Fill;
        _productCardsPanel.AutoScroll = true;
        _productCardsPanel.WrapContents = true;
        _productCardsPanel.Padding = new Padding(8, 8, 8, 8);
        _productCardsPanel.BackColor = Color.FromArgb(247, 248, 250);

        prodCard.Controls.Add(filterStrip);
        prodCard.Controls.Add(_productCardsPanel);
        prodOuter.Controls.Add(prodCard);

        // ── Assemble host ──────────────────────────────────────────────────────
        // ORDER MATTERS for Dock: add Right BEFORE Fill
        host.Controls.Add(prodOuter); // Fill — must be last
        host.Controls.Add(cartOuter); // Right — must be before Fill
        host.Controls.Add(kpiPanel); // Top

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

    private Panel BuildSummaryPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 140,
            BackColor = Color.FromArgb(236, 240, 241),
            Padding = new Padding(12)
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            RightToLeft = RightToLeft.Yes
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));

        _subtotalValueLabel.TextAlign = ContentAlignment.MiddleRight;
        _discountValueLabel.TextAlign = ContentAlignment.MiddleRight;
        _totalValueLabel.TextAlign = ContentAlignment.MiddleRight;
        table.Controls.Add(new Label { Text = "المجموع الفرعي", AutoSize = true, Font = UiFontCaption, ForeColor = UiTextPrimary, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes }, 0, 0);
        table.Controls.Add(_subtotalValueLabel, 1, 0);
        table.Controls.Add(new Label { Text = "الخصم", AutoSize = true, Font = UiFontCaption, ForeColor = UiTextPrimary, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes }, 0, 1);
        table.Controls.Add(_discountValueLabel, 1, 1);
        table.Controls.Add(new Label { Text = "الإجمالي", AutoSize = true, Font = UiFontSection, ForeColor = UiTextPrimary, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes }, 0, 2);
        table.Controls.Add(_totalValueLabel, 1, 2);

        panel.Controls.Add(table);
        return panel;
    }

    private Panel BuildSubmitPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 170,
            BackColor = Color.White,
            Padding = new Padding(12, 8, 12, 10)
        };

        var font = UiFontCaption;
        var posCustomerRow = new Panel { Dock = DockStyle.Top, Height = 44 };
        posCustomerRow.Controls.Add(new Label
        {
            Text = "عميل الفاتورة",
            Left = 0,
            Top = 12,
            Width = 124,
            Height = 22,
            Font = font,
            ForeColor = UiTextPrimary,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        });
        _posCustomerCombo.Left = 128;
        _posCustomerCombo.Top = 8;
        _posCustomerCombo.Width = 292;
        _posCustomerCombo.Height = 28;
        posCustomerRow.Controls.Add(_posCustomerCombo);

        var oilRow = new Panel { Dock = DockStyle.Top, Height = 46 };
        var customerLbl = new Label { Text = "رقم الزبون", Left = 0, Top = 12, Width = 78, Height = 22, Font = font, ForeColor = UiTextPrimary, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes };
        _oilChangeCustomerId.Left = 80;
        _oilChangeCustomerId.Top = 8;
        _oilChangeCustomerId.Height = 26;
        var carLbl = new Label { Text = "رقم السيارة", Left = 162, Top = 12, Width = 88, Height = 22, Font = font, ForeColor = UiTextPrimary, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes };
        _oilChangeCarId.Left = 252;
        _oilChangeCarId.Top = 8;
        _oilChangeCarId.Height = 26;
        var odoLbl = new Label { Text = "عداد المسافة", Left = 334, Top = 12, Width = 96, Height = 22, Font = font, ForeColor = UiTextPrimary, TextAlign = ContentAlignment.MiddleRight, RightToLeft = RightToLeft.Yes };
        _oilChangeOdometer.Left = 432;
        _oilChangeOdometer.Top = 8;
        _oilChangeOdometer.Height = 26;
        var oilBtn = BuildButton("خدمة تغيير الزيت", Color.FromArgb(41, 128, 185));
        oilBtn.Width = 180;
        oilBtn.Height = 34;
        oilBtn.Left = 530;
        oilBtn.Top = 6;
        oilBtn.Click += async (_, _) => await CompleteOilChangeServiceAsync();
        oilRow.Controls.Add(customerLbl);
        oilRow.Controls.Add(_oilChangeCustomerId);
        oilRow.Controls.Add(carLbl);
        oilRow.Controls.Add(_oilChangeCarId);
        oilRow.Controls.Add(odoLbl);
        oilRow.Controls.Add(_oilChangeOdometer);
        oilRow.Controls.Add(oilBtn);

        var payRow = new Panel { Dock = DockStyle.Top, Height = 52 };
        var submit = BuildButton("دفع", Color.FromArgb(39, 174, 96));
        submit.Width = 230;
        submit.Height = 44;
        submit.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        submit.Left = payRow.Width - submit.Width - 4;
        submit.Top = 4;
        submit.Click += async (_, _) => await CompleteSaleAsync();
        payRow.Controls.Add(submit);
        payRow.Resize += (_, _) => submit.Left = payRow.Width - submit.Width - 4;

        panel.Controls.Add(payRow);
        panel.Controls.Add(oilRow);
        panel.Controls.Add(posCustomerRow);
        return panel;
    }

    private async Task CompleteOilChangeServiceAsync()
    {
        var (whOk, warehouseId) = await TryResolvePosSaleWarehouseIdAsync();
        if (!whOk)
        {
            MessageBox.Show(
                "اختر المستودع قبل تسجيل خدمة تغيير الزيت (نفس قائمة نقطة البيع).",
                "مستودع مطلوب",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1,
                MsgRtl);
            return;
        }

        var cart = _cartBinding.List.Cast<CartRow>().ToList();
        if (!cart.Any())
        {
            MessageBox.Show("أضف أصنافاً للسلة أولاً.", "سلة فارغة", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        var customerId = (int)_oilChangeCustomerId.Value;
        var carId = (int)_oilChangeCarId.Value;
        var odometerKm = (int)_oilChangeOdometer.Value;

        var details = cart.Select(x => new SaleItemRequest(x.ProductId, x.Quantity)).ToList();

        try
        {
            var serviceId = await _serviceOrderService.CreateOilChangeServiceAsync(
                new OilChangeRequest(customerId, carId, odometerKm, _currentUser.Id, warehouseId, details));
            MessageBox.Show(
                $"تم حفظ خدمة تغيير الزيت. رقم الخدمة: {serviceId}",
                "تم",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                MsgRtl);
            _cartBinding.DataSource = new List<CartRow>();
            RefreshCartSummary();
            await RefreshAllStockViewsAsync();
            await RefreshDailyKpisAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "فشل خدمة تغيير الزيت", MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MsgRtl);
        }
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
            var borderClr = isLow ? Color.FromArgb(251, 140, 0) : Color.FromArgb(214, 218, 226);

            var card = new Panel
            {
                Width = 190,
                Height = 166,
                Margin = new Padding(5),
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
                Bounds = new Rectangle(1, 1, 188, 72),
                BackColor = Color.FromArgb(238, 242, 248),
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
                Bounds = new Rectangle(8, 76, 174, 32),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 25, 35),
                RightToLeft = RightToLeft.Yes,
                TextAlign = ContentAlignment.TopRight
            };

            // Price
            var priceLbl = new Label
            {
                Text = $"{p.UnitPrice:n2} ر.س",
                Bounds = new Rectangle(8, 108, 174, 20),
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(13, 71, 161),
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
                Bounds = new Rectangle(8, 130, 134, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = stockClr,
                RightToLeft = RightToLeft.Yes,
                TextAlign = ContentAlignment.MiddleRight
            };

            // Add button
            var addBtn = new Button
            {
                Text = "+",
                Bounds = new Rectangle(152, 128, 30, 24),
                FlatStyle = FlatStyle.Flat,
                BackColor = outStock ? Color.FromArgb(240, 240, 240) : Color.FromArgb(225, 242, 254),
                ForeColor = outStock ? Color.FromArgb(160, 160, 160) : Color.FromArgb(13, 71, 161),
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                Cursor = outStock ? Cursors.Default : Cursors.Hand,
                Enabled = !outStock,
                UseCompatibleTextRendering = false
            };
            addBtn.FlatAppearance.BorderColor = outStock
                ? Color.FromArgb(210, 210, 210)
                : Color.FromArgb(179, 229, 252);
            addBtn.FlatAppearance.BorderSize = 1;
            if (!outStock) addBtn.Click += (_, _) => AddProductToCart(p);

            card.Controls.AddRange(new Control[] { img, nameLbl, priceLbl, stockLbl, addBtn });
            _productCardsPanel.Controls.Add(card);
        }

        if (list.Count == 0)
        {
            _productCardsPanel.Controls.Add(new Label
            {
                Text = "لا توجد أصناف متاحة.",
                AutoSize = true,
                Font = new Font("Segoe UI", 11f),
                ForeColor = Color.FromArgb(190, 190, 196),
                Margin = new Padding(16)
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
                Height = 26,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = _selectedCategory == category ? Color.FromArgb(52, 152, 219) : Color.FromArgb(236, 240, 241),
                ForeColor = _selectedCategory == category ? Color.White : Color.FromArgb(52, 73, 94)
            };
            btn.FlatAppearance.BorderSize = 0;
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
