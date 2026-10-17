using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace UltimateJoystickMapper.Controls;

public sealed class BrandLogo : Control
{
    public BrandLogo() { DoubleBuffered = true; Size = new Size(48, 48); }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); AppBrand.Draw(e.Graphics, ClientRectangle); }
}

public static class AppBrand
{
    public static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(64, 64, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) Draw(graphics, new Rectangle(0, 0, 64, 64));
        var handle = bitmap.GetHicon();
        try { using var temporary = Icon.FromHandle(handle); return (Icon)temporary.Clone(); }
        finally { DestroyIcon(handle); }
    }

    // DP monogram on the app's teal-to-indigo tile. Drawn as geometry rather than
    // text so it looks identical on every PC, whatever fonts are installed.
    public static void Draw(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = Math.Min(bounds.Width, bounds.Height) / 64f;
        graphics.TranslateTransform(bounds.Left, bounds.Top); graphics.ScaleTransform(scale, scale);

        using var shadow = new SolidBrush(Color.FromArgb(55, 0, 0, 0));
        graphics.FillRoundedRectangle(shadow, new RectangleF(5, 6, 56, 56), 16);
        using var tile = new LinearGradientBrush(new Rectangle(4, 3, 56, 56), Color.FromArgb(45, 212, 191), Color.FromArgb(99, 102, 241), 35f);
        graphics.FillRoundedRectangle(tile, new RectangleF(4, 3, 56, 56), 16);
        using var inset = new SolidBrush(Color.FromArgb(235, 15, 23, 42));
        graphics.FillRoundedRectangle(inset, new RectangleF(9, 8, 46, 46), 12);

        // D: stem and bowl, white.
        using var dPen = new Pen(Color.White, 5f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using (var dPath = new GraphicsPath())
        {
            dPath.AddLine(15, 17, 15, 45);
            dPath.AddLine(15, 45, 19, 45);
            dPath.AddArc(6, 17, 26, 28, 90, -180);
            dPath.CloseFigure();
            graphics.DrawPath(dPen, dPath);
        }

        // P: stem and bowl in the accent green.
        using var pPen = new Pen(Color.FromArgb(74, 222, 128), 5f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using (var pPath = new GraphicsPath())
        {
            pPath.AddLine(38, 45, 38, 17);
            pPath.AddLine(38, 17, 43, 17);
            pPath.AddArc(36, 17, 14, 15, 270, 180);
            pPath.AddLine(43, 32, 38, 32);
            graphics.DrawPath(pPen, pPath);
        }
        graphics.ResetTransform();
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr handle);
}

internal static class GraphicsExtensions
{
    public static void FillRoundedRectangle(this Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = new GraphicsPath(); var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90); path.AddArc(bounds.Right-diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right-diameter, bounds.Bottom-diameter, diameter, diameter, 0, 90); path.AddArc(bounds.Left, bounds.Bottom-diameter, diameter, diameter, 90, 90);
        path.CloseFigure(); graphics.FillPath(brush, path);
    }
}
