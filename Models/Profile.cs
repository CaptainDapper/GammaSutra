using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaControl.Models;

[JsonConverter(typeof(ProfileConverter))]
public class Profile
{
    public string Name { get; set; } = "Default";
    public uint HotkeyModifiers { get; set; } = 0;
    public uint HotkeyVKey { get; set; } = 0;
    public MonitorSettings Settings { get; set; } = new();
}

/// <summary>
/// Handles both old format (MonitorSettings dict keyed by device name) and new format (single Settings object).
/// </summary>
public class ProfileConverter : JsonConverter<Profile>
{
    public override Profile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var profile = new Profile();

        if (root.TryGetProperty("Name", out var name))
            profile.Name = name.GetString() ?? "Default";
        if (root.TryGetProperty("HotkeyModifiers", out var hm))
            profile.HotkeyModifiers = hm.GetUInt32();
        if (root.TryGetProperty("HotkeyVKey", out var hv))
            profile.HotkeyVKey = hv.GetUInt32();

        // New format: "Settings" is a single MonitorSettings object
        if (root.TryGetProperty("Settings", out var settings))
        {
            profile.Settings = JsonSerializer.Deserialize<MonitorSettings>(settings.GetRawText(), options) ?? new();
        }
        // Old format: "MonitorSettings" is a dictionary keyed by device name
        else if (root.TryGetProperty("MonitorSettings", out var msDict) && msDict.ValueKind == JsonValueKind.Object)
        {
            // Pick the first value from the dictionary
            foreach (var entry in msDict.EnumerateObject())
            {
                profile.Settings = JsonSerializer.Deserialize<MonitorSettings>(entry.Value.GetRawText(), options) ?? new();
                break;
            }
        }

        return profile;
    }

    public override void Write(Utf8JsonWriter writer, Profile value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Name", value.Name);
        writer.WriteNumber("HotkeyModifiers", value.HotkeyModifiers);
        writer.WriteNumber("HotkeyVKey", value.HotkeyVKey);
        writer.WritePropertyName("Settings");
        JsonSerializer.Serialize(writer, value.Settings, options);
        writer.WriteEndObject();
    }
}
