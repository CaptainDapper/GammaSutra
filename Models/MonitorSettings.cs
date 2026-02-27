using System.Text.Json.Serialization;

namespace GammaControl.Models;

[JsonConverter(typeof(MonitorSettingsConverter))]
public class MonitorSettings
{
    public string DeviceName { get; set; } = string.Empty;
    public int CurveMode { get; set; } = 0; // 0 = Normal, 1 = Draw, 2 = Bezier
    public double[] Gamma { get; set; } = [1.0, 1.0, 1.0];
    public double[] Brightness { get; set; } = [0.0, 0.0, 0.0];
    public double[] Contrast { get; set; } = [1.0, 1.0, 1.0];
    public double[] SCurve { get; set; } = [0.0, 0.0, 0.0];
    public double[] Highlights { get; set; } = [0.0, 0.0, 0.0];
    public double[] Shadows { get; set; } = [0.0, 0.0, 0.0];
    public bool[] UseDrawnCurve { get; set; } = [false, false, false];
    public ushort[]?[] DrawnRamp { get; set; } = [null, null, null];
    public List<BezierPoint>?[] BezierPoints { get; set; } = [null, null, null];
    public int[] PosterizeSteps { get; set; } = [0, 0, 0];
    public double[] PosterizeRangeMin { get; set; } = [0.0, 0.0, 0.0];
    public double[] PosterizeRangeMax { get; set; } = [1.0, 1.0, 1.0];
    public double[] PosterizeFeather { get; set; } = [0.1, 0.1, 0.1];
    public double[] PosterizeFeatherCurve { get; set; } = [1.0, 1.0, 1.0];

    public MonitorSettings Clone() => new()
    {
        DeviceName          = DeviceName,
        CurveMode           = CurveMode,
        Gamma               = (double[])Gamma.Clone(),
        Brightness          = (double[])Brightness.Clone(),
        Contrast            = (double[])Contrast.Clone(),
        SCurve              = (double[])SCurve.Clone(),
        Highlights          = (double[])Highlights.Clone(),
        Shadows             = (double[])Shadows.Clone(),
        UseDrawnCurve       = (bool[])UseDrawnCurve.Clone(),
        DrawnRamp           = [DrawnRamp[0] != null ? (ushort[])DrawnRamp[0]!.Clone() : null,
                               DrawnRamp[1] != null ? (ushort[])DrawnRamp[1]!.Clone() : null,
                               DrawnRamp[2] != null ? (ushort[])DrawnRamp[2]!.Clone() : null],
        BezierPoints        = [BezierPoints[0]?.Select(p => p.Clone()).ToList(),
                               BezierPoints[1]?.Select(p => p.Clone()).ToList(),
                               BezierPoints[2]?.Select(p => p.Clone()).ToList()],
        PosterizeSteps      = (int[])PosterizeSteps.Clone(),
        PosterizeRangeMin   = (double[])PosterizeRangeMin.Clone(),
        PosterizeRangeMax   = (double[])PosterizeRangeMax.Clone(),
        PosterizeFeather    = (double[])PosterizeFeather.Clone(),
        PosterizeFeatherCurve = (double[])PosterizeFeatherCurve.Clone(),
    };
}
