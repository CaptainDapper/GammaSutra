namespace GammaControl.Models;

public class BezierPoint
{
    public double AnchorX { get; set; }
    public double AnchorY { get; set; }
    public double HandleInDX { get; set; }
    public double HandleInDY { get; set; }
    public double HandleOutDX { get; set; }
    public double HandleOutDY { get; set; }

    public BezierPoint Clone() => new()
    {
        AnchorX      = AnchorX,
        AnchorY      = AnchorY,
        HandleInDX   = HandleInDX,
        HandleInDY   = HandleInDY,
        HandleOutDX  = HandleOutDX,
        HandleOutDY  = HandleOutDY,
    };
}
