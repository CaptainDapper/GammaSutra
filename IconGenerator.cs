using System.Drawing;
using System.Drawing.Drawing2D;

namespace GammaControl;

public static class IconGenerator
{
    private const int Size = 64;
    private const int Pad  = 6;

    /// <summary>Returns a System.Drawing.Icon for the system tray.</summary>
    public static System.Drawing.Icon CreateIcon()
    {
        using var bmp  = Draw();
        var hIcon = bmp.GetHicon();
        try   { return (System.Drawing.Icon)System.Drawing.Icon.FromHandle(hIcon).Clone(); }
        finally { NativeMethods.DestroyIcon(hIcon); }
    }

    /// <summary>Returns a WPF ImageSource for Window.Icon.</summary>
    public static System.Windows.Media.ImageSource CreateImageSource()
    {
        using var bmp  = Draw();
        var hBmp = bmp.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBmp, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        }
        finally { NativeMethods.DeleteObject(hBmp); }
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    private static Bitmap Draw()
    {
        var bmp = new Bitmap(Size, Size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background
        g.Clear(Color.FromArgb(0x0F, 0x0F, 0x0F));

        int draw = Size - 2 * Pad; // usable pixels in each axis

        // Subtle grid lines at 25% and 75%
        using (var gridPen = new Pen(Color.FromArgb(0x1C, 0x1C, 0x1C), 1f))
        {
            float q1 = Pad + draw * 0.25f, q3 = Pad + draw * 0.75f;
            g.DrawLine(gridPen, q1, Pad, q1, Pad + draw);
            g.DrawLine(gridPen, q3, Pad, q3, Pad + draw);
            g.DrawLine(gridPen, Pad, q1, Pad + draw, q1);
            g.DrawLine(gridPen, Pad, q3, Pad + draw, q3);
        }

        // Identity diagonal
        using (var identPen = new Pen(Color.FromArgb(0x2E, 0x2E, 0x2E), 1f))
            g.DrawLine(identPen, Pad, Pad + draw, Pad + draw, Pad);

        // Build S-curve points (app's own formula: v += s*4v(1-v)(v-0.5), s=0.65)
        var pts = new PointF[draw + 1];
        for (int i = 0; i <= draw; i++)
        {
            double xn = i / (double)draw;
            double v  = xn;
            v += 0.65 * 4.0 * v * (1.0 - v) * (v - 0.5);
            v  = Math.Clamp(v, 0.0, 1.0);
            pts[i] = new PointF(Pad + i, Pad + (float)((1.0 - v) * draw));
        }

        // Outer glow
        using (var glow = new Pen(Color.FromArgb(45, 0, 200, 200), 6f) { LineJoin = LineJoin.Round })
            g.DrawCurve(glow, pts, 0.3f);

        // Inner glow
        using (var glow2 = new Pen(Color.FromArgb(80, 0, 220, 220), 3.5f) { LineJoin = LineJoin.Round })
            g.DrawCurve(glow2, pts, 0.3f);

        // Main curve line
        using (var curvePen = new Pen(Color.FromArgb(0x00, 0xD8, 0xD8), 1.8f) { LineJoin = LineJoin.Round })
            g.DrawCurve(curvePen, pts, 0.3f);

        // Endpoint dots for polish
        float r = 2.5f;
        using var dotBrush = new SolidBrush(Color.FromArgb(0x00, 0xD8, 0xD8));
        g.FillEllipse(dotBrush, pts[0].X - r, pts[0].Y - r, r * 2, r * 2);
        g.FillEllipse(dotBrush, pts[draw].X - r, pts[draw].Y - r, r * 2, r * 2);

        return bmp;
    }
}
