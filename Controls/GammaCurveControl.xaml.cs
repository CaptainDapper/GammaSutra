using System.Windows;
using System.Windows.Controls;
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

public class NodePointsChangedEventArgs : EventArgs
{
    public int Channel { get; init; }
    public List<NodePoint> Points { get; init; } = null!;
}

public partial class GammaCurveControl : UserControl
{
    private ushort[][] _ramps = [new ushort[256], new ushort[256], new ushort[256]];
    private int _activeChannel = -1; // -1 = All, 0=R, 1=G, 2=B

    // Zoom/Pan state
    private double _zoomLevel = 1.0;
    private Point _panOffset = new(0, 0);
    private bool _isPanning = false;
    private Point _panStart;
    private Point _panOffsetStart;
    private const double MinZoom = 1.0;
    private const double MaxZoom = 10.0;
    private const double ZoomFactor = 1.15;

    // Node mode state (per-channel)
    private bool _isNodeMode = false;
    private List<NodePoint>[] _nodePointsPerChannel =
    [
        MonotoneSplineEvaluator.DefaultPoints(),
        MonotoneSplineEvaluator.DefaultPoints(),
        MonotoneSplineEvaluator.DefaultPoints()
    ];
    private int _dragNodeIndex = -1;

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
    public event EventHandler<NodePointsChangedEventArgs>? NodePointsChanged;

    public int ActiveChannel
    {
        get => _activeChannel;
        set { _activeChannel = value; DrawCurve(); }
    }

    // The channel index used for node/bezier interaction
    private int EditChannel => _activeChannel < 0 ? 0 : _activeChannel;

    public GammaCurveControl()
    {
        InitializeComponent();

        for (int ch = 0; ch < 3; ch++)
            for (int i = 0; i < 256; i++)
                _ramps[ch][i] = (ushort)(i * 256);

        CurveCanvas.SizeChanged += (_, _) => DrawCurve();
        Loaded += (_, _) => DrawCurve();
    }

    // ── Zoom/Pan ──────────────────────────────────────────────────────────────

    private void ApplyCanvasTransform()
    {
        var tg = new TransformGroup();
        tg.Children.Add(new ScaleTransform(_zoomLevel, _zoomLevel));
        tg.Children.Add(new TranslateTransform(_panOffset.X, _panOffset.Y));
        CurveCanvas.RenderTransform = tg;

        ResetZoomButton.Visibility = _zoomLevel > 1.01 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Transforms a mouse position (in control space) to canvas pixel space accounting for zoom/pan.</summary>
    private Point MouseToCanvas(Point mousePos)
    {
        if (CurveCanvas.RenderTransform is TransformGroup tg)
        {
            var inverse = tg.Inverse;
            if (inverse != null)
                return inverse.Transform(mousePos);
        }
        return mousePos;
    }

    private void CurveCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mousePos = e.GetPosition(CurveCanvas);
        // mousePos is already in canvas space (WPF transforms it)
        // We need the position in parent space for zoom-toward-cursor
        var parentPos = e.GetPosition(this);

        double oldZoom = _zoomLevel;
        if (e.Delta > 0)
            _zoomLevel = Math.Min(_zoomLevel * ZoomFactor, MaxZoom);
        else
            _zoomLevel = Math.Max(_zoomLevel / ZoomFactor, MinZoom);

        // Adjust pan so the point under cursor stays fixed
        double ratio = _zoomLevel / oldZoom;
        _panOffset = new Point(
            parentPos.X - ratio * (parentPos.X - _panOffset.X),
            parentPos.Y - ratio * (parentPos.Y - _panOffset.Y));

        // Clamp pan when at min zoom
        if (_zoomLevel <= MinZoom + 0.01)
            _panOffset = new Point(0, 0);

        ApplyCanvasTransform();
        e.Handled = true;
    }

    private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
    {
        ResetZoom();
    }

    public void ResetZoom()
    {
        _zoomLevel = 1.0;
        _panOffset = new Point(0, 0);
        ApplyCanvasTransform();
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

    public void SetNodeMode(bool enable, List<NodePoint>?[]? pointsPerChannel = null)
    {
        _isNodeMode = enable;
        if (enable)
        {
            _isBezierMode = false;
            if (pointsPerChannel != null)
            {
                for (int ch = 0; ch < 3; ch++)
                {
                    if (pointsPerChannel[ch] != null)
                        _nodePointsPerChannel[ch] = pointsPerChannel[ch]!.Select(p => p.Clone()).ToList();
                    else
                        _nodePointsPerChannel[ch] = MonotoneSplineEvaluator.DefaultPoints();
                }
            }

            for (int ch = 0; ch < 3; ch++)
                _ramps[ch] = MonotoneSplineEvaluator.Evaluate(_nodePointsPerChannel[ch]);
        }
        DrawCurve();
    }

    public List<NodePoint> GetNodePoints(int channel)
        => _nodePointsPerChannel[channel].Select(p => p.Clone()).ToList();

    public List<NodePoint> GetNodePoints()
        => GetNodePoints(EditChannel);

    public void SetBezierMode(bool enable, List<BezierPoint>?[]? pointsPerChannel = null)
    {
        _isBezierMode = enable;
        if (enable)
        {
            _isNodeMode = false;
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

        double pad = (_isBezierMode || _isNodeMode) ? BezierPadding : 0;
        double cLeft = pad, cTop = pad;
        double cW = w - 2 * pad, cH = h - 2 * pad;

        for (int i = 1; i <= 3; i++)
        {
            double xg = cLeft + cW * i / 4.0, yg = cTop + cH * i / 4.0;
            CurveCanvas.Children.Add(new Line { X1 = xg, Y1 = cTop, X2 = xg, Y2 = cTop + cH, Stroke = gridBrush, StrokeThickness = 1 });
            CurveCanvas.Children.Add(new Line { X1 = cLeft, Y1 = yg, X2 = cLeft + cW, Y2 = yg, Stroke = gridBrush, StrokeThickness = 1 });
        }

        if (_isBezierMode || _isNodeMode)
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
            SolidColorBrush curveBrush;
            if (_isBezierMode) curveBrush = new SolidColorBrush(Color.FromRgb(0, 200, 80));
            else if (_isNodeMode) curveBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0));
            else curveBrush = new SolidColorBrush(Colors.Cyan);

            DrawChannelPolyline(cLeft, cTop, cW, cH, _ramps[0], curveBrush, 1.5);
        }
        else
        {
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
                DrawChannelPolyline(cLeft, cTop, cW, cH, _ramps[ch], brush, thickness);
            }
        }

        if (_isBezierMode)
            DrawBezierOverlay(w, h);

        if (_isNodeMode)
            DrawNodeOverlay(w, h);
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

    // ── Node overlay ──────────────────────────────────────────────────────────

    private void DrawNodeOverlay(double w, double h)
    {
        int editCh = EditChannel;
        var anchorBrush = ChannelBrushes.Length > editCh ? ChannelBrushes[editCh] : new SolidColorBrush(Color.FromRgb(255, 165, 0));
        if (_activeChannel < 0)
            anchorBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0));

        var pts = _nodePointsPerChannel[editCh];
        foreach (var pt in pts)
        {
            double px = NormToPixelX(pt.X, w);
            double py = NormToPixelY(pt.Y, h);

            var circle = new Ellipse { Width = 10, Height = 10, Fill = anchorBrush };
            Canvas.SetLeft(circle, px - 5);
            Canvas.SetTop(circle, py - 5);
            CurveCanvas.Children.Add(circle);
        }
    }

    // ── Node mouse handling ───────────────────────────────────────────────────

    private const double NodeHitRadius = 10.0;

    private void NodeMouseDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(CurveCanvas);
        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var pts = _nodePointsPerChannel[EditChannel];

        if (e.ChangedButton == MouseButton.Right)
        {
            // Right-click: delete node (not first/last)
            for (int i = 1; i < pts.Count - 1; i++)
            {
                double px = NormToPixelX(pts[i].X, w);
                double py = NormToPixelY(pts[i].Y, h);
                if (Distance(pos, new Point(px, py)) < NodeHitRadius)
                {
                    pts.RemoveAt(i);
                    if (_activeChannel < 0)
                    {
                        for (int ch = 0; ch < 3; ch++)
                            if (ch != EditChannel)
                                _nodePointsPerChannel[ch].RemoveAt(i);
                    }
                    NodeEvaluateAndNotify();
                    return;
                }
            }
            return;
        }

        if (e.ChangedButton != MouseButton.Left) return;

        // Left-click: hit test existing nodes
        for (int i = 0; i < pts.Count; i++)
        {
            double px = NormToPixelX(pts[i].X, w);
            double py = NormToPixelY(pts[i].Y, h);
            if (Distance(pos, new Point(px, py)) < NodeHitRadius)
            {
                _dragNodeIndex = i;
                CurveCanvas.CaptureMouse();
                return;
            }
        }

        // Left-click empty: add new node
        double newX = Math.Clamp(PixelToNormX(pos.X, w), 0.0, 1.0);
        double newY = Math.Clamp(PixelToNormY(pos.Y, h), 0.0, 1.0);

        int insertIdx = 0;
        for (int i = 0; i < pts.Count; i++)
            if (pts[i].X < newX) insertIdx = i + 1;

        var newPt = new NodePoint { X = newX, Y = newY };
        pts.Insert(insertIdx, newPt);

        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
                if (ch != EditChannel)
                    _nodePointsPerChannel[ch].Insert(insertIdx, newPt.Clone());
        }

        _dragNodeIndex = insertIdx;
        CurveCanvas.CaptureMouse();
        NodeEvaluateAndNotify();
    }

    private void NodeMouseMove(MouseEventArgs e)
    {
        if (_dragNodeIndex < 0) return;

        var pos = e.GetPosition(CurveCanvas);
        double w = CurveCanvas.ActualWidth;
        double h = CurveCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var pts = _nodePointsPerChannel[EditChannel];
        var pt = pts[_dragNodeIndex];

        double mx = Math.Clamp(PixelToNormX(pos.X, w), 0.0, 1.0);
        double my = Math.Clamp(PixelToNormY(pos.Y, h), 0.0, 1.0);

        bool isFirst = _dragNodeIndex == 0;
        bool isLast = _dragNodeIndex == pts.Count - 1;

        if (isFirst) mx = 0;
        else if (isLast) mx = 1;
        else
        {
            double minX = pts[_dragNodeIndex - 1].X + 0.001;
            double maxX = pts[_dragNodeIndex + 1].X - 0.001;
            mx = Math.Clamp(mx, minX, maxX);
        }

        pt.X = mx;
        pt.Y = my;

        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                if (ch == EditChannel) continue;
                var other = _nodePointsPerChannel[ch][_dragNodeIndex];
                other.X = mx;
                other.Y = my;
            }
        }

        // Re-evaluate
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                _ramps[ch] = MonotoneSplineEvaluator.Evaluate(_nodePointsPerChannel[ch]);
                DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs { Channel = ch, Ramp = _ramps[ch] });
            }
        }
        else
        {
            _ramps[EditChannel] = MonotoneSplineEvaluator.Evaluate(pts);
            DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs { Channel = EditChannel, Ramp = _ramps[EditChannel] });
        }

        DrawCurve();
    }

    private void NodeMouseUp(MouseButtonEventArgs e)
    {
        if (_dragNodeIndex < 0) return;
        _dragNodeIndex = -1;
        CurveCanvas.ReleaseMouseCapture();

        // Fire node points changed
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
                NodePointsChanged?.Invoke(this, new NodePointsChangedEventArgs
                    { Channel = ch, Points = GetNodePoints(ch) });
        }
        else
        {
            NodePointsChanged?.Invoke(this, new NodePointsChangedEventArgs
                { Channel = EditChannel, Points = GetNodePoints(EditChannel) });
        }
    }

    private void NodeEvaluateAndNotify()
    {
        if (_activeChannel < 0)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                _ramps[ch] = MonotoneSplineEvaluator.Evaluate(_nodePointsPerChannel[ch]);
                DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs { Channel = ch, Ramp = _ramps[ch] });
                NodePointsChanged?.Invoke(this, new NodePointsChangedEventArgs
                    { Channel = ch, Points = GetNodePoints(ch) });
            }
        }
        else
        {
            _ramps[EditChannel] = MonotoneSplineEvaluator.Evaluate(_nodePointsPerChannel[EditChannel]);
            DrawnRampChanged?.Invoke(this, new DrawnRampChangedEventArgs { Channel = EditChannel, Ramp = _ramps[EditChannel] });
            NodePointsChanged?.Invoke(this, new NodePointsChangedEventArgs
                { Channel = EditChannel, Points = GetNodePoints(EditChannel) });
        }
        DrawCurve();
    }

    // ── Bezier overlay ────────────────────────────────────────────────────────

    private void DrawBezierOverlay(double w, double h)
    {
        var handleBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200));
        var handleLineBrush = new SolidColorBrush(Color.FromArgb(120, 200, 200, 200));
        int editCh = EditChannel;
        var anchorBrush = ChannelBrushes.Length > editCh ? ChannelBrushes[editCh] : new SolidColorBrush(Color.FromRgb(0, 200, 80));

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

    // ── Mouse dispatch ────────────────────────────────────────────────────────

    private void CurveCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            _isPanning = true;
            _panStart = e.GetPosition(this);
            _panOffsetStart = _panOffset;
            CurveCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }
        if (_isBezierMode) { BezierMouseDown(e); return; }
        if (_isNodeMode) { NodeMouseDown(e); return; }
    }

    private void CurveCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            var pos = e.GetPosition(this);
            _panOffset = new Point(
                _panOffsetStart.X + (pos.X - _panStart.X),
                _panOffsetStart.Y + (pos.Y - _panStart.Y));
            ApplyCanvasTransform();
            e.Handled = true;
            return;
        }
        if (_isBezierMode) { BezierMouseMove(e); return; }
        if (_isNodeMode) { NodeMouseMove(e); return; }
    }

    private void CurveCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && _isPanning)
        {
            _isPanning = false;
            CurveCanvas.ReleaseMouseCapture();
            e.Handled = true;
            return;
        }
        if (_isBezierMode) { BezierMouseUp(e); return; }
        if (_isNodeMode) { NodeMouseUp(e); return; }
    }

    // ── Bezier mouse handling ────────────────────────────────────────────────

    private const double MaxHandleLength = 1.0;
    private const double HitRadius = 10.0;
    private const double BezierPadding = 30.0;

    private double NormToPixelX(double nx, double w) => BezierPadding + nx * (w - 2 * BezierPadding);
    private double NormToPixelY(double ny, double h) => (h - BezierPadding) - ny * (h - 2 * BezierPadding);
    private double PixelToNormX(double px, double w) => (px - BezierPadding) / (w - 2 * BezierPadding);
    private double PixelToNormY(double py, double h) => ((h - BezierPadding) - py) / (h - 2 * BezierPadding);

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

                pt.AnchorX = mx;
                pt.AnchorY = my;

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
