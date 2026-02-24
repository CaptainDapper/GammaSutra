using System.Windows.Input;

namespace GammaControl;

public static class HotkeyHelper
{
    public static string Format(uint mods, uint vkey)
    {
        if (vkey == 0) return "(none)";

        var parts = new List<string>();
        if ((mods & NativeMethods.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((mods & NativeMethods.MOD_ALT)     != 0) parts.Add("Alt");
        if ((mods & NativeMethods.MOD_SHIFT)   != 0) parts.Add("Shift");
        if ((mods & NativeMethods.MOD_WIN)     != 0) parts.Add("Win");

        var key     = KeyInterop.KeyFromVirtualKey((int)vkey);
        var keyName = key.ToString();

        // D1–D9 → 1–9 for readability
        if (keyName.Length == 2 && keyName[0] == 'D' && char.IsDigit(keyName[1]))
            keyName = keyName[1..];

        parts.Add(keyName);
        return string.Join("+", parts);
    }
}
