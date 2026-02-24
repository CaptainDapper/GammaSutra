using Microsoft.Win32;

namespace GammaControl;

public static class StartupHelper
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "GammaSutra";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) != null;
    }

    public static void SetEnabled(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key == null) return;

        if (enable)
        {
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (exe != null)
                key.SetValue(AppName, $"\"{exe}\" --minimized");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}
