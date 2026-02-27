namespace GammaControl;

public static class GammaCalculator
{
    /// <summary>
    /// Builds a 256-entry gamma ramp.
    /// Max value is 0xFF00 (65280) — values must be MSB-aligned for SetDeviceGammaRamp.
    ///
    /// Pipeline (all in normalized 0–1 space):
    ///   1. Gamma      — power curve: pow(v, 1/gamma)
    ///   2. Contrast   — scale around midpoint: (v-0.5)*contrast+0.5
    ///   3. Brightness — additive offset
    ///   4. S-Curve    — sigmoid contrast: v += s * 4v(1-v)(v-0.5)
    ///   5. Highlights — quadratic bright lift/pull: v += h * v²
    ///   6. Shadows    — quadratic dark lift/pull:  v += s * (1-v)²
    /// </summary>
    public static ushort[] BuildRamp(double gamma, double brightness, double contrast,
        double sCurve = 0.0, double highlights = 0.0, double shadows = 0.0)
    {
        var ramp = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            double v = i / 255.0;
            v = Math.Pow(v, 1.0 / gamma);
            v = (v - 0.5) * contrast + 0.5;
            v += brightness;
            v += sCurve * 4.0 * v * (1.0 - v) * (v - 0.5);
            v += highlights * v * v;
            v += shadows * (1.0 - v) * (1.0 - v);
            v = Math.Clamp(v, 0.0, 1.0);
            ramp[i] = (ushort)(v * 65280);
        }
        return ramp;
    }

    public static NativeMethods.RAMP BuildFullRamp(double gamma, double brightness, double contrast,
        double sCurve = 0.0, double highlights = 0.0, double shadows = 0.0)
    {
        var channel = BuildRamp(gamma, brightness, contrast, sCurve, highlights, shadows);
        return new NativeMethods.RAMP
        {
            Red = channel,
            Green = (ushort[])channel.Clone(),
            Blue = (ushort[])channel.Clone()
        };
    }

    public static NativeMethods.RAMP BuildFullRampFromArray(ushort[] rawRamp)
    {
        return new NativeMethods.RAMP
        {
            Red = (ushort[])rawRamp.Clone(),
            Green = (ushort[])rawRamp.Clone(),
            Blue = (ushort[])rawRamp.Clone()
        };
    }

    public static ushort[] ApplyPosterize(ushort[] ramp, int steps, double rangeMin, double rangeMax,
        double feather, double featherCurve)
    {
        if (steps < 2) return ramp;

        var result = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            double t = i / 255.0;
            double original = ramp[i];

            // Compute blend factor (0 = smooth, 1 = fully posterized)
            double blend;
            double innerMin = rangeMin + feather;
            double innerMax = rangeMax - feather;

            if (t < rangeMin || t > rangeMax)
                blend = 0.0;
            else if (t >= innerMin && t <= innerMax)
                blend = 1.0;
            else if (t < innerMin)
            {
                double linearFade = feather > 0 ? (t - rangeMin) / feather : 1.0;
                blend = Math.Pow(Math.Clamp(linearFade, 0.0, 1.0), featherCurve);
            }
            else // t > innerMax
            {
                double linearFade = feather > 0 ? (rangeMax - t) / feather : 1.0;
                blend = Math.Pow(Math.Clamp(linearFade, 0.0, 1.0), featherCurve);
            }

            // Quantize
            double v = original / 65280.0;
            double q = Math.Round(v * (steps - 1)) / (steps - 1);
            double quantized = q * 65280.0;

            result[i] = (ushort)Math.Clamp(original + blend * (quantized - original), 0, 65280);
        }
        return result;
    }

    /// <summary>Returns a linear (identity) ramp — ramp[i] = i * 256, max = 65280.</summary>
    public static NativeMethods.RAMP BuildLinearRamp()
    {
        var channel = new ushort[256];
        for (int i = 0; i < 256; i++)
            channel[i] = (ushort)(i * 256);
        return new NativeMethods.RAMP
        {
            Red = channel,
            Green = (ushort[])channel.Clone(),
            Blue = (ushort[])channel.Clone()
        };
    }
}
