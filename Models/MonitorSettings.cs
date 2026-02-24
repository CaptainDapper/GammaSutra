namespace GammaControl.Models;

public class MonitorSettings
{
    public string DeviceName { get; set; } = string.Empty;
    public double Gamma { get; set; } = 1.0;
    public double Brightness { get; set; } = 0.0;
    public double Contrast { get; set; } = 1.0;
    public double SCurve { get; set; } = 0.0;
    public double Highlights { get; set; } = 0.0;
    public double Shadows { get; set; } = 0.0;
    public bool UseDrawnCurve { get; set; } = false;
    public ushort[]? DrawnRamp { get; set; } = null;

    public MonitorSettings Clone() => new()
    {
        DeviceName    = DeviceName,
        Gamma         = Gamma,
        Brightness    = Brightness,
        Contrast      = Contrast,
        SCurve        = SCurve,
        Highlights    = Highlights,
        Shadows       = Shadows,
        UseDrawnCurve = UseDrawnCurve,
        DrawnRamp     = DrawnRamp != null ? (ushort[])DrawnRamp.Clone() : null,
    };
}
