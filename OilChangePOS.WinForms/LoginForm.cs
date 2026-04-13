using OilChangePOS.Business;
using OilChangePOS.Domain;

namespace OilChangePOS.WinForms;

internal sealed class LoginForm : Form
{
    private static readonly Color TextPrimary = Color.FromArgb(30, 30, 30);
    private static readonly Font BodyFont = new("Segoe UI", 10.75f, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font CaptionFont = new("Segoe UI", 10.75f, FontStyle.Bold, GraphicsUnit.Point);
    private static readonly Font ButtonFont = new("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Point);

    private readonly IAuthService _auth;
    private readonly TextBox _user = new() { Width = 260, PlaceholderText = "اسم المستخدم" };
    private readonly TextBox _pass = new() { Width = 260, UseSystemPasswordChar = true, PlaceholderText = "كلمة المرور" };
    public AppUser? AuthenticatedUser { get; private set; }

    public LoginForm(IAuthService auth)
    {
        _auth = auth;
        Text = "تسجيل الدخول — نقطة بيع تغيير الزيت";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = BodyFont;
        ForeColor = TextPrimary;
        Width = 360;
        Height = 220;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(16),
            RightToLeft = RightToLeft.Yes
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 4; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        layout.Controls.Add(new Label { Text = "المستخدم", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = CaptionFont, ForeColor = TextPrimary, RightToLeft = RightToLeft.Yes }, 0, 0);
        layout.Controls.Add(_user, 1, 0);
        layout.Controls.Add(new Label { Text = "كلمة المرور", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill, Font = CaptionFont, ForeColor = TextPrimary, RightToLeft = RightToLeft.Yes }, 0, 1);
        layout.Controls.Add(_pass, 1, 1);

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, WrapContents = false };
        var ok = new Button { Text = "دخول", Width = 100, DialogResult = DialogResult.None, Font = ButtonFont, UseCompatibleTextRendering = false };
        var cancel = new Button { Text = "إلغاء", Width = 100, DialogResult = DialogResult.Cancel, Font = ButtonFont, UseCompatibleTextRendering = false };
        ok.Click += async (_, _) => await TryLoginAsync();
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        _pass.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await TryLoginAsync();
            }
        };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.SetColumnSpan(buttons, 2);
        layout.Controls.Add(buttons, 0, 3);

        _user.Font = BodyFont;
        _user.ForeColor = TextPrimary;
        _user.RightToLeft = RightToLeft.Yes;
        _pass.Font = BodyFont;
        _pass.ForeColor = TextPrimary;
        _pass.RightToLeft = RightToLeft.Yes;

        Controls.Add(layout);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private async Task TryLoginAsync()
    {
        try
        {
            var u = _user.Text.Trim();
            var p = _pass.Text;
            if (string.IsNullOrEmpty(u) || string.IsNullOrEmpty(p))
            {
                MessageBox.Show("أدخل اسم المستخدم وكلمة المرور.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                return;
            }

            var user = await _auth.LoginAsync(u, p);
            if (user is null)
            {
                MessageBox.Show("بيانات الدخول غير صحيحة أو المستخدم غير مفعّل.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
                return;
            }

            AuthenticatedUser = user;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
    }
}
