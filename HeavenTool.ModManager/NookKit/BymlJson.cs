#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace HeavenTool.ModManager.NookKit;

/// <summary>
/// Converts BYML to plain JSON and back, with no type tags. An integer that fits in 32 bits is an Int32 and a bigger one an Int64,
/// a number with a decimal point or an exponent is a Single, and strings, bools, nulls, arrays and dictionaries map directly.
/// </summary>
/// <remarks>
/// Nodes this can't tell apart on the way back (unsigned and double values, an Int64 small enough for 32 bits, binary data, hash
/// maps) are refused rather than changed silently. Villager params use none of them.
/// </remarks>
public static class BymlJson
{
    public static JsonNode? ToJson(IBymlNode node) => node switch
    {
        BymlDictionary dictionary => ToJsonObject(dictionary),
        BymlArray array => new JsonArray([.. array.Select(ToJson)]),
        BymlStringValue value => JsonValue.Create(value.Value),
        BymlBoolValue value => JsonValue.Create(value.Value),
        BymlInt32Value value => JsonValue.Create(value.Value),
        BymlInt64Value value when value.Value is < int.MinValue or > int.MaxValue => JsonValue.Create(value.Value),
        BymlSingleValue value => NookKitJson.Number(value.Value),
        BymlNullValue => null,
        _ => throw new NotSupportedException($"A BYML {Describe(node)} node can't be written as plain JSON."),
    };

    public static IBymlNode FromJson(JsonNode? node) => node switch
    {
        null => new BymlNullValue(),
        JsonObject obj => FromJsonObject(obj),
        JsonArray array => FromJsonArray(array),
        JsonValue value => FromJsonValue(value),
        _ => throw new NotSupportedException($"Unexpected JSON node {node.GetType().Name}."),
    };

    private static JsonObject ToJsonObject(BymlDictionary dictionary)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in dictionary)
            obj[key] = ToJson(value);
        return obj;
    }

    private static BymlDictionary FromJsonObject(JsonObject obj)
    {
        var dictionary = new BymlDictionary(obj.Count);
        foreach (var (key, value) in obj)
            dictionary[key] = FromJson(value);
        return dictionary;
    }

    private static BymlArray FromJsonArray(JsonArray array)
    {
        var result = new BymlArray(array.Count);
        foreach (var item in array)
            result.Add(FromJson(item));
        return result;
    }

    private static IBymlNode FromJsonValue(JsonValue value)
    {
        switch (value.GetValueKind())
        {
            case JsonValueKind.String: return new BymlStringValue(value.GetValue<string>());
            case JsonValueKind.True: return new BymlBoolValue(true);
            case JsonValueKind.False: return new BymlBoolValue(false);
            // Read from the number's text, which works the same for parsed values and for values built in memory.
            case JsonValueKind.Number when NookKitJson.IsFloat(value):
                return new BymlSingleValue(float.Parse(value.ToJsonString(), CultureInfo.InvariantCulture));
            case JsonValueKind.Number:
                var number = long.Parse(value.ToJsonString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                return number is >= int.MinValue and <= int.MaxValue ? new BymlInt32Value((int)number) : new BymlInt64Value(number);
            default: throw new NotSupportedException($"Unexpected JSON value {value.ToJsonString()}.");
        }
    }

    private static string Describe(IBymlNode node) => node is BymlInt64Value ? "Int64 that fits in 32 bits" : node.Type.ToString();
}
