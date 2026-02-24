namespace GammaControl.Models;

public class Profile
{
    public string Name { get; set; } = "Default";
    public uint HotkeyModifiers { get; set; } = 0;
    public uint HotkeyVKey { get; set; } = 0;
    public Dictionary<string, MonitorSettings> MonitorSettings { get; set; } = new();
}
