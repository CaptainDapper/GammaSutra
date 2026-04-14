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
    private NodePoint[]? _dragOriginalPositions; // snapshot of all channels at drag start

    public event EventHandler<DrawnRampChangedEventArgs>? DrawnRampChanged;
    public event EventHandler<NodePointsChangedEventArgs>? NodePointsChanged;

    public int ActiveChannel
    {
        get => _activeChannel;
        set { _activeChannel = value; DrawCurve(); }
    }

    // The channel index used for node interaction
    private int EditChannel => _activeChannel < 0 ? 0 : _activeChannel;

    public GammaCurveControl()
    {
        InitializeComponent();

        for (int ch = 0; ch < 3; ch++)
            for (int i = 0; i < 256; i++)
                _ramps[ch][i] = (ushort)(i * 256);

        CurveCanvas.SizeChanged += (_, _) => DrawCurve();
        CurveCanvas.LostMouseCapture += CurveCanvas_LostMouseCapture;
        Loaded += (_, _) => DrawCurve();
    }

    // ── Zoom/Pan ──────────────────────────────────────────────────────────────

    private void UpdateResetZoomVisibility()
    {
        ResetZoomButton.Visibility = _zoomLevel > 1.01 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CurveCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Mouse position in canvas pixel space (pre-zoom)
        var pos = e.GetPosition(CurveCanvas);

        double oldZoom = _zoomLevel;
        if (e.Delta > 0)
            _zoomLevel = Math.Min(_zoomLevel * ZoomFactor, MaxZoom);
        else
            _zoomLevel = Math.Max(_zoomLevel / ZoomFactor, MinZoom);

        // Adjust pan so the point under cursor stays fixed
        // pos in canvas space maps to a norm coordinate; we want that same norm
        // coordinate to map back to the same pixel after zoom change.
        double ratio = _zoomLevel / oldZoom;
        _panOffset = new Point(
            pos.X - ratio * (pos.X - _panOffset.X),
            pos.Y - ratio * (pos.Y - _panOffset.Y));

        // Clamp pan when at min zoom
        if (_zoomLevel <= MinZoom + 0.01)
            _panOffset = new Point(0, 0);

        UpdateResetZoomVisibility();
        DrawCurve();
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
        UpdateResetZoomVisibility();
        DrawCurve();
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

    // ── Coordinate helpers (zoom/pan-aware) ──────────────────────────────────

    private const double HitRadius = 10.0;
    private const double CurvePadding = 30.0;

    // Normalized (0-1) → screen pixel, with zoom and pan applied
    private double NormToScreenX(double nx, double w)
    {
        double baseX = CurvePadding + nx * (w - 2 * CurvePadding);
        return baseX * _zoomLevel + _panOffset.X;
    }

    private double NormToScreenY(double ny, double h)
    {
        double baseY = (h - CurvePadding) - ny * (h - 2 * CurvePadding);
        return baseY * _zoomLevel + _panOffset.Y;
    }

    // Screen pixel → normalized (0-1), reversing zoom and pan
    private double ScreenToNormX(double sx, double w)
    {
        double baseX = (sx - _panOffset.X) / _zoomLevel;
        return (baseX - CurvePadding) / (w - 2 * CurvePadding);
    }

    private double ScreenToNormY(double sy, double h)
    {
        double baseY = (sy - _panOffset.Y) / _zoomLevel;
        return ((h - CurvePadding) - baseY) / (h - 2 * CurvePadding);
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

        // Grid lines (quarter marks)
        for (int i = 0; i <= 4; i++)
        {
            double nx = i / 4.0;
            double ny = i / 4.0;
            double sx = NormToScreenX(nx, w);
            double sy = NormToScreenY(ny, h);

            if (i > 0 && i < 4)
            {
                // Vertical grid line — full height of visible area
                CurveCanvas.Children.Add(new Line { X1 = sx, Y1 = NormToScreenY(1, h), X2 = sx, Y2 = NormToScreenY(0, h), Stroke = gridBrush, StrokeThickness = 1 });
                // Horizontal grid line — full width of visible area
                CurveCanvas.Children.Add(new Line { X1 = NormToScreenX(0, w), Y1 = sy, X2 = NormToScreenX(1, w), Y2 = sy, Stroke = gridBrush, StrokeThickness = 1 });
            }
        }

        // Border rectangle
        {
            double x0 = NormToScreenX(0, w), y0 = NormToScreenY(1, h);
            double x1 = NormToScreenX(1, w), y1 = NormToScreenY(0, h);
            var borderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 55));
            CurveCanvas.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = x1 - x0, Height = y1 - y0,
                Stroke = borderBrush, StrokeThickness = 1,
                Fill = System.Windows.Media.Brushes.Transparent
            });
            Canvas.SetLeft(CurveCanvas.Children[^1], x0);
            Canvas.SetTop(CurveCanvas.Children[^1], y0);
        }

        // Identity line (diagonal)
        CurveCanvas.Children.Add(new Line
        {
            X1 = NormToScreenX(0, w), Y1 = NormToScreenY(0, h),
            X2 = NormToScreenX(1, w), Y2 = NormToScreenY(1, h),
            Stroke = identityBrush, StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        });

        // Check if all 3 ramps are identical
        bool allSame = RampsEqual(_ramps[0], _ramps[1]) && RampsEqual(_ramps[1], _ramps[2]);

        if (allSame && _activeChannel < 0)
        {
            SolidColorBrush curveBrush;
            if (_isNodeMode) curveBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0));
            else curveBrush = new SolidColorBrush(Colors.Cyan);

            DrawChannelPolyline(w, h, _ramps[0], curveBrush, 1.5);
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
                DrawChannelPolyline(w, h, _ramps[ch], brush, thickness);
            }
        }

        if (_isNodeMode)
            DrawNodeOverlay(w, h);
    }

    private void DrawChannelPolyline(double w, double h,
                                      ushort[] ramp, SolidColorBrush brush, double thickness)
    {
        var polyline = new Polyline { Stroke = brush, StrokeThickness = thickness, Points = new PointCollection() };
        for (int i = 0; i < 256; i++)
        {
            double nx = i / 255.0;
            double ny = ramp[i] / 65280.0;
            polyline.Points.Add(new Point(NormToScreenX(nx, w), NormToScreenY(ny, h)));
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
            double px = NormToScreenX(pt.X, w);
            double py = NormToScreenY(pt.Y, h);

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
                double px = NormToScreenX(pts[i].X, w);
                double py = NormToScreenY(pts[i].Y, h);
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
            double px = NormToScreenX(pts[i].X, w);
            double py = NormToScreenY(pts[i].Y, h);
            if (Distance(pos, new Point(px, py)) < NodeHitRadius)
            {
                _dragNodeIndex = i;
                SnapshotDragOrigin();
                CurveCanvas.CaptureMouse();
                return;
            }
        }

        // Left-click empty: add new node
        double newX = Math.Clamp(ScreenToNormX(pos.X, w), 0.0, 1.0);
        double newY = Math.Clamp(ScreenToNormY(pos.Y, h), 0.0, 1.0);

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
        SnapshotDragOrigin();
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

        double mx = Math.Clamp(ScreenToNormX(pos.X, w), 0.0, 1.0);
        double my = Math.Clamp(ScreenToNormY(pos.Y, h), 0.0, 1.0);

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
        _dragOriginalPositions = null;
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

    private void SnapshotDragOrigin()
    {
        _dragOriginalPositions = new NodePoint[3];
        for (int ch = 0; ch < 3; ch++)
            _dragOriginalPositions[ch] = _nodePointsPerChannel[ch][_dragNodeIndex].Clone();
    }

    private void CancelNodeDrag()
    {
        if (_dragNodeIndex < 0 || _dragOriginalPositions == null) return;

        int idx = _dragNodeIndex;
        var orig = _dragOriginalPositions;
        _dragNodeIndex = -1;
        _dragOriginalPositions = null;

        // Restore original positions
        for (int ch = 0; ch < 3; ch++)
        {
            var pt = _nodePointsPerChannel[ch][idx];
            pt.X = orig[ch].X;
            pt.Y = orig[ch].Y;
        }

        CurveCanvas.ReleaseMouseCapture();

        // Re-evaluate with restored positions
        NodeEvaluateAndNotify();
    }

    private void CurveCanvas_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_dragNodeIndex >= 0)
            CancelNodeDrag();
        _isPanning = false;
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _dragNodeIndex >= 0)
        {
            CancelNodeDrag();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    // ── Mouse dispatch ────────────────────────────────────────────────────────

    private void CurveCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle)
        {
            _isPanning = true;
            _panStart = e.GetPosition(CurveCanvas);
            _panOffsetStart = _panOffset;
            CurveCanvas.CaptureMouse();
            e.Handled = true;
            return;
        }
        if (_isNodeMode) { NodeMouseDown(e); return; }
    }

    private void CurveCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            var pos = e.GetPosition(CurveCanvas);
            _panOffset = new Point(
                _panOffsetStart.X + (pos.X - _panStart.X),
                _panOffsetStart.Y + (pos.Y - _panStart.Y));
            DrawCurve();
            e.Handled = true;
            return;
        }
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
        if (_isNodeMode) { NodeMouseUp(e); return; }
    }

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
