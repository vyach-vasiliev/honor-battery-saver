using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace HonorBatterySaver.Tray;

internal static class AppIconRenderer
{
    public static Icon CreateIcon(int size = 32)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.ScaleTransform(size / 48f, size / 48f);
            using var accent = new SolidBrush(Color.FromArgb(75, 104, 232));
            using var white = new SolidBrush(Color.White);
            using var leaf = new SolidBrush(Color.FromArgb(121, 230, 178));
            using var outer = CreateRoundedRectangle(new RectangleF(0, 0, 48, 48), 12);
            using var battery = CreateRoundedRectangle(new RectangleF(7, 14, 32, 22), 6);
            using var terminal = CreateRoundedRectangle(new RectangleF(38, 20, 4, 10), 1.5f);
            using var inner = CreateRoundedRectangle(new RectangleF(11, 18, 24, 14), 3);
            graphics.FillPath(accent, outer);
            graphics.FillPath(white, battery);
            graphics.FillPath(white, terminal);
            graphics.FillPath(accent, inner);
            using var leafShape = new GraphicsPath();
            leafShape.AddBezier(14, 29, 17, 22, 23, 20, 32, 20);
            leafShape.AddBezier(32, 20, 30, 27, 25, 31, 18, 31);
            leafShape.AddBezier(18, 31, 20, 28, 23, 25, 27, 23);
            leafShape.AddBezier(27, 23, 22, 24, 18, 26, 14, 29);
            leafShape.CloseFigure();
            graphics.FillPath(leaf, leafShape);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    public static Icon CreateModeIcon(byte stopChargePercent, bool hasError, int size = 32)
    {
        var resourceName = stopChargePercent switch
        {
            70 => "HonorBatterySaver.Tray.Assets.Tray.tray-70.ico",
            90 => "HonorBatterySaver.Tray.Assets.Tray.tray-90.ico",
            100 => "HonorBatterySaver.Tray.Assets.Tray.tray-100.ico",
            _ => throw new ArgumentOutOfRangeException(
                nameof(stopChargePercent), stopChargePercent, "No tray icon exists for this charge limit.")
        };

        using var stream = typeof(AppIconRenderer).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded tray icon was not found: {resourceName}");
        using var source = new Icon(stream, size, size);
        if (!hasError)
        {
            return (Icon)source.Clone();
        }

        using var bitmap = source.ToBitmap();
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var markerSize = Math.Max(5f, bitmap.Width * 0.31f);
            var markerInset = Math.Max(0.5f, bitmap.Width * 0.03f);
            var markerBounds = new RectangleF(
                bitmap.Width - markerSize - markerInset,
                markerInset,
                markerSize,
                markerSize);
            using var error = new SolidBrush(Color.FromArgb(225, 67, 67));
            using var errorOutline = new Pen(Color.White, Math.Max(1f, bitmap.Width * 0.06f));
            graphics.FillEllipse(error, markerBounds);
            graphics.DrawEllipse(errorOutline, markerBounds);
        }
        return CreateIconFromBitmap(bitmap);
    }

    private static Icon CreateIconFromBitmap(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            _ = DestroyIcon(handle);
        }
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint handle);
}
