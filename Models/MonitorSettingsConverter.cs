using System.Text.Json;
using System.Text.Json.Serialization;

namespace GammaControl.Models;

public class MonitorSettingsConverter : JsonConverter<MonitorSettings>
{
    public override MonitorSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = new MonitorSettings();
        if (reader.TokenType != JsonTokenType.StartObject) return s;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = reader.GetString()!;
            reader.Read();

            switch (prop)
            {
                case "DeviceName":    s.DeviceName = reader.GetString() ?? string.Empty; break;
                case "CurveMode":
                    s.CurveMode = reader.GetInt32();
                    // Treat legacy Bezier (mode 2) as Normal
                    if (s.CurveMode == 2) s.CurveMode = 0;
                    break;
                case "Gamma":         s.Gamma = ReadDoubleArrayOrScalar(ref reader, 1.0); break;
                case "Brightness":    s.Brightness = ReadDoubleArrayOrScalar(ref reader, 0.0); break;
                case "Contrast":      s.Contrast = ReadDoubleArrayOrScalar(ref reader, 1.0); break;
                case "SCurve":        s.SCurve = ReadDoubleArrayOrScalar(ref reader, 0.0); break;
                case "Highlights":    s.Highlights = ReadDoubleArrayOrScalar(ref reader, 0.0); break;
                case "Shadows":       s.Shadows = ReadDoubleArrayOrScalar(ref reader, 0.0); break;
                case "UseDrawnCurve": s.UseDrawnCurve = ReadBoolArrayOrScalar(ref reader); break;
                case "DrawnRamp":     s.DrawnRamp = ReadUshortArrayArrayOrSingle(ref reader); break;
                case "BezierPoints":  reader.Skip(); break; // Legacy field, ignored
                case "NodePoints":   s.NodePoints = ReadNodePointsArray(ref reader, options); break;
                case "PosterizeSteps":       s.PosterizeSteps = ReadIntArrayOrScalar(ref reader, 0); break;
                case "PosterizeRangeMin":    s.PosterizeRangeMin = ReadDoubleArrayOrScalar(ref reader, 0.0); break;
                case "PosterizeRangeMax":    s.PosterizeRangeMax = ReadDoubleArrayOrScalar(ref reader, 1.0); break;
                case "PosterizeFeather":     s.PosterizeFeather = ReadDoubleArrayOrScalar(ref reader, 0.1); break;
                case "PosterizeFeatherCurve": s.PosterizeFeatherCurve = ReadDoubleArrayOrScalar(ref reader, 1.0); break;
                default: reader.Skip(); break;
            }
        }
        return s;
    }

    public override void Write(Utf8JsonWriter writer, MonitorSettings value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("DeviceName", value.DeviceName);
        writer.WriteNumber("CurveMode", value.CurveMode);
        WriteDoubleArray(writer, "Gamma", value.Gamma);
        WriteDoubleArray(writer, "Brightness", value.Brightness);
        WriteDoubleArray(writer, "Contrast", value.Contrast);
        WriteDoubleArray(writer, "SCurve", value.SCurve);
        WriteDoubleArray(writer, "Highlights", value.Highlights);
        WriteDoubleArray(writer, "Shadows", value.Shadows);
        WriteBoolArray(writer, "UseDrawnCurve", value.UseDrawnCurve);
        WriteIntArray(writer, "PosterizeSteps", value.PosterizeSteps);
        WriteDoubleArray(writer, "PosterizeRangeMin", value.PosterizeRangeMin);
        WriteDoubleArray(writer, "PosterizeRangeMax", value.PosterizeRangeMax);
        WriteDoubleArray(writer, "PosterizeFeather", value.PosterizeFeather);
        WriteDoubleArray(writer, "PosterizeFeatherCurve", value.PosterizeFeatherCurve);

        // DrawnRamp
        writer.WritePropertyName("DrawnRamp");
        writer.WriteStartArray();
        for (int ch = 0; ch < 3; ch++)
        {
            if (value.DrawnRamp[ch] == null)
                writer.WriteNullValue();
            else
            {
                writer.WriteStartArray();
                foreach (var v in value.DrawnRamp[ch]!)
                    writer.WriteNumberValue(v);
                writer.WriteEndArray();
            }
        }
        writer.WriteEndArray();

        // NodePoints
        writer.WritePropertyName("NodePoints");
        writer.WriteStartArray();
        for (int ch = 0; ch < 3; ch++)
        {
            if (value.NodePoints[ch] == null)
                writer.WriteNullValue();
            else
                JsonSerializer.Serialize(writer, value.NodePoints[ch], options);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    // --- Helpers ---

    private static double[] ReadDoubleArrayOrScalar(ref Utf8JsonReader reader, double def)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<double>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                list.Add(reader.GetDouble());
            return list.Count >= 3 ? [list[0], list[1], list[2]] : [def, def, def];
        }
        var v = reader.GetDouble();
        return [v, v, v];
    }

    private static int[] ReadIntArrayOrScalar(ref Utf8JsonReader reader, int def)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<int>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                list.Add(reader.GetInt32());
            return list.Count >= 3 ? [list[0], list[1], list[2]] : [def, def, def];
        }
        var v = reader.GetInt32();
        return [v, v, v];
    }

    private static bool[] ReadBoolArrayOrScalar(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<bool>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                list.Add(reader.GetBoolean());
            return list.Count >= 3 ? [list[0], list[1], list[2]] : [false, false, false];
        }
        var v = reader.GetBoolean();
        return [v, v, v];
    }

    private static ushort[]?[] ReadUshortArrayArrayOrSingle(ref Utf8JsonReader reader)
    {
        if (reader.TokenType == JsonTokenType.Null) return [null, null, null];

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read();
            // Peek: if the first element is a number, this is a single flat ushort[] (old format)
            if (reader.TokenType == JsonTokenType.Number)
            {
                var flat = new List<ushort> { (ushort)reader.GetInt32() };
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    flat.Add((ushort)reader.GetInt32());
                var arr = flat.ToArray();
                return [arr, (ushort[])arr.Clone(), (ushort[])arr.Clone()];
            }

            // New format: array of 3 elements (each null or ushort[])
            var result = new ushort[]?[3];
            for (int ch = 0; ch < 3; ch++)
            {
                if (reader.TokenType == JsonTokenType.Null)
                    result[ch] = null;
                else if (reader.TokenType == JsonTokenType.StartArray)
                {
                    var list = new List<ushort>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        list.Add((ushort)reader.GetInt32());
                    result[ch] = list.ToArray();
                }
                if (ch < 2) reader.Read();
            }
            // Read past EndArray
            reader.Read();
            return result;
        }

        return [null, null, null];
    }

    private static List<NodePoint>?[] ReadNodePointsArray(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return [null, null, null];

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var arr = doc.RootElement;

            if (arr.GetArrayLength() == 0) return [null, null, null];

            var result = new List<NodePoint>?[3];
            for (int ch = 0; ch < 3 && ch < arr.GetArrayLength(); ch++)
            {
                if (arr[ch].ValueKind == JsonValueKind.Null)
                    result[ch] = null;
                else
                    result[ch] = JsonSerializer.Deserialize<List<NodePoint>>(arr[ch].GetRawText(), options);
            }
            return result;
        }

        return [null, null, null];
    }

    private static void WriteDoubleArray(Utf8JsonWriter writer, string name, double[] arr)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var v in arr) writer.WriteNumberValue(v);
        writer.WriteEndArray();
    }

    private static void WriteIntArray(Utf8JsonWriter writer, string name, int[] arr)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var v in arr) writer.WriteNumberValue(v);
        writer.WriteEndArray();
    }

    private static void WriteBoolArray(Utf8JsonWriter writer, string name, bool[] arr)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (var v in arr) writer.WriteBooleanValue(v);
        writer.WriteEndArray();
    }
}
