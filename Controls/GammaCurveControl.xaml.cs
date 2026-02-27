using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using GammaControl.Models;
using Color = System.Windows.Media.Color;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;

namespace GammaControl.Controls;

public class DrawnRampChangedEventArgs : EventArgs
{
    public int Channel { get; init; }
    public ushort[] Ramp { get; init; } = null!;
}

public class BezierPointsChangedEventArgs : EventArgs
{
    public int Channel { get; init; }
    public List<BezierPoint> Points { get; init; } = null!;
}

public partial class GammaCurveControl : UserControl
{
    private ushort[][] _ramps = [new ushort[256], new ushort[256], new ushort[256]];
    private int _activeChannel = -1; // -1 = All, 0=R, 1=G, 2=B
    private bool _isDrawMode = false;
    private bool _isDrawing = false;
    private int _lastDrawX = -1;
    private ushort _lastDrawY = 0;
    private bool _suppressSkewEvents = false;

    // Horizontal skew — non-destructive overlay, baked on slider release
    private double _topSkewX = 255.0;
    private double _bottomSkewX = 0.0;

    // Bezier mode state (per-channel)
    private bool _isBezierMode = false;
    private List<BezierPoint>[] _bezierPointsPerChannel =
    [
        BezierEvaluator.DefaultPoints(),
        BezierEvaluator.DefaultPoints(),
        BezierEvaluator.DefaultPoints()
    ];
    private enum BezierDragTarget { None, Anchor, HandleIn, HandleOut }
    private BezierDragTarget _dragTarget = BezierDragTarget.None;
    private int _dragPointIndex = -1;

    public event EventHandler<DrawnRampChangedEventArgs>? DrawnRampChanged;
    public event EventHandler<BezierPointsChangedEventArgs>? BezierPointsChanged;

    public int ActiveChannel
    {
        get => _activeChannel;
        set { _activeChannel = value; DrawCurve(); }
    }

    // The channel index used for draw/bezier interaction
    private int EditChannel => _activeChannel < 0 ? 0 : _activeChannel;

    public GammaCurveControl()
    {
        InitializeComponent();

        for (int ch = 0; ch < 3; ch++)
            for (int i = 0; i < 256; i++)
                _ramps[ch][i] = (ushort)(i * 256);

        CurveCanvas.SizeChanged += (_, _) => DrawCurve();
        Loaded += (_, _) =>
        {
            DrawCurve();
            TopSkewSlider.AddHandler(Thumb.DragCompletedEvent,
                new DragCompletedEventHandler(HSkew_DragCompleted));
            BottomSkewSlider.AddHandler(Thumb.DragCompletedEvent,
                new DragCompletedEventHandler(HSkew_DragCompleted));
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void UpdateRamps(ushort[] r, ushort[] g, ushort[] b, int activeChannel)
    {
        _ramps[0] = r;
        _ramps[1] = g;
        _ramps[2] = b;
        _activeChannel = activeChannel;
        DrawCurve();
    }

    // Legacy single-ramp update (sets all 3 channels the same)
    public void UpdateRamp(ushort[] ramp)
    {
        _ramps[0] = ramp;
        _ramps[1] = (ushort[])ramp.Clone();
        _ramps[2] = (ushort[])ramp.Clone();
        DrawCurve();
    }

    public void SetDrawMode(bool enable)
    {
        _isDrawMode = enable;
        if (enable)
        {
            _isBezierMode = false;
            _topSkewX = 255.0;
            _bottomSkewX = 0.0;

            _suppressSkewEvents = true;
            var editRamp = _ramps[EditChannel];
            LeftSkewSlider.Value  = editRamp[0];
            RightSkewSlider.Value = editRamp[255];
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
            BakeHorizontalSkew();
            LeftSkewSlider.Visibility   = Visibility.Collapsed;
            RightSkewSlider.Visibility  = Visibility.Collapsed;
            TopSkewSlider.Visibility    = Visibility.Collapsed;
            BottomSkewSlider.Visibility = Visibility.Collapsed;
        }
        DrawCurve();
    }

    public void SetBezierMode(bool enable, List<BezierPoint>?[]? pointsPerChannel = null)
    {
        _isBezierMode = enable;
        if (enable)
        {
            _isDrawMode = false;
            if (pointsPerChannel != null)
            {
                for (int ch = 0; ch < 3; ch++)
                {
                    if (pointsPerChannel[ch] != null)
                        _bezierPointsPerChannel[ch] = pointsPerChannel[ch]!.Select(p => p.Clone()).ToList();
                    else
                        _bezierPointsPerChannel[ch] = BezierEvaluator.DefaultPoints();
                }
            }

            for (int ch = 0; ch < 3; ch++)
                _ramps[ch] = BezierEvaluator.Evaluate(_bezierPointsPerChannel[ch]);

            LeftSkewSlider.Visibility   = Visibility.Collapsed;
            RightSkewSlider.Visibility  = Visibility.Collapsed;
            TopSkewSlider.Visibility    = Visibility.Collapsed;
            BottomSkewSlider.Visibility = Visibility.Collapsed;
        }
        DrawCurve();
    }

    // Overload for backward compat (single list of points)
    public void SetBezierMode(bool enable, List<BezierPoint>? points)
    {
        if (points != null)
            SetBezierMode(enable, [points, points.Select(p => p.Clone()).ToList(), points.Select(p => p.Clone()).ToList()]);
        else
            SetBezierMode(enable, (List<BezierPoint>?[]?)null);
    }

    public List<BezierPoint> GetBezierPoints(int channel)
        => _bezierPointsPerChannel[channel].Select(p => p.Clone()).ToList();

    public List<BezierPoint> GetBezierPoints()
        => GetBezierPoints(EditChannel);

    // ── Horizontal remapping ─────────────────────────────────────────────────

    private ushort[] GetAppliedRamp(int ch)
    {
        var ramp = _ramps[ch];
        if (_bottomSkewX <= 0 && _topSkewX >= 255) return ramp;

        double from  = _bottomSkewX;
        double range = _topSkewX - from;
        if (range <= 0) return ramp;

        ushort globalMin = ramp[0], globalMax = ramp[0];
        for (int i = 0; i < 256; i++)
        {
            if (ramp[i] <= globalMin) globalMin = ramp[i];
            if (ramp[i] > globalMax) globalMax = ramp[i];
        }
        int topAnchorIdx = 0;
        for (int i = 0; i < 256; i++)
        {
            if (ramp[i] >= globalMax) { topAnchorIdx = i; break; }
        }

        var applied = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            double t      = Math.Clamp((i - from) / range, 0.0, 1.0);
            int    srcIdx = (int)Math.Round(t * 255.0);
            applied[i] = ramp[Math.Clamp(srcIdx, 0, 255)];
        }

        for (int i = 0; i < 256; i++)
        {
            if (i < (int)Math.Round(from))
                applied[i] = globalMin;
            else if (i > (int)Math.Round(_topSkewX))
                applied[i] = globalMax;
        }

        return applied;
    }

    // Legacy: returns the applied ramp for the edit channel
    private ushort[] GetAppliedRamp() => GetAppliedRamp(EditChannel);

    private void BakeHorizontalSkew()
    {
        if (_bottomSkewX <= 0 && _topSkewX >= 255) return;

        // Bake all channels affected
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
                _ramps[ch] = GetAppliedRamp(ch);
        }
        else
        {
            _ramps[_activeChannel] = GetAppliedRamp(_activeChannel);
        }

        _topSkewX    = 255.0;
        _bottomSkewX = 0.0;

        _suppressSkewEvents = true;
        var editRamp = _ramps[EditChannel];
        LeftSkewSlider.Value   = editRamp[0];
        RightSkewSlider.Value  = editRamp[255];
        TopSkewSlider.Value    = 255;
        BottomSkewSlider.Value = 0;
        _suppressSkewEvents = false;
    }

    private void HSkew_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_isDrawMode) return;
        BakeHorizontalSkew();
        DrawCurve();
        FireDrawnRampChanged();
    }

    private void FireDrawnRampChanged()
    {
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
                DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs
                    { Channel = ch, Ramp = GetAppliedRamp(ch) });
        }
        else
        {
            DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs
                { Channel = _activeChannel, Ramp = GetAppliedRamp(_activeChannel) });
        }
    }

    private void TopSkewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDrawMode || _suppressSkewEvents) return;
        _topSkewX = Math.Max(e.NewValue, _bottomSkewX + 10);
        DrawCurve();
        FireDrawnRampChanged();
    }

    private void BottomSkewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDrawMode || _suppressSkewEvents) return;
        _bottomSkewX = Math.Min(e.NewValue, _topSkewX - 10);
        DrawCurve();
        FireDrawnRampChanged();
    }

    // ── Curve rendering ───────────────────────────────────────────────────────

    private static readonly SolidColorBrush[] ChannelBrushes =
    [
        new(Color.FromRgb(255, 80, 80)),   // Red
        new(Color.FromRgb(80, 220, 80)),   // Green
        new(Color.FromRgb(80, 140, 255)),  // Blue
    ];

    private static readonly SolidColorBrush[] ChannelBrushesDim =
    [
        new(Color.FromArgb(64, 255, 80, 80)),
        new(Color.FromArgb(64, 80, 220, 80)),
        new(Color.FromArgb(64, 80, 140, 255)),
    ];

    private void DrawCurve()
    {
        CurveCanvas.Children.Clear();

        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var gridBrush     = new SolidColorBrush(Color.FromRgb(40, 40, 40));
        var identityBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70));

        double pad = _isBezierMode ? BezierPadding : 0;
        double cLeft = pad, cTop = pad;
        double cW = w - 2 * pad, cH = h - 2 * pad;

        for (int i = 1; i <= 3; i++)
        {
            double xg = cLeft + cW * i / 4.0, yg = cTop + cH * i / 4.0;
            CurveCanvas.Children.Add(new Line { X1 = xg, Y1 = cTop, X2 = xg, Y2 = cTop + cH, Stroke = gridBrush, StrokeThickness = 1 });
            CurveCanvas.Children.Add(new Line { X1 = cLeft, Y1 = yg, X2 = cLeft + cW, Y2 = yg, Stroke = gridBrush, StrokeThickness = 1 });
        }

        if (_isBezierMode)
        {
            var borderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 55));
            CurveCanvas.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = cW, Height = cH,
                Stroke = borderBrush, StrokeThickness = 1,
                Fill = System.Windows.Media.Brushes.Transparent
            });
            Canvas.SetLeft(CurveCanvas.Children[^1], cLeft);
            Canvas.SetTop(CurveCanvas.Children[^1], cTop);
        }

        CurveCanvas.Children.Add(new Line
        {
            X1 = cLeft, Y1 = cTop + cH, X2 = cLeft + cW, Y2 = cTop,
            Stroke = identityBrush, StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        });

        // Check if all 3 ramps are identical
        bool allSame = RampsEqual(_ramps[0], _ramps[1]) && RampsEqual(_ramps[1], _ramps[2]);

        if (allSame && _activeChannel < 0)
        {
            // Single white/cyan/orange/green curve
            SolidColorBrush curveBrush;
            if (_isBezierMode) curveBrush = new SolidColorBrush(Color.FromRgb(0, 200, 80));
            else if (_isDrawMode) curveBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0));
            else curveBrush = new SolidColorBrush(Colors.Cyan);

            DrawChannelPolyline(cLeft, cTop, cW, cH, GetAppliedRamp(0), curveBrush, 1.5);
        }
        else
        {
            // Draw inactive channels first (dim, thin), then active on top
            int[] order;
            if (_activeChannel < 0)
                order = [0, 1, 2];
            else
            {
                var list = new List<int>();
                for (int ch = 0; ch < 3; ch++)
                    if (ch != _activeChannel) list.Add(ch);
                list.Add(_activeChannel);
                order = list.ToArray();
            }

            foreach (int ch in order)
            {
                bool isActive = _activeChannel < 0 || ch == _activeChannel;
                var brush = isActive ? ChannelBrushes[ch] : ChannelBrushesDim[ch];
                double thickness = isActive ? 1.5 : 1.0;
                DrawChannelPolyline(cLeft, cTop, cW, cH, GetAppliedRamp(ch), brush, thickness);
            }
        }

        // Ghost base ramp when horizontal skew is active
        if (_isDrawMode && (_bottomSkewX > 0 || _topSkewX < 255))
        {
            var ghostBrush = new SolidColorBrush(Color.FromArgb(60, 255, 165, 0));
            var ghostLine = new Polyline
            {
                Stroke = ghostBrush, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 3 },
                Points = new PointCollection()
            };
            var editRamp = _ramps[EditChannel];
            for (int i = 0; i < 256; i++)
            {
                double x = cLeft + cW * i / 255.0;
                double y = cTop + cH * (1.0 - editRamp[i] / 65280.0);
                ghostLine.Points.Add(new Point(x, y));
            }
            CurveCanvas.Children.Add(ghostLine);
        }

        if (_isBezierMode)
            DrawBezierOverlay(w, h);
    }

    private void DrawChannelPolyline(double cLeft, double cTop, double cW, double cH,
                                      ushort[] ramp, SolidColorBrush brush, double thickness)
    {
        var polyline = new Polyline { Stroke = brush, StrokeThickness = thickness, Points = new PointCollection() };
        for (int i = 0; i < 256; i++)
        {
            double x = cLeft + cW * i / 255.0;
            double y = cTop + cH * (1.0 - ramp[i] / 65280.0);
            polyline.Points.Add(new Point(x, y));
        }
        CurveCanvas.Children.Add(polyline);
    }

    private static bool RampsEqual(ushort[] a, ushort[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    private void DrawBezierOverlay(double w, double h)
    {
        var handleBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
        var handleLineBrush = new SolidColorBrush(Color.FromArgb(120, 200, 200, 200));
        int editCh = EditChannel;
        var anchorBrush = ChannelBrushes.Length > editCh ? ChannelBrushes[editCh] : new SolidColorBrush(Color.FromRgb(0, 200, 80));

        // In "All" mode, use green accent
        if (_activeChannel < 0)
            anchorBrush = new SolidColorBrush(Color.FromRgb(0, 200, 80));

        var pts = _bezierPointsPerChannel[editCh];
        foreach (var pt in pts)
        {
            double ax = NormToPixelX(pt.AnchorX, w);
            double ay = NormToPixelY(pt.AnchorY, h);

            if (pt.HandleInDX != 0 || pt.HandleInDY != 0)
            {
                double hx = NormToPixelX(pt.AnchorX + pt.HandleInDX, w);
                double hy = NormToPixelY(pt.AnchorY + pt.HandleInDY, h);
                CurveCanvas.Children.Add(new Line
                {
                    X1 = ax, Y1 = ay, X2 = hx, Y2 = hy,
                    Stroke = handleLineBrush, StrokeThickness = 1
                });
                var hCircle = new Ellipse { Width = 8, Height = 8, Fill = handleBrush };
                Canvas.SetLeft(hCircle, hx - 4);
                Canvas.SetTop(hCircle, hy - 4);
                CurveCanvas.Children.Add(hCircle);
            }

            if (pt.HandleOutDX != 0 || pt.HandleOutDY != 0)
            {
                double hx = NormToPixelX(pt.AnchorX + pt.HandleOutDX, w);
                double hy = NormToPixelY(pt.AnchorY + pt.HandleOutDY, h);
                CurveCanvas.Children.Add(new Line
                {
                    X1 = ax, Y1 = ay, X2 = hx, Y2 = hy,
                    Stroke = handleLineBrush, StrokeThickness = 1
                });
                var hCircle = new Ellipse { Width = 8, Height = 8, Fill = handleBrush };
                Canvas.SetLeft(hCircle, hx - 4);
                Canvas.SetTop(hCircle, hy - 4);
                CurveCanvas.Children.Add(hCircle);
            }

            var anchor = new Ellipse { Width = 10, Height = 10, Fill = anchorBrush };
            Canvas.SetLeft(anchor, ax - 5);
            Canvas.SetTop(anchor, ay - 5);
            CurveCanvas.Children.Add(anchor);
        }
    }

    // ── Vertical skew sliders ─────────────────────────────────────────────────

    private void LeftSkewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDrawMode || _suppressSkewEvents) return;

        double delta = e.NewValue - e.OldValue;
        if (delta == 0) return;

        void ApplyToChannel(int ch)
        {
            for (int i = 0; i < 256; i++)
            {
                double t = 1.0 - i / 255.0;
                _ramps[ch][i] = (ushort)Math.Clamp(_ramps[ch][i] + delta * t, 0, 65280);
            }
        }

        if (_activeChannel < 0)
            for (int ch = 0; ch < 3; ch++) ApplyToChannel(ch);
        else
            ApplyToChannel(_activeChannel);

        DrawCurve();
        FireDrawnRampChanged();
    }

    private void RightSkewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isDrawMode || _suppressSkewEvents) return;

        double delta = e.NewValue - e.OldValue;
        if (delta == 0) return;

        void ApplyToChannel(int ch)
        {
            for (int i = 0; i < 256; i++)
            {
                double t = i / 255.0;
                _ramps[ch][i] = (ushort)Math.Clamp(_ramps[ch][i] + delta * t, 0, 65280);
            }
        }

        if (_activeChannel < 0)
            for (int ch = 0; ch < 3; ch++) ApplyToChannel(ch);
        else
            ApplyToChannel(_activeChannel);

        DrawCurve();
        FireDrawnRampChanged();
    }

    // ── Freehand drawing ──────────────────────────────────────────────────────

    private void CurveCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_isBezierMode)
        {
            BezierMouseDown(e);
            return;
        }
        if (!_isDrawMode) return;
        _isDrawing = true;
        _lastDrawX = -1;
        PaintAt(e.GetPosition(CurveCanvas));
        CurveCanvas.CaptureMouse();
    }

    private void CurveCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isBezierMode)
        {
            BezierMouseMove(e);
            return;
        }
        if (!_isDrawMode || !_isDrawing) return;
        PaintAt(e.GetPosition(CurveCanvas));
    }

    private void CurveCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isBezierMode)
        {
            BezierMouseUp(e);
            return;
        }
        if (!_isDrawMode) return;
        _isDrawing = false;
        _lastDrawX = -1;
        CurveCanvas.ReleaseMouseCapture();

        _suppressSkewEvents = true;
        var editRamp = _ramps[EditChannel];
        LeftSkewSlider.Value  = editRamp[0];
        RightSkewSlider.Value = editRamp[255];
        _suppressSkewEvents = false;

        FireDrawnRampChanged();
    }

    private void PaintAt(Point pos)
    {
        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        int    curX = (int)Math.Clamp(pos.X / w * 255.0, 0, 255);
        ushort curY = (ushort)(Math.Clamp(1.0 - pos.Y / h, 0.0, 1.0) * 65280);

        void PaintChannel(int ch)
        {
            if (_lastDrawX >= 0 && _lastDrawX != curX)
            {
                int fromX = _lastDrawX, toX = curX;
                ushort fromY = _lastDrawY, toY = curY;
                if (fromX > toX) { (fromX, toX) = (toX, fromX); (fromY, toY) = (toY, fromY); }

                for (int ix = fromX; ix <= toX; ix++)
                {
                    double t = (double)(ix - fromX) / (toX - fromX);
                    _ramps[ch][ix] = (ushort)Math.Clamp(fromY + t * (toY - fromY), 0, 65280);
                }
            }
            else
            {
                _ramps[ch][curX] = curY;
            }
        }

        if (_activeChannel < 0)
            for (int ch = 0; ch < 3; ch++) PaintChannel(ch);
        else
            PaintChannel(_activeChannel);

        _lastDrawX = curX;
        _lastDrawY = curY;
        DrawCurve();
    }

    // ── Bezier mouse handling ────────────────────────────────────────────────

    private const double MaxHandleLength = 1.0;
    private const double HitRadius = 10.0;
    private const double BezierPadding = 30.0;

    private double NormToPixelX(double nx, double w) => BezierPadding + nx * (w - 2 * BezierPadding);
    private double NormToPixelY(double ny, double h) => (h - BezierPadding) - ny * (h - 2 * BezierPadding);
    private double PixelToNormX(double px, double w) => (px - BezierPadding) / (w - 2 * BezierPadding);
    private double PixelToNormY(double py, double h) => ((h - BezierPadding) - py) / (h - 2 * BezierPadding);

    // The bezier points used for interaction = the active edit channel's points
    private List<BezierPoint> ActiveBezierPoints => _bezierPointsPerChannel[EditChannel];

    private void BezierMouseDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(CurveCanvas);
        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var pts = ActiveBezierPoints;

        if (e.ChangedButton == MouseButton.Right)
        {
            for (int i = 1; i < pts.Count - 1; i++)
            {
                double ax = NormToPixelX(pts[i].AnchorX, w);
                double ay = NormToPixelY(pts[i].AnchorY, h);
                if (Distance(pos, new Point(ax, ay)) < HitRadius)
                {
                    pts.RemoveAt(i);
                    EvaluateAndNotify();
                    return;
                }
            }
            return;
        }

        if (e.ChangedButton != MouseButton.Left) return;

        for (int i = 0; i < pts.Count; i++)
        {
            var pt = pts[i];
            double ax = NormToPixelX(pt.AnchorX, w);
            double ay = NormToPixelY(pt.AnchorY, h);

            if (pt.HandleInDX != 0 || pt.HandleInDY != 0)
            {
                double hx = NormToPixelX(pt.AnchorX + pt.HandleInDX, w);
                double hy = NormToPixelY(pt.AnchorY + pt.HandleInDY, h);
                if (Distance(pos, new Point(hx, hy)) < HitRadius)
                {
                    _dragTarget = BezierDragTarget.HandleIn;
                    _dragPointIndex = i;
                    CurveCanvas.CaptureMouse();
                    return;
                }
            }

            if (pt.HandleOutDX != 0 || pt.HandleOutDY != 0)
            {
                double hx = NormToPixelX(pt.AnchorX + pt.HandleOutDX, w);
                double hy = NormToPixelY(pt.AnchorY + pt.HandleOutDY, h);
                if (Distance(pos, new Point(hx, hy)) < HitRadius)
                {
                    _dragTarget = BezierDragTarget.HandleOut;
                    _dragPointIndex = i;
                    CurveCanvas.CaptureMouse();
                    return;
                }
            }

            if (Distance(pos, new Point(ax, ay)) < HitRadius)
            {
                _dragTarget = BezierDragTarget.Anchor;
                _dragPointIndex = i;
                CurveCanvas.CaptureMouse();
                return;
            }
        }

        double newX = Math.Clamp(PixelToNormX(pos.X, w), 0.0, 1.0);
        double newY = Math.Clamp(PixelToNormY(pos.Y, h), 0.0, 1.0);

        int insertIdx = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            if (pts[i].AnchorX < newX)
                insertIdx = i + 1;
        }

        double handleLen = 0.1;
        if (insertIdx > 0 && insertIdx < pts.Count)
            handleLen = (pts[insertIdx].AnchorX - pts[insertIdx - 1].AnchorX) / 3.0;

        var newPt = new BezierPoint
        {
            AnchorX = newX, AnchorY = newY,
            HandleInDX = -handleLen, HandleInDY = 0,
            HandleOutDX = handleLen, HandleOutDY = 0,
        };

        pts.Insert(insertIdx, newPt);

        // In All mode, mirror the addition to other channels
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                if (ch == EditChannel) continue;
                _bezierPointsPerChannel[ch].Insert(insertIdx, newPt.Clone());
            }
        }

        EvaluateAndNotify();
    }

    private void BezierMouseMove(MouseEventArgs e)
    {
        if (_dragTarget == BezierDragTarget.None) return;

        var pos = e.GetPosition(CurveCanvas);
        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var pts = ActiveBezierPoints;
        var pt = pts[_dragPointIndex];
        double mx = PixelToNormX(pos.X, w);
        double my = PixelToNormY(pos.Y, h);

        bool isFirst = _dragPointIndex == 0;
        bool isLast  = _dragPointIndex == pts.Count - 1;

        switch (_dragTarget)
        {
            case BezierDragTarget.Anchor:
            {
                mx = Math.Clamp(mx, 0.0, 1.0);
                my = Math.Clamp(my, 0.0, 1.0);
                if (isFirst) mx = 0;
                else if (isLast) mx = 1;
                else
                {
                    double minX = pts[_dragPointIndex - 1].AnchorX + 0.001;
                    double maxX = pts[_dragPointIndex + 1].AnchorX - 0.001;
                    mx = Math.Clamp(mx, minX, maxX);
                }

                double dax = mx - pt.AnchorX;
                double day = my - pt.AnchorY;
                pt.AnchorX = mx;
                pt.AnchorY = my;

                // Mirror to other channels in All mode
                if (_activeChannel < 0)
                {
                    for (int ch = 0; ch < 3; ch++)
                    {
                        if (ch == EditChannel) continue;
                        var other = _bezierPointsPerChannel[ch][_dragPointIndex];
                        other.AnchorX = mx;
                        other.AnchorY = my;
                    }
                }
                break;
            }
            case BezierDragTarget.HandleIn:
            {
                double dx = mx - pt.AnchorX;
                double dy = my - pt.AnchorY;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > MaxHandleLength) { dx *= MaxHandleLength / len; dy *= MaxHandleLength / len; }
                pt.HandleInDX = dx;
                pt.HandleInDY = dy;
                if (_activeChannel < 0)
                {
                    for (int ch = 0; ch < 3; ch++)
                    {
                        if (ch == EditChannel) continue;
                        var other = _bezierPointsPerChannel[ch][_dragPointIndex];
                        other.HandleInDX = dx;
                        other.HandleInDY = dy;
                    }
                }
                break;
            }
            case BezierDragTarget.HandleOut:
            {
                double dx = mx - pt.AnchorX;
                double dy = my - pt.AnchorY;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > MaxHandleLength) { dx *= MaxHandleLength / len; dy *= MaxHandleLength / len; }
                pt.HandleOutDX = dx;
                pt.HandleOutDY = dy;
                if (_activeChannel < 0)
                {
                    for (int ch = 0; ch < 3; ch++)
                    {
                        if (ch == EditChannel) continue;
                        var other = _bezierPointsPerChannel[ch][_dragPointIndex];
                        other.HandleOutDX = dx;
                        other.HandleOutDY = dy;
                    }
                }
                break;
            }
        }

        // Re-evaluate affected channels
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
                _ramps[ch] = BezierEvaluator.Evaluate(_bezierPointsPerChannel[ch]);
        }
        else
        {
            _ramps[EditChannel] = BezierEvaluator.Evaluate(ActiveBezierPoints);
        }

        DrawCurve();

        // Fire drawn ramp changed for affected channels
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
                DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs { Channel = ch, Ramp = _ramps[ch] });
        }
        else
        {
            DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs { Channel = EditChannel, Ramp = _ramps[EditChannel] });
        }
    }

    private void BezierMouseUp(MouseButtonEventArgs e)
    {
        if (_dragTarget == BezierDragTarget.None) return;
        _dragTarget = BezierDragTarget.None;
        _dragPointIndex = -1;
        CurveCanvas.ReleaseMouseCapture();

        // Fire bezier points changed for affected channels
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
                BezierPointsChanged?.Invoke(this, new BezierPointsChangedEventArgs
                    { Channel = ch, Points = GetBezierPoints(ch) });
        }
        else
        {
            BezierPointsChanged?.Invoke(this, new BezierPointsChangedEventArgs
                { Channel = EditChannel, Points = GetBezierPoints(EditChannel) });
        }
    }

    private void EvaluateAndNotify()
    {
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                _ramps[ch] = BezierEvaluator.Evaluate(_bezierPointsPerChannel[ch]);
                DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs { Channel = ch, Ramp = _ramps[ch] });
                BezierPointsChanged?.Invoke(this, new BezierPointsChangedEventArgs
                    { Channel = ch, Points = GetBezierPoints(ch) });
            }
        }
        else
        {
            _ramps[EditChannel] = BezierEvaluator.Evaluate(ActiveBezierPoints);
            DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs { Channel = EditChannel, Ramp = _ramps[EditChannel] });
            BezierPointsChanged?.Invoke(this, new BezierPointsChangedEventArgs
                { Channel = EditChannel, Points = GetBezierPoints(EditChannel) });
        }
        DrawCurve();
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
