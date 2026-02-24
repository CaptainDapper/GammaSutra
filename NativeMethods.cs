using System.Runtime.InteropServices;

namespace GammaControl;

public static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RAMP
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool SetDeviceGammaRamp(IntPtr hDC, ref RAMP ramp);

    [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CreateDC(string? lpszDriver, string? lpszDevice, string? lpszOutput, IntPtr lpInitData);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int WM_HOTKEY = 0x0312;

    [DllImport("gdi32.dll")]  public static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
}
