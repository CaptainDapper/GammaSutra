namespace GammaControl.Models;

public class NodePoint
{
    public double X { get; set; }
    public double Y { get; set; }

    public NodePoint Clone() => new() { X = X, Y = Y };
}
