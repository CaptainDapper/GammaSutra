using GammaControl.Models;

namespace GammaControl;

public static class BezierEvaluator
{
    /// <summary>
    /// Evaluates a list of bezier points into a 256-entry gamma ramp (ushort[256], values 0–65280).
    /// Points must be sorted by AnchorX. Handles are expressed as deltas from the anchor.
    /// All coordinates are normalized 0–1.
    /// </summary>
    public static ushort[] Evaluate(List<BezierPoint> points)
    {
        if (points.Count < 2)
            return DefaultRamp();

        // Sample all cubic segments densely, collecting (x, y) pairs
        var samples = new List<(double x, double y)>();
        for (int seg = 0; seg < points.Count - 1; seg++)
        {
            var p0 = points[seg];
            var p1 = points[seg + 1];

            // Control points for this cubic segment
            double x0 = p0.AnchorX;
            double y0 = p0.AnchorY;
            double x1 = p0.AnchorX + p0.HandleOutDX;
            double y1 = p0.AnchorY + p0.HandleOutDY;
            double x2 = p1.AnchorX + p1.HandleInDX;
            double y2 = p1.AnchorY + p1.HandleInDY;
            double x3 = p1.AnchorX;
            double y3 = p1.AnchorY;

            // Sample at high resolution
            const int steps = 512;
            for (int i = 0; i <= steps; i++)
            {
                double t = (double)i / steps;
                double oneMinusT = 1.0 - t;
                double a = oneMinusT * oneMinusT * oneMinusT;
                double b = 3.0 * oneMinusT * oneMinusT * t;
                double c = 3.0 * oneMinusT * t * t;
                double d = t * t * t;

                double sx = a * x0 + b * x1 + c * x2 + d * x3;
                double sy = a * y0 + b * y1 + c * y2 + d * y3;
                samples.Add((sx, sy));
            }
        }

        // Sort by x so we can interpolate
        samples.Sort((a, b) => a.x.CompareTo(b.x));

        // Interpolate into 256 output values
        var ramp = new ushort[256];
        int sampleIdx = 0;
        for (int i = 0; i < 256; i++)
        {
            double targetX = i / 255.0;

            // Advance to the sample just past targetX
            while (sampleIdx < samples.Count - 1 && samples[sampleIdx + 1].x < targetX)
                sampleIdx++;

            double y;
            if (sampleIdx >= samples.Count - 1)
            {
                y = samples[^1].y;
            }
            else if (sampleIdx == 0 && targetX <= samples[0].x)
            {
                y = samples[0].y;
            }
            else
            {
                var (x0, y0) = samples[sampleIdx];
                var (x1, y1) = samples[Math.Min(sampleIdx + 1, samples.Count - 1)];
                double span = x1 - x0;
                if (span <= 0)
                    y = y0;
                else
                    y = y0 + (y1 - y0) * ((targetX - x0) / span);
            }

            y = Math.Clamp(y, 0.0, 1.0);
            ramp[i] = (ushort)(y * 65280);
        }

        return ramp;
    }

    public static List<BezierPoint> DefaultPoints()
    {
        // Straight diagonal from (0,0) to (1,1) with 1/3-length handles
        return
        [
            new BezierPoint
            {
                AnchorX = 0, AnchorY = 0,
                HandleInDX = 0, HandleInDY = 0,
                HandleOutDX = 1.0 / 3.0, HandleOutDY = 1.0 / 3.0
            },
            new BezierPoint
            {
                AnchorX = 1, AnchorY = 1,
                HandleInDX = -1.0 / 3.0, HandleInDY = -1.0 / 3.0,
                HandleOutDX = 0, HandleOutDY = 0
            }
        ];
    }

    private static ushort[] DefaultRamp()
    {
        var ramp = new ushort[256];
        for (int i = 0; i < 256; i++)
            ramp[i] = (ushort)(i * 256);
        return ramp;
    }
}
