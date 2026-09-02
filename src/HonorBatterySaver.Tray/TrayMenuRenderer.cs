using System.Drawing;
using System.Windows.Forms;

namespace HonorBatterySaver.Tray;

internal sealed class TrayMenuRenderer(bool dark) : ToolStripProfessionalRenderer(new TrayMenuColorTable(dark))
{
    private readonly Color _textColor = dark ? Color.FromArgb(243, 245, 250) : Color.FromArgb(24, 32, 51);
    private readonly Color _mutedColor = dark ? Color.FromArgb(170, 178, 194) : Color.FromArgb(102, 112, 133);
    private readonly Color _accentColor = dark ? Color.FromArgb(127, 150, 255) : Color.FromArgb(75, 104, 232);
    private readonly Color _surfaceColor = dark ? Color.FromArgb(23, 26, 34) : Color.White;
    private readonly Color _hoverColor = dark ? Color.FromArgb(31, 36, 46) : Color.FromArgb(241, 244, 250);

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(_surfaceColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(_surfaceColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        using var background = new SolidBrush(e.Item.Selected && e.Item.Enabled ? _hoverColor : _surfaceColor);
        e.Graphics.FillRectangle(background, new Rectangle(Point.Empty, e.Item.Size));
        if (e.Item.Selected && e.Item.Enabled)
        {
            using var accent = new SolidBrush(_accentColor);
            e.Graphics.FillRectangle(accent, 0, 4, 3, Math.Max(0, e.Item.Height - 8));
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _textColor : _mutedColor;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item?.Enabled != false ? _textColor : _mutedColor;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var bounds = e.ImageRectangle;
        using var background = new SolidBrush(_accentColor);
        using var pen = new Pen(Color.White, 1.8f)
        {
            StartCap = System.Drawing.Drawing2D.LineCap.Round,
            EndCap = System.Drawing.Drawing2D.LineCap.Round
        };
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(background, bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
        e.Graphics.DrawLines(pen,
        [
            new PointF(bounds.X + bounds.Width * 0.29f, bounds.Y + bounds.Height * 0.52f),
            new PointF(bounds.X + bounds.Width * 0.44f, bounds.Y + bounds.Height * 0.67f),
            new PointF(bounds.X + bounds.Width * 0.72f, bounds.Y + bounds.Height * 0.35f)
        ]);
    }
}

internal sealed class TrayMenuColorTable(bool dark) : ProfessionalColorTable
{
    private readonly Color _surface = dark ? Color.FromArgb(23, 26, 34) : Color.White;
    private readonly Color _hover = dark ? Color.FromArgb(31, 36, 46) : Color.FromArgb(241, 244, 250);
    private readonly Color _pressed = dark ? Color.FromArgb(35, 42, 58) : Color.FromArgb(232, 237, 255);
    private readonly Color _border = dark ? Color.FromArgb(48, 53, 65) : Color.FromArgb(222, 227, 236);

    public override Color ToolStripDropDownBackground => _surface;
    public override Color ImageMarginGradientBegin => _surface;
    public override Color ImageMarginGradientMiddle => _surface;
    public override Color ImageMarginGradientEnd => _surface;
    public override Color MenuBorder => _border;
    public override Color MenuItemBorder => _hover;
    public override Color MenuItemSelected => _hover;
    public override Color MenuItemSelectedGradientBegin => _hover;
    public override Color MenuItemSelectedGradientEnd => _hover;
    public override Color MenuItemPressedGradientBegin => _pressed;
    public override Color MenuItemPressedGradientMiddle => _pressed;
    public override Color MenuItemPressedGradientEnd => _pressed;
    public override Color CheckBackground => _pressed;
    public override Color CheckSelectedBackground => _pressed;
    public override Color CheckPressedBackground => _pressed;
    public override Color SeparatorDark => _border;
    public override Color SeparatorLight => _border;
}
