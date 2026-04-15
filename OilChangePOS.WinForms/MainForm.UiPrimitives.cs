namespace OilChangePOS.WinForms;

/// <summary>Shared UI builders and grid styling used across tabs.</summary>
public partial class MainForm
{
    /// <summary>Single-line module page title height (white header cards).</summary>
    private const int ModuleHeaderTitleHeight = 40;
    private static readonly Padding ModuleHeaderCardPadding = new(16, 14, 16, 12);
    /// <summary>Module header subtitle: darker than <see cref="UiTextSecondary"/> for contrast on white.</summary>
    private static readonly Color ModuleHeaderSubtitleForeColor = Color.FromArgb(36, 40, 48);
    /// <summary>Slightly larger than <see cref="UiFont"/> so long Arabic lines stay readable.</summary>
    private static readonly Font ModuleHeaderSubtitleFont = new("Segoe UI", 11.75f, FontStyle.Regular, GraphicsUnit.Point);

    /// <summary>
    /// White bordered strip: <see cref="UiFontTitle"/> + optional <see cref="UiFont"/> subtitle, physical right alignment.
    /// Use <paramref name="autoSizeHeight"/> with <see cref="DockStyle.Top"/> so multi-line subtitles grow the panel.
    /// Use <see cref="DockStyle.Fill"/> + <paramref name="autoSizeHeight"/> false inside a fixed-height table row.
    /// </summary>
    private static Panel BuildStandardModuleHeaderCard(
        string titleText,
        string? subtitleText,
        bool subtitleItalic,
        DockStyle panelDock,
        bool autoSizeHeight,
        out Label titleLabel,
        out Label? subtitleLabel)
    {
        var panel = new Panel
        {
            Dock = panelDock,
            Margin = new Padding(0, 0, 0, 10),
            Padding = ModuleHeaderCardPadding,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RightToLeft = RightToLeft.No
        };

        var hasSub = !string.IsNullOrWhiteSpace(subtitleText);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = hasSub ? 2 : 1,
            BackColor = Color.White,
            RightToLeft = RightToLeft.No,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            AutoSize = autoSizeHeight && panelDock == DockStyle.Top,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ModuleHeaderTitleHeight));
        if (hasSub)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var ttl = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            Text = titleText,
            Font = UiFontTitle,
            ForeColor = UiTextPrimary,
            BackColor = Color.White,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.No,
            UseCompatibleTextRendering = false,
            Padding = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(ttl, 0, 0);

        Label? sub = null;
        if (!hasSub)
        {
            panel.Controls.Add(layout);
            titleLabel = ttl;
            subtitleLabel = null;
            if (autoSizeHeight && panelDock == DockStyle.Top)
                panel.Height = panel.Padding.Vertical + ModuleHeaderTitleHeight;
            return panel;
        }

        sub = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Text = subtitleText,
            Font = subtitleItalic
                ? new Font(ModuleHeaderSubtitleFont.FontFamily, ModuleHeaderSubtitleFont.Size, FontStyle.Italic, GraphicsUnit.Point)
                : ModuleHeaderSubtitleFont,
            ForeColor = ModuleHeaderSubtitleForeColor,
            BackColor = Color.White,
            TextAlign = ContentAlignment.TopRight,
            RightToLeft = RightToLeft.No,
            UseCompatibleTextRendering = false,
            Padding = new Padding(0, 2, 0, 0)
        };
        layout.Controls.Add(sub, 0, 1);
        panel.Controls.Add(layout);

        void SyncSubtitleWrapAndHeight(object? _, EventArgs __)
        {
            var innerW = Math.Max(280, panel.ClientSize.Width - panel.Padding.Horizontal);
            sub!.MaximumSize = new Size(innerW, 0);
            if (autoSizeHeight && panel.Dock == DockStyle.Top)
            {
                layout.PerformLayout();
                var innerH = layout.GetPreferredSize(new Size(innerW, int.MaxValue)).Height;
                panel.Height = panel.Padding.Vertical + innerH;
            }
        }

        panel.HandleCreated += SyncSubtitleWrapAndHeight;
        panel.Resize += SyncSubtitleWrapAndHeight;
        sub.SizeChanged += (_, _) =>
        {
            if (autoSizeHeight && panel.Dock == DockStyle.Top)
            {
                var innerW = Math.Max(280, panel.ClientSize.Width - panel.Padding.Horizontal);
                layout.PerformLayout();
                var innerH = layout.GetPreferredSize(new Size(innerW, int.MaxValue)).Height;
                panel.Height = panel.Padding.Vertical + innerH;
            }
        };

        titleLabel = ttl;
        subtitleLabel = sub;
        return panel;
    }

    private static Panel BuildAnalyticsKpiCard(string caption, Label valueLabel, Color accent, bool reportsRtl)
    {
        var wrap = new Panel { Width = 200, Height = 102, Margin = new Padding(10, 6, 10, 10), BackColor = Color.White };
        if (reportsRtl)
            wrap.RightToLeft = RightToLeft.Yes;
        var accentBar = new Panel { Height = 4, Dock = DockStyle.Top, BackColor = accent };
        var inner = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(14, 10, 14, 12) };
        if (reportsRtl)
            inner.RightToLeft = RightToLeft.Yes;
        inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        // RTL inner panel mirrors alignment: BottomLeft / MiddleLeft place text on the physical right edge.
        inner.Controls.Add(new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            ForeColor = UiTextSecondary,
            Font = UiFontCaption,
            TextAlign = ContentAlignment.BottomLeft,
            RightToLeft = reportsRtl ? RightToLeft.Yes : RightToLeft.No
        }, 0, 0);
        valueLabel.Dock = DockStyle.Fill;
        // LTR on the value: avoids WinForms mirroring MiddleRight→visual-left when the label itself is RTL.Yes.
        valueLabel.RightToLeft = RightToLeft.No;
        valueLabel.TextAlign = ContentAlignment.MiddleRight;
        valueLabel.ForeColor = accent;
        inner.Controls.Add(valueLabel, 0, 1);
        wrap.Controls.Add(inner);
        wrap.Controls.Add(accentBar);
        return wrap;
    }
    // ════════════════════════════════════════════════════════════════════════════
    // HELPER: two-column row (label | control) — used in cart bottom
    // ════════════════════════════════════════════════════════════════════════════
    private static TableLayoutPanel BuildTwoColRow(string labelText)
    {
        var tbl = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 4),
            RightToLeft = RightToLeft.No
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tbl.Controls.Add(new Label
        {
            Text = labelText,
            Dock = DockStyle.Fill,
            Font = new Font(UiFont.FontFamily, 10.75f, FontStyle.Bold, GraphicsUnit.Point),
            ForeColor = UiTextSecondary,
            TextAlign = ContentAlignment.MiddleRight,
            RightToLeft = RightToLeft.Yes
        }, 0, 0);
        return tbl;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // HELPER: white card with border
    // ════════════════════════════════════════════════════════════════════════════
    private static Panel BuildCard()
    {
        var p = new Panel { BackColor = Color.White };
        p.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(216, 219, 226), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        return p;
    }

    // ════════════════════════════════════════════════════════════════════════════
    // HELPER: bottom border line for toolbar panels
    // ════════════════════════════════════════════════════════════════════════════
    private static void BorderBottom(object? s, PaintEventArgs e)
    {
        if (s is not Control c) return;
        e.Graphics.DrawLine(new Pen(Color.FromArgb(224, 226, 230)),
            0, c.Height - 1, c.Width, c.Height - 1);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // KPI TILE HELPER
    // ════════════════════════════════════════════════════════════════════════════
    private static Panel BuildKpiTile(string caption, Label valueLabel, Color accent)
    {
        var card = new Panel { BackColor = Color.White };
        card.Paint += (s, e) =>
        {
            using var border = new Pen(Color.FromArgb(216, 219, 226));
            e.Graphics.DrawRectangle(border, 0, 0, card.Width - 1, card.Height - 1);
            using var bar = new SolidBrush(accent);
            e.Graphics.FillRectangle(bar, 0, 0, 4, card.Height);
        };

        var lbl = new Label
        {
            Text = caption,
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = Color.FromArgb(108, 108, 120),
            TextAlign = ContentAlignment.BottomRight,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0, 0, 10, 0)
        };

        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Segoe UI", 18.5f, FontStyle.Bold, GraphicsUnit.Point);
        valueLabel.ForeColor = accent;
        valueLabel.TextAlign = ContentAlignment.MiddleRight;
        valueLabel.RightToLeft = RightToLeft.Yes;
        valueLabel.Padding = new Padding(0, 0, 10, 0);
        valueLabel.Text = "—";

        card.Controls.Add(valueLabel);
        card.Controls.Add(lbl);
        return card;
    }

    private static Panel WrapInBanner(Label lbl, Color bg)
    {
        var p = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = bg,
            Margin = new Padding(0, 0, 8, 0),
            Padding = new Padding(12, 6, 12, 6)
        };
        lbl.ForeColor = Color.White;
        lbl.BackColor = Color.Transparent;
        lbl.TextAlign = ContentAlignment.MiddleRight;
        lbl.Dock = DockStyle.Fill;
        p.Controls.Add(lbl);
        return p;
    }

    /// <summary>Shared font/padding for warehouse toolbar and form action buttons (Arabic RTL).</summary>
    private static void ApplyWarehouseActionButtonFont(Button b)
    {
        b.Font = new Font("Segoe UI", 9.75f, FontStyle.Bold, GraphicsUnit.Point);
        b.Padding = new Padding(6, 4, 6, 4);
        b.UseCompatibleTextRendering = false;
        b.RightToLeft = RightToLeft.Yes;
    }

    private static void SizeWarehouseButtonToFitText(Button b, int minWidth, int maxWidth)
    {
        if (string.IsNullOrEmpty(b.Text)) return;
        var flags = TextFormatFlags.SingleLine | TextFormatFlags.RightToLeft | TextFormatFlags.NoPadding;
        var tw = TextRenderer.MeasureText(b.Text, b.Font, Size.Empty, flags).Width;
        var w = tw + b.Padding.Horizontal + 12;
        w = Math.Max(minWidth, Math.Min(maxWidth, w));
        b.Width = w;
    }

    /// <summary>Main warehouse form actions: width follows Arabic text (no clipped labels).</summary>
    private static void ApplyMainWarehousePrimaryButtonTypography(Button b, int minWidth = 112, int maxWidth = 200)
    {
        ApplyWarehouseActionButtonFont(b);
        SizeWarehouseButtonToFitText(b, minWidth, maxWidth);
    }

    /// <summary>Excel row on warehouse tab: keep requested pill width, same typography as other actions.</summary>
    private static void ApplyMainWarehouseFixedToolbarButtonTypography(Button b, int width)
    {
        ApplyWarehouseActionButtonFont(b);
        b.Width = width;
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = Color.White;
        grid.RowHeadersVisible = false;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(44, 62, 80);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = UiGridHeader;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(12, 10, 12, 10);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(44, 62, 80);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        grid.AlternatingRowsDefaultCellStyle.ForeColor = UiTextPrimary;
        grid.AlternatingRowsDefaultCellStyle.Font = UiGridCell;
        grid.DefaultCellStyle.Font = UiGridCell;
        grid.DefaultCellStyle.ForeColor = UiTextPrimary;
        grid.DefaultCellStyle.Padding = new Padding(12, 8, 12, 8);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.RowTemplate.Height = 38;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.ColumnHeadersHeight = 50;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.Font = UiGridCell;
    }

    /// <summary>
    /// Arabic report grids: <see cref="Control.RightToLeft"/> mirrors the client area, so
    /// <see cref="DataGridViewContentAlignment.MiddleRight"/> paints on the visual left; use
    /// <see cref="DataGridViewContentAlignment.MiddleLeft"/> so values and headers sit on the reading edge (physical right).
    /// </summary>
    private static void StyleReportGrid(DataGridView grid)
    {
        StyleGrid(grid);
        grid.RightToLeft = RightToLeft.Yes;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.AlternatingRowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
    }

    /// <summary>Equal-size pill for report module tabs (Arabic/RTL); pairs with Excel on the same row.</summary>
    private static Button CreateReportPillButton(string text)
    {
        const int w = 200;
        const int h = 54;
        var b = new Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(w, h),
            Padding = new Padding(10, 6, 10, 6),
            Margin = new Padding(6, 4, 6, 4),
            FlatStyle = FlatStyle.Flat,
            Font = UiFont,
            RightToLeft = RightToLeft.Yes,
            Cursor = Cursors.Hand,
            BackColor = Color.White,
            ForeColor = UiTextPrimary,
            UseCompatibleTextRendering = false,
            TextAlign = ContentAlignment.MiddleCenter
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(189, 195, 199);
        b.FlatAppearance.BorderSize = 1;
        return b;
    }

    private static Button BuildButton(string text, Color backColor) => BuildSizedButton(text, backColor, 188, 48);

    private static Button BuildSizedButton(string text, Color backColor, int width, int height)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = height,
            Padding = new Padding(14, 10, 14, 10),
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            BackColor = backColor,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11.25f, FontStyle.Bold, GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            UseCompatibleTextRendering = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.12f);
        return b;
    }

    /// <summary>
    /// Applies <paramref name="preferredDistance"/> once the split container has a real client size.
    /// Large <see cref="SplitContainer.Panel1MinSize"/> / <see cref="SplitContainer.Panel2MinSize"/> values
    /// must not be set on a new control (still at default ~150px); the runtime validates
    /// <see cref="SplitContainer.SplitterDistance"/> against those mins and throws immediately.
    /// </summary>
    private static void ApplyInitialSplitterDistance(SplitContainer split, int preferredDistance) =>
        ApplyInitialSplitterDistance(split, preferredDistance, split.Panel1MinSize, split.Panel2MinSize);

    private static void ApplyInitialSplitterDistance(SplitContainer split, int preferredDistance, int panel1MinSize, int panel2MinSize)
    {
        const int safeMin = 25;
        split.Panel1MinSize = safeMin;
        split.Panel2MinSize = safeMin;

        void OnSized(object? sender, EventArgs e)
        {
            var along = split.Orientation == Orientation.Horizontal ? split.ClientSize.Height : split.ClientSize.Width;
            var usable = along - split.SplitterWidth;
            if (usable < panel1MinSize + panel2MinSize)
                return;

            var maxDist = usable - panel2MinSize;
            var lo = Math.Min(panel1MinSize, maxDist);
            var hi = Math.Max(panel1MinSize, maxDist);
            var dist = Math.Clamp(preferredDistance, lo, hi);

            try
            {
                split.SplitterDistance = dist;
                split.Panel1MinSize = panel1MinSize;
                split.Panel2MinSize = panel2MinSize;
            }
            catch (InvalidOperationException)
            {
                return;
            }

            split.SizeChanged -= OnSized;
        }

        split.SizeChanged += OnSized;
        OnSized(split, EventArgs.Empty);
    }
}
