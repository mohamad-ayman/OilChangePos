namespace OilChangePOS.WinForms;

public partial class MainForm
{
    private TabPage BuildBranchesAdminTab()
    {
        var tab = new TabPage("الفروع");
        tab.BackColor = Color.FromArgb(245, 247, 250);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(22, 20, 22, 18),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // title
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // subtitle
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // form fields
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // actions bar
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // grid

        var title = new Label
        {
            Text = "إدارة الفروع (مرجع)",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
            Font = UiFontTitle,
            ForeColor = UiTextPrimary,
            RightToLeft = RightToLeft.Yes
        };
        root.Controls.Add(title, 0, 0);

        var subtitle = new Label
        {
            Text = "إضافة وتعديل وتعطيل فروع البيع فقط. المستودع الرئيسي يُدار تلقائياً ولا يُعرض هنا.",
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Margin = new Padding(0, 0, 0, 16),
            ForeColor = UiTextSecondary,
            Font = UiFont,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.Yes
        };
        root.Controls.Add(subtitle, 0, 1);

        var formHost = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        var formFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12, 14, 12, 14),
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        formFlow.MinimumSize = new Size(560, 0);
        _branchName.MinimumSize = new Size(360, 36);
        var nameRow = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, WrapContents = false, Padding = new Padding(0, 0, 0, 6) };
        nameRow.Controls.Add(_branchName);
        nameRow.Controls.Add(new Label { Text = "اسم الفرع", AutoSize = true, Padding = new Padding(12, 10, 0, 0), Font = UiFontCaption, ForeColor = UiTextPrimary, RightToLeft = RightToLeft.Yes });

        formFlow.Controls.Add(nameRow);
        formFlow.Controls.Add(_branchActive);
        var addBranch = BuildButton("إضافة فرع", Color.FromArgb(39, 174, 96));
        addBranch.Click += async (_, _) => await SaveBranchAsync(createNew: true);
        var saveBranch = BuildButton("حفظ التعديل", Color.FromArgb(243, 156, 18));
        saveBranch.Click += async (_, _) => await SaveBranchAsync(createNew: false);
        var newBranch = BuildButton("جديد", Color.FromArgb(52, 73, 94));
        newBranch.Click += (_, _) => ClearBranchForm();
        var refreshBranches = BuildButton("تحديث القائمة", Color.FromArgb(52, 152, 219));
        refreshBranches.Click += async (_, _) =>
        {
            await RefreshBranchesGridAsync();
            await LoadWarehousesAsync();
        };
        formHost.Controls.Add(formFlow);
        root.Controls.Add(formHost, 0, 2);

        var actionRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 10, 0, 4),
            Padding = Padding.Empty,
            BackColor = Color.FromArgb(245, 247, 250),
            RightToLeft = RightToLeft.Yes,
            Height = 52
        };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        actionRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        foreach (var b in new[] { addBranch, saveBranch, newBranch, refreshBranches })
        {
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(3, 0, 3, 0);
        }
        actionRow.Controls.Add(addBranch, 0, 0);
        actionRow.Controls.Add(saveBranch, 1, 0);
        actionRow.Controls.Add(newBranch, 2, 0);
        actionRow.Controls.Add(refreshBranches, 3, 0);
        root.Controls.Add(actionRow, 0, 3);

        StyleGrid(_branchesGrid);
        ConfigureBranchesAdminColumns();
        _branchesGrid.Dock = DockStyle.Fill;
        _branchesGrid.Margin = new Padding(0, 12, 0, 0);
        _branchesGrid.SelectionChanged += (_, _) => LoadSelectedBranchRow();
        root.Controls.Add(_branchesGrid, 0, 4);

        tab.Controls.Add(root);
        return tab;
    }

    private void ConfigureBranchesAdminColumns()
    {
        _branchesGrid.AutoGenerateColumns = false;
        _branchesGrid.Columns.Clear();
        _branchesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _branchesGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(WarehouseDto.Name),
            HeaderText = "اسم الفرع",
            FillWeight = 62,
            ReadOnly = true
        });
        _branchesGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(WarehouseDto.IsActive),
            HeaderText = "نشط",
            FillWeight = 18,
            ReadOnly = true
        });
    }

    private async Task RefreshBranchesGridAsync()
    {
        if (_currentUser.Role != UserRole.Admin)
            return;
        var list = await _warehouseService.ListBranchesForAdminAsync();
        _suppressBranchRowLoad = true;
        try
        {
            _branchesGrid.DataSource = list;
            if (list.Count > 0)
            {
                _branchesGrid.ClearSelection();
                var first = _branchesGrid.Rows[0];
                first.Selected = true;
                _branchesGrid.CurrentCell = first.Cells[0];
            }
        }
        finally
        {
            _suppressBranchRowLoad = false;
        }
        if (list.Count == 0)
            ClearBranchForm();
        else
            LoadSelectedBranchRow();
    }

    private void LoadSelectedBranchRow()
    {
        if (_suppressBranchRowLoad) return;
        if (_branchesGrid.CurrentRow is null || _branchesGrid.CurrentRow.IsNewRow
                                         || _branchesGrid.CurrentRow.DataBoundItem is not WarehouseDto row)
        {
            _selectedBranchId = null;
            return;
        }

        _selectedBranchId = row.Id;
        _branchName.Text = row.Name;
        _branchActive.Checked = row.IsActive;
    }

    private void ClearBranchForm()
    {
        _suppressBranchRowLoad = true;
        try
        {
            _selectedBranchId = null;
            _branchName.Clear();
            _branchActive.Checked = true;
            _branchesGrid.ClearSelection();
        }
        finally
        {
            _suppressBranchRowLoad = false;
        }
    }

    private async Task SaveBranchAsync(bool createNew)
    {
        if (_currentUser.Role != UserRole.Admin)
            return;

        var name = _branchName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("أدخل اسم الفرع.", "الفروع", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
            return;
        }

        try
        {
            if (createNew)
            {
                await _warehouseService.CreateBranchAsync(name, _currentUser.Id);
                MessageBox.Show("تمت إضافة الفرع.", "الفروع", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            }
            else
            {
                if (!_selectedBranchId.HasValue)
                {
                    MessageBox.Show("اختر فرعاً من الجدول للتعديل، أو استخدم «إضافة فرع» لفرع جديد.", "الفروع",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1, MsgRtl);
                    return;
                }

                await _warehouseService.UpdateBranchAsync(_selectedBranchId.Value, name, _branchActive.Checked, _currentUser.Id);
                MessageBox.Show("تم حفظ التعديلات.", "الفروع", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MsgRtl);
            }

            await RefreshBranchesGridAsync();
            await LoadWarehousesAsync();
            UpdatePosStockLocationLabel();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "الفروع", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1, MsgRtl);
        }
    }
}
