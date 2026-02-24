using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;

namespace GammaControl.Controls;

public partial class GammaCurveControl : UserControl
{
    private ushort[] _ramp = new ushort[256];
    private bool _isDrawMode = false;
    private bool _isDrawing = false;
    private int _lastDrawX = -1;
    private ushort _lastDrawY = 0;
    private bool _suppressSkewEvents = false;

    // Horizontal skew — non-destructive overlay, baked on slider release
    private double _topSkewX = 255.0;    // input index where curve reaches max (0–255)
    private double _bottomSkewX = 0.0;   // input index where curve leaves zero (0–255)

    public event EventHandler<ushort[]>? DrawnRampChanged;

    public GammaCurveControl()
    {
        InitializeComponent();

        for (int i = 0; i < 256; i++)
            _ramp[i] = (ushort)(i * 256);

        CurveCanvas.SizeChanged += (_, _) => DrawCurve();
        Loaded += (_, _) =>
        {
            DrawCurve();
            // Bake horizontal skew when the user releases either horizontal slider
            TopSkewSlider.AddHandler(Thumb.DragCompletedEvent,
                new DragCompletedEventHandler(HSkew_DragCompleted));
            BottomSkewSlider.AddHandler(Thumb.DragCompletedEvent,
                new DragCompletedEventHandler(HSkew_DragCompleted));
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void UpdateRamp(ushort[] ramp)
    {
        _ramp = ramp;
        DrawCurve();
    }

    public void SetDrawMode(bool enable)
    {
        _isDrawMode = enable;
        if (enable)
        {
            // Reset horizontal skew
            _topSkewX = 255.0;
            _bottomSkewX = 0.0;

            _suppressSkewEvents = true;
            LeftSkewSlider.Value  = _ramp[0];
            RightSkewSlider.Value = _ramp[255];
            TopSkewSlider.Value    = 255;
            BottomSkewSlider.Value = 0;
            _suppressSkewEvents = false;

            LeftSkewSlider.Visibility   = Visibility.Visible;
            RightSkewSlider.Visibility  = Visibility.Visible;
            TopSkewSlider.Visibility    = Visibility.Visible;
            BottomSkewSlider.Visibility = Visibility.Visible;
        }
        else
        {
            // Bake any pending horizontal skew before leaving draw mode
            BakeHorizontalSkew();

            LeftSkewSlider.Visibility   = Visibility.Collapsed;
            RightSkewSlider.Visibility  = Visibility.Collapsed;
            TopSkewSlider.Visibility    = Visibility.Collapsed;
            BottomSkewSlider.Visibility = Visibility.Collapsed;
        }
        DrawCurve();
    }

    // ── Horizontal remapping ─────────────────────────────────────────────────
    // The drawn _ramp is the base.  Top/bottom sliders remap the input axis:
    //   t = (i - bottomSkewX) / (topSkewX - bottomSkewX)
    // i values outside [bottomSkewX, topSkewX] clamp to the nearest endpoint.
    // Endpoints _ramp[0] and _ramp[255] are always preserved after remapping.

    private ushort[] GetAppliedRamp()
    {
        if (_bottomSkewX <= 0 && _topSkewX >= 255) return _ramp;

        double from  = _bottomSkewX;
        double range = _topSkewX - from;
        if (range <= 0) return _ramp;

        var applied = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            double t      = Math.Clamp((i - from) / range, 0.0, 1.0);
            int    srcIdx = (int)Math.Round(t * 255.0);
            applied[i] = _ramp[Math.Clamp(srcIdx, 0, 255)];
        }
        return applied;
    }

    private void BakeHorizontalSkew()
    {
        if (_bottomSkewX <= 0 && _topSkewX >= 255) return;

        _ramp = GetAppliedRamp();
        _topSkewX    = 255.0;
        _bottomSkewX = 0.0;

        // Re-sync vertical sliders to the new endpoints
        _suppressSkewEvents = true;
        LeftSkewSlider.Value   = _ramp[0];
        RightSkewSlider.Value  = _ramp[255];
        TopSkewSlider.Value    = 255;
        BottomSkewSlider.Value = 0;
        _suppressSkewEvents = false;
    }

    private void HSkew_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_isDrawMode) return;
        BakeHorizontalSkew();
        DrawCurve();
        DrawnRampChanged?.Invoke(this, _ramp);
    }

    private void TopSkewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDrawMode || _suppressSkewEvents) return;
        _topSkewX = Math.Max(e.NewValue, _bottomSkewX + 10);
        DrawCurve();
        DrawnRampChanged?.Invoke(this, GetAppliedRamp());
    }

    private void BottomSkewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDrawMode || _suppressSkewEvents) return;
        _bottomSkewX = Math.Min(e.NewValue, _topSkewX - 10);
        DrawCurve();
        DrawnRampChanged?.Invoke(this, GetAppliedRamp());
    }

    // ── Curve rendering ───────────────────────────────────────────────────────

    private void DrawCurve()
    {
        CurveCanvas.Children.Clear();

        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var gridBrush     = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        var identityBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70));
        var curveBrush    = _isDrawMode
            ? new SolidColorBrush(Color.FromRgb(255, 165, 0))
            : new SolidColorBrush(Colors.Cyan);

        for (int i = 1; i <= 3; i++)
        {
            double xg = w * i / 4.0, yg = h * i / 4.0;
            CurveCanvas.Children.Add(new Line { X1 = xg, Y1 = 0, X2 = xg, Y2 = h, Stroke = gridBrush, StrokeThickness = 1 });
            CurveCanvas.Children.Add(new Line { X1 = 0, Y1 = yg, X2 = w, Y2 = yg, Stroke = gridBrush, StrokeThickness = 1 });
        }

        CurveCanvas.Children.Add(new Line
        {
            X1 = 0, Y1 = h, X2 = w, Y2 = 0,
            Stroke = identityBrush, StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        });

        // Draw the fully-applied ramp (includes horizontal remap if active)
        var display  = GetAppliedRamp();
        var polyline = new Polyline { Stroke = curveBrush, StrokeThickness = 1.5, Points = new PointCollection() };
        for (int i = 0; i < 256; i++)
        {
            double x = w * i / 255.0;
            double y = h * (1.0 - display[i] / 65280.0);
            polyline.Points.Add(new Point(x, y));
        }
        CurveCanvas.Children.Add(polyline);

        // Ghost the base _ramp when horizontal skew is active so you can see the difference
        if (_isDrawMode && (_bottomSkewX > 0 || _topSkewX < 255))
        {
            var ghostLine = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 165, 0)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 3 },
                Points = new PointCollection()
            };
            for (int i = 0; i < 256; i++)
            {
                double x = w * i / 255.0;
                double y = h * (1.0 - _ramp[i] / 65280.0);
                ghostLine.Points.Add(new Point(x, y));
            }
            CurveCanvas.Children.Add(ghostLine);
        }
    }

    // ── Vertical skew sliders ─────────────────────────────────────────────────

    private void LeftSkewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDrawMode || _suppressSkewEvents) return;

        double delta = e.NewValue - e.OldValue;
        if (delta == 0) return;

        for (int i = 0; i < 256; i++)
        {
            double t = 1.0 - i / 255.0;
            _ramp[i] = (ushort)Math.Clamp(_ramp[i] + delta * t, 0, 65280);
        }

        DrawCurve();
        DrawnRampChanged?.Invoke(this, GetAppliedRamp());
    }

    private void RightSkewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDrawMode || _suppressSkewEvents) return;

        double delta = e.NewValue - e.OldValue;
        if (delta == 0) return;

        for (int i = 0; i < 256; i++)
        {
            double t = i / 255.0;
            _ramp[i] = (ushort)Math.Clamp(_ramp[i] + delta * t, 0, 65280);
        }

        DrawCurve();
        DrawnRampChanged?.Invoke(this, GetAppliedRamp());
    }

    // ── Freehand drawing ──────────────────────────────────────────────────────

    private void CurveCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawMode) return;
        _isDrawing = true;
        _lastDrawX = -1;
        PaintAt(e.GetPosition(CurveCanvas));
        CurveCanvas.CaptureMouse();
    }

    private void CurveCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawMode || !_isDrawing) return;
        PaintAt(e.GetPosition(CurveCanvas));
    }

    private void CurveCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawMode) return;
        _isDrawing = false;
        _lastDrawX = -1;
        CurveCanvas.ReleaseMouseCapture();

        _suppressSkewEvents = true;
        LeftSkewSlider.Value  = _ramp[0];
        RightSkewSlider.Value = _ramp[255];
        _suppressSkewEvents = false;

        DrawnRampChanged?.Invoke(this, GetAppliedRamp());
    }

    private void PaintAt(Point pos)
    {
        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        int    curX = (int)Math.Clamp(pos.X / w * 255.0, 0, 255);
        ushort curY = (ushort)(Math.Clamp(1.0 - pos.Y / h, 0.0, 1.0) * 65280);

        if (_lastDrawX >= 0 && _lastDrawX != curX)
        {
            int fromX = _lastDrawX, toX = curX;
            ushort fromY = _lastDrawY, toY = curY;
            if (fromX > toX) { (fromX, toX) = (toX, fromX); (fromY, toY) = (toY, fromY); }

            for (int ix = fromX; ix <= toX; ix++)
            {
                double t = (double)(ix - fromX) / (toX - fromX);
                _ramp[ix] = (ushort)Math.Clamp(fromY + t * (toY - fromY), 0, 65280);
            }
        }
        else
        {
            _ramp[curX] = curY;
        }

        _lastDrawX = curX;
        _lastDrawY = curY;
        DrawCurve();
    }
}
