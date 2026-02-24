using System.Windows.Forms;

namespace GammaControl;

public static class MonitorService
{
    public static Screen[] GetAllScreens() => Screen.AllScreens;

    public static bool ApplyRamp(string deviceName, NativeMethods.RAMP ramp)
    {
        IntPtr hdc = NativeMethods.CreateDC(null, deviceName, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero)
            return false;
        try
        {
            return NativeMethods.SetDeviceGammaRamp(hdc, ref ramp);
        }
        finally
        {
            NativeMethods.DeleteDC(hdc);
        }
    }

    public static void ResetAll()
    {
        var linear = GammaCalculator.BuildLinearRamp();
        foreach (var screen in Screen.AllScreens)
            ApplyRamp(screen.DeviceName, linear);
    }
}
