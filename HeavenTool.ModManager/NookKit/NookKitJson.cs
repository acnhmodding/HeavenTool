#nullable enable

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace HeavenTool.ModManager.NookKit;

/// <summary>The kinds of patch NookKit loads, one folder each under <c>romfs/NookKit</c>.</summary>
public enum NookKitKind
{
    Message,
    Bcsv,
    Pack,
}

/// <summary>
/// Reads and writes NookKit patch files. Each file is <c>romfs/NookKit/&lt;kind&gt;/&lt;name&gt;.json</c> and holds a JSON array
/// of patches, so one file can change several game files.
/// </summary>
public static class NookKitJson
{
    public const string Folder = "NookKit";

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        IndentSize = 2,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Keeps names in other scripts and accented text readable instead of escaping them.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The path of a patch file of the given kind in a romfs folder.</summary>
    public static string PathFor(string romfs, NookKitKind kind, string name) =>
        Path.Combine(romfs, Folder, kind.ToString(), name + ".json");

    public static string Serialize<T>(IReadOnlyList<T> patches) => JsonSerializer.Serialize(patches, Options);

    public static List<T> Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<List<T>>(json, Options) ?? throw new JsonException("A NookKit patch file must hold an array of patches.");

    public static void Write<T>(string path, IReadOnlyList<T> patches)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Serialize(patches) + Environment.NewLine);
    }

    /// <summary>
    /// A float as a JSON number that always has a decimal point or an exponent (<c>30.0</c>, not <c>30</c>), so a reader can tell
    /// it from an integer without a type tag.
    /// </summary>
    public static JsonNode Number(float value)
    {
        if (!float.IsFinite(value))
            throw new NotSupportedException($"{value} can't be written as a JSON number.");

        var text = value.ToString("R", CultureInfo.InvariantCulture);
        if (text.IndexOfAny(['.', 'E', 'e']) < 0)
            text += ".0";
        return JsonNode.Parse(text)!;
    }

    /// <summary>Whether a JSON number was written as a float, with a decimal point or an exponent.</summary>
    public static bool IsFloat(JsonValue value) => value.ToJsonString().IndexOfAny(['.', 'E', 'e']) >= 0;
}
