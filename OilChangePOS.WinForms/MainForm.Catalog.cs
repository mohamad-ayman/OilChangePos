using ClosedXML.Excel;
using OilChangePOS.Business;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm : Form
{
    /// <summary>Catalog action bar: width follows Arabic captions (avoids clipped text like "تحديث الكل").</summary>
    private static void ApplyCatalogToolbarButtonWidths(params Button[] buttons)
    {
        foreach (var b in buttons)
            SizeWarehouseButtonToFitText(b, 120, 360);
    }

    /// <summary>Soft toolbar strip + bottom rule for separation from the grid.</summary>
    private static Panel WrapCatalogToolbar(FlowLayoutPanel flow)
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(10, 8, 10, 10),
            RightToLeft = RightToLeft.No,
            MinimumSize = new Size(0, 84)
        };
        host.Paint += BorderBottom;
        flow.Dock = DockStyle.Fill;
        flow.BackColor = Color.Transparent;
        flow.Padding = new Padding(4, 4, 4, 4);
        host.Controls.Add(flow);
        return host;
    }

    private static void ApplyCatalogListChrome(DataGridView grid)
    {
        grid.ColumnHeadersHeight = 54;
        grid.RowTemplate.Height = 40;
        grid.GridColor = Color.FromArgb(236, 240, 245);
    }

    private TabPage BuildCatalogTab()
    {
        var tab = new TabPage("الشركات والأصناف")
        {
            BackColor = Color.FromArgb(242, 245, 249),
            RightToLeft = RightToLeft.Yes
        };

        _catalogProductTypeCombo.Items.Clear();
        _catalogProductTypeCombo.Items.AddRange(["Oil", "Filter", "Grease", "Other"]);
        _catalogProductTypeCombo.SelectedIndex = 0;
        _catalogProductPackCombo.Items.Clear();
        _catalogProductPackCombo.Items.AddRange(["1L", "4L", "5L", "16L", "20L", "Unit"]);
        _catalogProductPackCombo.SelectedIndex = 0;

        foreach (var c in new Control[] { _catalogCompanyNameEdit, _catalogProductNameEdit, _catalogProductTypeCombo, _catalogProductPackCombo })
        {
            c.Font = UiFont;
            c.Margin = new Padding(0, 2, 0, 6);
            c.RightToLeft = RightToLeft.Yes;
        }

        _catalogCompanyActiveEdit.RightToLeft = RightToLeft.Yes;
        _catalogProductActiveEdit.RightToLeft = RightToLeft.Yes;

        _catalogCompanyNameEdit.MinimumSize = new Size(220, 36);
        _catalogCompanyNameEdit.BorderStyle = BorderStyle.FixedSingle;
        _catalogProductNameEdit.MinimumSize = new Size(200, 36);
        _catalogProductNameEdit.BorderStyle = BorderStyle.FixedSingle;
        _catalogProductTypeCombo.MinimumSize = new Size(120, 34);
        _catalogProductPackCombo.MinimumSize = new Size(100, 34);
        _catalogCompanyActiveEdit.Font = UiFontCaption;
        _catalogProductActiveEdit.Font = UiFontCaption;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor = Color.FromArgb(230, 235, 242),
            SplitterWidth = 6,
            Panel1MinSize = 25,
            Panel2MinSize = 25,
            RightToLeft = RightToLeft.No
        };
        split.Panel1.BackColor = split.Panel2.BackColor = split.BackColor;
        ApplyInitialSplitterDistance(split, 480, 260, 260);

        // Panel1 = physical left: product detail. Panel2 = physical right: company master (reading order for RTL).
        var productsCard = BuildCard();
        productsCard.Dock = DockStyle.Fill;
        productsCard.Padding = new Padding(20, 18, 20, 20);
        productsCard.Margin = new Padding(0, 0, 6, 0);

        var companiesCard = BuildCard();
        companiesCard.Dock = DockStyle.Fill;
        companiesCard.Padding = new Padding(20, 18, 20, 20);
        companiesCard.Margin = new Padding(6, 0, 0, 0);

        var companiesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };
        companiesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        companiesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        companiesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 212f));
        companiesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        companiesLayout.Controls.Add(BuildCatalogSectionHeader("الشركات (المورّد)"), 0, 0);

        var companyForm = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(12, 8, 12, 10),
            BackColor = Color.FromArgb(249, 251, 253),
            // Column 0 is on the reading edge (right): caption, then control to its left.
            RightToLeft = RightToLeft.Yes
        };
        companyForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        companyForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        companyForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        companyForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        companyForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 92f));
        companyForm.Controls.Add(new Label
        {
            Text = "اسم الشركة",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextSecondary,
            Padding = new Padding(0, 10, 12, 0),
            RightToLeft = RightToLeft.No
        }, 0, 0);
        companyForm.Controls.Add(_catalogCompanyNameEdit, 1, 0);
        _catalogCompanyActiveEdit.Padding = new Padding(0, 8, 0, 0);
        companyForm.SetColumnSpan(_catalogCompanyActiveEdit, 2);
        companyForm.Controls.Add(_catalogCompanyActiveEdit, 0, 1);
        var companyBtns = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            RightToLeft = RightToLeft.No,
            WrapContents = true,
            AutoSize = false,
            BackColor = Color.Transparent
        };
        var addCo = BuildButton("إضافة شركة", Color.FromArgb(39, 174, 96));
        addCo.Margin = new Padding(0, 0, 8, 8);
        addCo.RightToLeft = RightToLeft.Yes;
        addCo.Click += async (_, _) => await SaveCatalogCompanyAsync(createNew: true);
        var saveCo = BuildButton("حفظ", Color.FromArgb(243, 156, 18));
        saveCo.Margin = new Padding(0, 0, 8, 8);
        saveCo.RightToLeft = RightToLeft.Yes;
        saveCo.Click += async (_, _) => await SaveCatalogCompanyAsync(createNew: false);
        var newCo = BuildButton("جديد", Color.FromArgb(52, 73, 94));
        newCo.Margin = new Padding(0, 0, 8, 8);
        newCo.RightToLeft = RightToLeft.Yes;
        newCo.Click += (_, _) => ClearCatalogCompanyForm();
        var refAll = BuildButton("تحديث الكل", Color.FromArgb(52, 152, 219));
        refAll.Margin = new Padding(0, 0, 8, 8);
        refAll.RightToLeft = RightToLeft.Yes;
        refAll.Click += async (_, _) => await RefreshCatalogGridsAsync();
        // RightToLeft flow: first in collection is on the physical right (primary action).
        companyBtns.Controls.Add(addCo);
        companyBtns.Controls.Add(saveCo);
        companyBtns.Controls.Add(newCo);
        companyBtns.Controls.Add(refAll);
        ApplyCatalogToolbarButtonWidths(addCo, saveCo, newCo, refAll);
        var companyToolbarHost = WrapCatalogToolbar(companyBtns);
        companyForm.SetColumnSpan(companyToolbarHost, 2);
        companyForm.Controls.Add(companyToolbarHost, 0, 2);

        StyleReportGrid(_catalogCompaniesGrid);
        ConfigureCatalogCompaniesColumns();
        ApplyReportGridColumnBiDiAlignment(_catalogCompaniesGrid);
        ApplyCatalogListChrome(_catalogCompaniesGrid);
        _catalogCompaniesGrid.AllowUserToAddRows = false;
        _catalogCompaniesGrid.Margin = new Padding(0, 14, 0, 0);
        _catalogCompaniesGrid.SelectionChanged += (_, _) =>
        {
            LoadSelectedCatalogCompanyRow();
            _ = RefreshCatalogProductsGridAsync();
        };

        companiesLayout.Controls.Add(companyForm, 0, 1);
        companiesLayout.Controls.Add(_catalogCompaniesGrid, 0, 2);
        companiesCard.Controls.Add(companiesLayout);

        var productsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes
        };
        productsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        productsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        productsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 256f));
        productsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        productsLayout.Controls.Add(BuildCatalogSectionHeader("أصناف الشركة المحددة"), 0, 0);

        var productForm = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4,
            Padding = new Padding(12, 8, 12, 10),
            BackColor = Color.FromArgb(249, 251, 253),
            RightToLeft = RightToLeft.Yes
        };
        productForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        productForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
        productForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        productForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
        productForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        productForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));
        productForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
        productForm.RowStyles.Add(new RowStyle(SizeType.Absolute, 92f));
        productForm.Controls.Add(new Label
        {
            Text = "اسم الصنف",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextSecondary,
            Padding = new Padding(0, 10, 12, 0),
            RightToLeft = RightToLeft.No
        }, 0, 0);
        productForm.SetColumnSpan(_catalogProductNameEdit, 3);
        productForm.Controls.Add(_catalogProductNameEdit, 1, 0);
        productForm.Controls.Add(new Label
        {
            Text = "النوع",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextSecondary,
            Padding = new Padding(0, 10, 10, 0),
            RightToLeft = RightToLeft.No
        }, 0, 1);
        productForm.Controls.Add(_catalogProductTypeCombo, 1, 1);
        productForm.Controls.Add(new Label
        {
            Text = "العبوة",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = UiFontCaption,
            ForeColor = UiTextSecondary,
            Padding = new Padding(0, 10, 10, 0),
            RightToLeft = RightToLeft.No
        }, 2, 1);
        productForm.Controls.Add(_catalogProductPackCombo, 3, 1);
        _catalogProductActiveEdit.Margin = new Padding(0, 8, 0, 0);
        productForm.SetColumnSpan(_catalogProductActiveEdit, 4);
        productForm.Controls.Add(_catalogProductActiveEdit, 0, 2);
        var productBtns = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            RightToLeft = RightToLeft.No,
            WrapContents = true,
            AutoSize = false,
            BackColor = Color.Transparent
        };
        var addPr = BuildButton("إضافة صنف", Color.FromArgb(39, 174, 96));
        addPr.Margin = new Padding(0, 0, 8, 8);
        addPr.RightToLeft = RightToLeft.Yes;
        addPr.Click += async (_, _) => await SaveCatalogProductAsync(createNew: true);
        var savePr = BuildButton("حفظ الصنف", Color.FromArgb(243, 156, 18));
        savePr.Margin = new Padding(0, 0, 8, 8);
        savePr.RightToLeft = RightToLeft.Yes;
        savePr.Click += async (_, _) => await SaveCatalogProductAsync(createNew: false);
        var newPr = BuildButton("صنف جديد", Color.FromArgb(52, 73, 94));
        newPr.Margin = new Padding(0, 0, 8, 8);
        newPr.RightToLeft = RightToLeft.Yes;
        newPr.Click += (_, _) => ClearCatalogProductForm();
        productBtns.Controls.Add(addPr);
        productBtns.Controls.Add(savePr);
        productBtns.Controls.Add(newPr);
        ApplyCatalogToolbarButtonWidths(addPr, savePr, newPr);
        var productToolbarHost = WrapCatalogToolbar(productBtns);
        productForm.SetColumnSpan(productToolbarHost, 4);
        productForm.Controls.Add(productToolbarHost, 0, 3);

        StyleReportGrid(_catalogProductsGrid);
        ConfigureCatalogProductsColumns();
        ApplyReportGridColumnBiDiAlignment(_catalogProductsGrid);
        ApplyCatalogListChrome(_catalogProductsGrid);
        _catalogProductsGrid.AllowUserToAddRows = false;
        _catalogProductsGrid.Margin = new Padding(0, 14, 0, 0);
        _catalogProductsGrid.SelectionChanged += (_, _) => LoadSelectedCatalogProductRow();

        productsLayout.Controls.Add(productForm, 0, 1);
        productsLayout.Controls.Add(_catalogProductsGrid, 0, 2);
        productsCard.Controls.Add(productsLayout);

        split.Panel1.Controls.Add(productsCard);
        split.Panel2.Controls.Add(companiesCard);

        var wrap = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 12, 16, 14),
            BackColor = Color.FromArgb(242, 245, 249),
            RightToLeft = RightToLeft.No
        };
        wrap.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        wrap.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        wrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var catalogPageHeader = BuildStandardModuleHeaderCard(
            "الشركات والأصناف",
            "أضف الشركة أولاً، ثم اخترها في الجدول وأضف أصنافها (زيت / شحوم / …). المخزون يُستلم من تبويب المستودع الرئيسي. تفعيل الشركة/الصنف يؤثر على ظهوره في القوائم.",
            subtitleItalic: false,
            DockStyle.Top,
            autoSizeHeight: true,
            out _,
            out _);
        catalogPageHeader.Margin = new Padding(0, 0, 0, 14);
        catalogPageHeader.Dock = DockStyle.Fill;

        wrap.Controls.Add(catalogPageHeader, 0, 0);
        wrap.Controls.Add(split, 0, 1);
        tab.Controls.Add(wrap);
        return tab;
    }

    /// <summary>Accent strip + title for each catalog card (RTL-friendly).</summary>
    private static Panel BuildCatalogSectionHeader(string title)
    {
        var host = new Panel { Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 12), BackColor = Color.White, RightToLeft = RightToLeft.No };
        var accent = new Panel { Dock = DockStyle.Top, Height = 4, BackColor = Color.FromArgb(41, 128, 185) };
        var titleLbl = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 42,
            Font = UiFontSection,
            ForeColor = Color.FromArgb(44, 62, 80),
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.No,
            Padding = new Padding(0, 12, 6, 6),
            BackColor = Color.White,
            UseCompatibleTextRendering = false
        };
        host.Controls.Add(accent);
        host.Controls.Add(titleLbl);
        return host;
    }

    private void ConfigureCatalogCompaniesColumns()
    {
        _catalogCompaniesGrid.AutoGenerateColumns = false;
        _catalogCompaniesGrid.Columns.Clear();
        _catalogCompaniesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _catalogCompaniesGrid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(CatalogCompanyRow.Name), HeaderText = "الشركة", FillWeight = 50, ReadOnly = true });
        _catalogCompaniesGrid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(CatalogCompanyRow.IsActive), HeaderText = "نشط", FillWeight = 14, ReadOnly = true });
        var productCountCol = new DataGridViewTextBoxColumn { DataPropertyName = nameof(CatalogCompanyRow.ProductCount), HeaderText = "عدد الأصناف", FillWeight = 18, ReadOnly = true };
        productCountCol.DefaultCellStyle.Format = "N0";
        _catalogCompaniesGrid.Columns.Add(productCountCol);
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

        var companyDtos = await _catalogAdminService.ListCompaniesForCatalogAsync();
        var companies = companyDtos.Select(c => new CatalogCompanyRow
        {
            Id = c.Id,
            Name = c.Name,
            IsActive = c.IsActive,
            ProductCount = c.ProductCount
        }).ToList();

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

        var cid = _selectedCatalogCompanyId.Value;
        var productDtos = await _catalogAdminService.ListProductsForCompanyAsync(cid);
        var products = productDtos.Select(p => new CatalogProductRow
        {
            Id = p.Id,
            CompanyId = p.CompanyId,
            Name = p.Name,
            ProductCategory = p.ProductCategory,
            PackageSize = p.PackageSize,
            IsActive = p.IsActive
        }).ToList();

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

        try
        {
            await _catalogAdminService.SaveCatalogCompanyAsync(createNew, _selectedCatalogCompanyId, name, _catalogCompanyActiveEdit.Checked);
            MessageBox.Show(createNew ? "تمت إضافة الشركة." : "تم حفظ الشركة.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            if (createNew)
            {
                var refreshed = await _catalogAdminService.ListCompaniesForCatalogAsync();
                var match = refreshed.FirstOrDefault(c => c.Name == name);
                if (match is not null)
                    _selectedCatalogCompanyId = match.Id;
            }

            await RefreshCatalogGridsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MsgRtl);
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

        try
        {
            await _catalogAdminService.SaveCatalogProductAsync(createNew, companyId, _selectedCatalogProductId, pname, category, package, _catalogProductActiveEdit.Checked);
            MessageBox.Show(createNew ? "تمت إضافة الصنف." : "تم حفظ الصنف.", "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            if (createNew)
            {
                var plist = await _catalogAdminService.ListProductsForCompanyAsync(companyId);
                var match = plist.FirstOrDefault(p =>
                    p.Name == pname && p.ProductCategory == category && p.PackageSize == package);
                if (match is not null)
                    _selectedCatalogProductId = match.Id;
            }

            await RefreshCatalogGridsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "الكتالوج", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }

    private async Task RefreshCompanyComboBoxesAsync()
    {
        var comboDtos = await _catalogAdminService.ListActiveCompaniesForComboAsync();
        var companies = comboDtos.Select(c => new CompanyListItem { Id = c.Id, Name = c.Name }).ToList();

        _newProductCompanyCombo.DataSource = null;
        _newProductCompanyCombo.DisplayMember = nameof(CompanyListItem.Name);
        _newProductCompanyCombo.ValueMember = nameof(CompanyListItem.Id);
        _newProductCompanyCombo.DataSource = companies;
        if (companies.Count > 0)
            _newProductCompanyCombo.SelectedIndex = 0;
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

