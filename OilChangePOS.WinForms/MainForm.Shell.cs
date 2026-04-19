using ClosedXML.Excel;
using OilChangePOS.Business;
using OilChangePOS.Domain;
using System.Globalization;

namespace OilChangePOS.WinForms;

public partial class MainForm : Form
{

    public MainForm(
        ISalesService salesService,
        IInventoryService inventoryService,
        IReportService reportService,
        IExpenseService expenseService,
        ITransferService transferService,
        IWarehouseService warehouseService,
        ICustomerService customerService,
        IAuthService authService,
        ICatalogAdminService catalogAdminService,
        IMainWarehouseAdminService mainWarehouseAdminService,
        IProductCatalogService productCatalogService,
        AppUser currentUser)
    {
        _catalogAdminService = catalogAdminService;
        _mainWarehouseAdminService = mainWarehouseAdminService;
        _productCatalogService = productCatalogService;
        _salesService = salesService;
        _inventoryService = inventoryService;
        _reportService = reportService;
        _expenseService = expenseService;
        _transferService = transferService;
        _warehouseService = warehouseService;
        _customerService = customerService;
        _authService = authService;
        _currentUser = currentUser;

        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Text = "نظام نقطة بيع تغيير الزيت والمخزون";
        Width = 1500;
        Height = 900;
        MinimumSize = new Size(1300, 780);
        Font = UiFont;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 247, 250);
        KeyPreview = true;
        KeyDown += OnMainFormKeyDown;
        BuildUi();
        ApplyRoleToUi();
        ApplyUnifiedFont(this);
        RestoreMainWarehouseFieldCaptionFonts(this);
        ApplyReportsVisualStyle();
        Load += async (_, _) => await LoadDataAsync();
    }

    private async void OnMainFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (_mainWarehouseTabPage is null || _mainTabs.SelectedTab != _mainWarehouseTabPage)
            return;

        if (_mainWarehouseGrid.IsCurrentCellInEditMode)
            return;

        if (ActiveControl == _mainWarehouseGrid && e.KeyCode == Keys.Enter)
            return;

        if (e.KeyCode == Keys.Enter && !e.Control && !e.Shift && !e.Alt)
        {
            if (ActiveControl is DateTimePicker)
                return;

            if (_mwCmdAdd is null || !_mwCmdAdd.Enabled)
                return;

            e.Handled = true;
            e.SuppressKeyPress = true;
            await AddMainWarehouseManualAsync();
            return;
        }

        if (e.KeyCode == Keys.Delete && _selectedMainPurchaseId.HasValue)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            await DeleteMainWarehouseManualAsync();
            return;
        }

        if (e.KeyCode == Keys.E && e.Control)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            await ExportMainWarehouseExcelAsync();
        }
    }



    private void ApplyRoleToUi()
    {
        var admin = _currentUser.Role == UserRole.Admin;
        Text = admin
            ? $"نقطة بيع تغيير الزيت — {_currentUser.Username} (مدير)"
            : $"نقطة بيع تغيير الزيت — {_currentUser.Username} (فرع)";

        if (admin)
        {
            if (_sidebarNavButtons.Count > 2)
            {
                _sidebarNavButtons[0].Visible = false;
                _sidebarNavButtons[1].Visible = false;
                _sidebarNavButtons[2].Visible = false;
            }
            _mainTabs.SelectedIndex = 5;
            ApplySidebarNavHighlight(_mainTabs.SelectedIndex);
            return;
        }

        for (var i = 3; i < _sidebarNavButtons.Count; i++)
            _sidebarNavButtons[i].Visible = false;

        _inventoryShowZeroStockCheck.Checked = true;
        _inventoryAdminGroup.Visible = false;
        _inventoryAddProductGroup.Visible = false;
        _inventoryTopPanel.Height = 268;
        _mainTabs.SelectedIndex = 0;
        ApplySidebarNavHighlight(_mainTabs.SelectedIndex);
    }

    private void OnMainTabsSelecting(object? sender, TabControlCancelEventArgs e)
    {
        if (_currentUser.Role == UserRole.Admin)
        {
            if (e.TabPageIndex < 3)
                e.Cancel = true;
            return;
        }

        if (e.TabPageIndex > 2)
            e.Cancel = true;
    }

    private void BuildUi()
    {
        var sidebar = BuildSidebar();
        sidebar.Dock = DockStyle.Left;
        sidebar.Width = 268;

        var content = BuildMainContent();
        content.Dock = DockStyle.Fill;

        Controls.Add(content);
        Controls.Add(sidebar);
    }

    private Control BuildSidebar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(38, 43, 48) };
        var header = new Label
        {
            Text = "نقطة بيع تغيير الزيت",
            Dock = DockStyle.Top,
            Height = 56,
            ForeColor = Color.White,
            Font = UiFontSection,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 12, 0),
            RightToLeft = RightToLeft.Yes
        };

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10, 16, 10, 12),
            RightToLeft = RightToLeft.Yes
        };

        menu.Controls.Add(BuildMenuButton("طلب / بيع", 0));
        menu.Controls.Add(BuildMenuButton("المخزون", 1));
        menu.Controls.Add(BuildMenuButton("تقارير الفرع", 2));
        menu.Controls.Add(BuildMenuButton("الشركات والأصناف", 3));
        menu.Controls.Add(BuildMenuButton("الفروع", 4));
        menu.Controls.Add(BuildMenuButton("المستودع الرئيسي", 5));
        menu.Controls.Add(BuildMenuButton("استلام مشتريات", 6));
        menu.Controls.Add(BuildMenuButton("التحويلات", 7));
        menu.Controls.Add(BuildMenuButton("جرد المخزون", 8));
        menu.Controls.Add(BuildMenuButton("التقارير", 9));

        panel.Controls.Add(menu);
        panel.Controls.Add(header);
        return panel;
    }

    private Control BuildMainContent()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(242, 245, 249) };
        var topbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.FromArgb(33, 37, 41) };
        var topbarTitle = new Label
        {
            Text = " ",
            Dock = DockStyle.Left,
            Width = 40,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter
        };

        var logoutBtn = new Button
        {
            Text = "تسجيل الخروج",
            AutoSize = true,
            MinimumSize = new Size(120, 32),
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(52, 58, 64),
            Cursor = Cursors.Hand,
            Margin = new Padding(6, 0, 0, 0),
            Font = new Font("Segoe UI", 10.75f, FontStyle.Bold, GraphicsUnit.Point),
            UseCompatibleTextRendering = false
        };
        logoutBtn.FlatAppearance.BorderSize = 0;
        logoutBtn.Click += (_, _) =>
        {
            if (MessageBox.Show(
                    "تسجيل الخروج من النظام؟",
                    "تأكيد",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2,
                    MsgRtl) != DialogResult.Yes)
                return;
            LogoutRequested = true;
            Close();
        };

        var userCaps = _currentUser.Role == UserRole.Admin
            ? "مدير"
            : _currentUser.Role == UserRole.Manager
                ? "مدير فرع"
                : _currentUser.Role == UserRole.Cashier
                    ? "كاشير"
                    : "فرع";
        var userLine = new Label
        {
            AutoSize = true,
            Text = $"{_currentUser.Username} · {userCaps}",
            ForeColor = Color.FromArgb(230, 232, 235),
            Font = UiFont,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 6, 16, 0)
        };

        var rightBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(8, 8, 12, 8),
            BackColor = Color.FromArgb(33, 37, 41)
        };
        rightBar.Controls.Add(logoutBtn);
        rightBar.Controls.Add(userLine);

        topbar.Controls.Add(rightBar);
        topbar.Controls.Add(topbarTitle);

        _mainTabs.TabPages.Add(BuildPosTab());
        _mainTabs.TabPages.Add(BuildInventoryTab());
        _mainTabs.TabPages.Add(BuildBranchReportsTab());
        _mainTabs.TabPages.Add(BuildCatalogTab());
        _mainTabs.TabPages.Add(BuildBranchesAdminTab());
        _mainTabs.TabPages.Add(BuildMainWarehouseTab());
        _mainTabs.TabPages.Add(BuildBulkPurchaseTab());
        _mainTabs.TabPages.Add(BuildTransferTab());
        _mainTabs.TabPages.Add(BuildAuditTab());
        _mainTabs.TabPages.Add(BuildReportsTab());
        _mainTabs.Appearance = TabAppearance.FlatButtons;
        _mainTabs.ItemSize = new Size(0, 1);
        _mainTabs.SizeMode = TabSizeMode.Fixed;
        _mainTabs.RightToLeft = RightToLeft.Yes;
        _mainTabs.RightToLeftLayout = false;
        _mainTabs.Selecting += OnMainTabsSelecting;
        _mainTabs.SelectedIndexChanged += async (_, _) =>
        {
            ApplySidebarNavHighlight(_mainTabs.SelectedIndex);
            if (_mainTabs.SelectedIndex == 2)
                await RefreshBranchOnlyReportsAsync();
        };

        var tabHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 10, 16, 14),
            BackColor = Color.FromArgb(242, 245, 249)
        };
        tabHost.Controls.Add(_mainTabs);
        panel.Controls.Add(tabHost);
        panel.Controls.Add(topbar);
        return panel;
    }

    private Button BuildMenuButton(string text, int targetTab)
    {
        var button = new Button
        {
            Text = text,
            Width = 220,
            Height = 50,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = SidebarNavBg,
            ForeColor = Color.FromArgb(230, 232, 235),
            TextAlign = ContentAlignment.MiddleRight,
            Tag = targetTab,
            Margin = new Padding(0, 0, 0, 10),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 11.5f, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Padding(12, 0, 16, 0),
            RightToLeft = RightToLeft.Yes,
            UseCompatibleTextRendering = false
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(48, 54, 64);
        button.MouseEnter += (_, _) =>
        {
            if (_mainTabs.SelectedIndex == targetTab)
                return;
            button.BackColor = SidebarNavHover;
            button.FlatAppearance.BorderColor = Color.FromArgb(72, 80, 94);
        };
        button.MouseLeave += (_, _) =>
        {
            var active = _mainTabs.SelectedIndex == targetTab;
            button.BackColor = active ? SidebarNavActive : SidebarNavBg;
            button.FlatAppearance.BorderColor = active ? Color.FromArgb(52, 152, 219) : Color.FromArgb(48, 54, 64);
            button.FlatAppearance.BorderSize = active ? 2 : 1;
        };
        button.Click += (_, _) =>
        {
            if (targetTab < 0 || targetTab >= _mainTabs.TabPages.Count) return;
            if (_currentUser.Role == UserRole.Admin)
            {
                if (targetTab < 3) return;
            }
            else if (targetTab > 2)
            {
                return;
            }

            _mainTabs.SelectedIndex = targetTab;
            ApplySidebarNavHighlight(targetTab);
        };
        _sidebarNavButtons.Add(button);
        return button;
    }

    private void ApplySidebarNavHighlight(int selectedTabIndex)
    {
        foreach (var b in _sidebarNavButtons)
        {
            if (b.Tag is not int idx) continue;
            var active = idx == selectedTabIndex;
            b.BackColor = active ? SidebarNavActive : SidebarNavBg;
            b.ForeColor = active ? Color.White : Color.FromArgb(230, 232, 235);
            b.FlatAppearance.BorderColor = active ? Color.FromArgb(52, 152, 219) : Color.FromArgb(48, 54, 64);
            b.FlatAppearance.BorderSize = active ? 2 : 1;
        }
    }

}

