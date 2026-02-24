using System.Windows.Interop;

namespace GammaControl;

public class HotkeyManager : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, Action> _hotkeys = new();
    private int _nextId = 1;
    private HwndSource? _hwndSource;

    public HotkeyManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
    }

    public int Register(uint modifiers, uint vKey, Action callback)
    {
        int id = _nextId++;
        if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers | NativeMethods.MOD_NOREPEAT, vKey))
        {
            _hotkeys[id] = callback;
            return id;
        }
        return -1;
    }

    public void Unregister(int id)
    {
        if (_hotkeys.Remove(id))
            NativeMethods.UnregisterHotKey(_hwnd, id);
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeys.Keys.ToList())
            NativeMethods.UnregisterHotKey(_hwnd, id);
        _hotkeys.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (_hotkeys.TryGetValue(id, out var action))
            {
                action();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
    }
}
