using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using OilChangePOS.Business;
using OilChangePOS.Data;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm : Form
{

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
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.No
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
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.No
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

        var wrap = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 16, 18, 14), BackColor = Color.FromArgb(245, 247, 250), RightToLeft = RightToLeft.No };
        var catalogPageHeader = BuildStandardModuleHeaderCard(
            "الشركات والأصناف",
            "أضف الشركة أولاً، ثم اخترها في الجدول وأضف أصنافها (زيت / شحوم / …). المخزون يُستلم من تبويب المستودع الرئيسي. تفعيل الشركة/الصنف يؤثر على ظهوره في القوائم.",
            subtitleItalic: false,
            DockStyle.Top,
            autoSizeHeight: true,
            out _,
            out _);
        catalogPageHeader.Margin = new Padding(0, 0, 0, 12);
        wrap.Controls.Add(split);
        wrap.Controls.Add(catalogPageHeader);
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

