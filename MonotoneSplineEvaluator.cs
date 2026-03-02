using GammaControl.Models;

namespace GammaControl;

public static class MonotoneSplineEvaluator
{
    /// <summary>
    /// Fritsch-Carlson monotone cubic interpolation of node points into a 256-entry gamma ramp.
    /// Points must be sorted by X. All coordinates normalized 0–1. Output values 0–65280.
    /// </summary>
    public static ushort[] Evaluate(List<NodePoint> points)
    {
        if (points.Count < 2)
            return DefaultRamp();

        int n = points.Count;
        double[] xs = new double[n];
        double[] ys = new double[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = points[i].X;
            ys[i] = points[i].Y;
        }

        // Compute slopes of secant lines
        double[] dx = new double[n - 1];
        double[] dy = new double[n - 1];
        double[] m = new double[n - 1]; // secant slopes
        for (int i = 0; i < n - 1; i++)
        {
            dx[i] = xs[i + 1] - xs[i];
            dy[i] = ys[i + 1] - ys[i];
            m[i] = dx[i] > 0 ? dy[i] / dx[i] : 0;
        }

        // Compute tangent slopes using Fritsch-Carlson method
        double[] tangents = new double[n];
        if (n == 2)
        {
            tangents[0] = m[0];
            tangents[1] = m[0];
        }
        else
        {
            // Initial tangents: average of adjacent secants
            tangents[0] = m[0];
            for (int i = 1; i < n - 1; i++)
                tangents[i] = (m[i - 1] + m[i]) / 2.0;
            tangents[n - 1] = m[n - 2];

            // Fritsch-Carlson monotonicity constraint
            for (int i = 0; i < n - 1; i++)
            {
                if (Math.Abs(m[i]) < 1e-12)
                {
                    tangents[i] = 0;
                    tangents[i + 1] = 0;
                }
                else
                {
                    double alpha = tangents[i] / m[i];
                    double beta = tangents[i + 1] / m[i];
                    double h = Math.Sqrt(alpha * alpha + beta * beta);
                    if (h > 3.0)
                    {
                        double tau = 3.0 / h;
                        tangents[i] = tau * alpha * m[i];
                        tangents[i + 1] = tau * beta * m[i];
                    }
                }
            }
        }

        // Evaluate the spline at 256 points
        var ramp = new ushort[256];
        int seg = 0;
        for (int i = 0; i < 256; i++)
        {
            double x = i / 255.0;

            // Clamp to endpoints
            if (x <= xs[0]) { ramp[i] = (ushort)(Math.Clamp(ys[0], 0, 1) * 65280); continue; }
            if (x >= xs[n - 1]) { ramp[i] = (ushort)(Math.Clamp(ys[n - 1], 0, 1) * 65280); continue; }

            // Find segment
            while (seg < n - 2 && x > xs[seg + 1]) seg++;

            double h2 = dx[seg];
            if (h2 <= 0) { ramp[i] = (ushort)(Math.Clamp(ys[seg], 0, 1) * 65280); continue; }

            double t = (x - xs[seg]) / h2;
            double t2 = t * t;
            double t3 = t2 * t;

            // Hermite basis functions
            double h00 = 2 * t3 - 3 * t2 + 1;
            double h10 = t3 - 2 * t2 + t;
            double h01 = -2 * t3 + 3 * t2;
            double h11 = t3 - t2;

            double y = h00 * ys[seg] + h10 * h2 * tangents[seg]
                     + h01 * ys[seg + 1] + h11 * h2 * tangents[seg + 1];

            ramp[i] = (ushort)(Math.Clamp(y, 0, 1) * 65280);
        }

        return ramp;
    }

    public static List<NodePoint> DefaultPoints() =>
    [
        new NodePoint { X = 0, Y = 0 },
        new NodePoint { X = 1, Y = 1 }
    ];

    private static ushort[] DefaultRamp()
    {
        var ramp = new ushort[256];
        for (int i = 0; i < 256; i++)
            ramp[i] = (ushort)(i * 256);
        return ramp;
    }
}
