#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using HeavenTool.IO;
using System.Numerics;

namespace HeavenTool.ModManager.Villagers;

/// <summary>
/// The colors a villager's name is shown in during conversations: the plate behind the name and the name's text, as RGBA from
/// 0 to 1. They're listed in <c>Param/Npc/NpcColor.byml</c>, and NmlNpcParam's NpcColor column is an index into that list.
/// </summary>
public sealed record NameColor(Vector4 Plate, Vector4 Text)
{
    public const string ParamPath = "Param/Npc/NpcColor.byml";

    // The param names hash to these keys. Plate colors vary from entry to entry, while the text is nearly always a light cream.
    private static readonly string ListKey = Key("mParamsBuffer[]");
    private static readonly string PlateKey = Key("mNameBgColor.C4f");
    private static readonly string TextKey = Key("mNameStrColor.C4f");

    private static string Key(string name) => name.ToCRC32().ToString("x");

    /// <summary>Reads the colors in a NpcColor file's root node, by index.</summary>
    public static IReadOnlyList<NameColor> ReadAll(IBymlNode root) =>
        root is BymlDictionary { Count: 1 } dictionary && dictionary.Values.Single() is BymlDictionary body
            && body.TryGetValue(ListKey, out var list) && list is BymlArray entries
            ? [.. entries.Select(entry => entry is BymlDictionary color
                ? new NameColor(Rgba(color, PlateKey), Rgba(color, TextKey))
                : throw new InvalidDataException("A name color must be a dictionary."))]
            : throw new InvalidDataException("The NpcColor file must hold a single dictionary with the list of name colors.");

    private static Vector4 Rgba(BymlDictionary color, string key) =>
        color.TryGetValue(key, out var value) && value is BymlArray { Count: 4 } channels
            && channels.All(channel => channel is BymlSingleValue)
            ? new Vector4([.. channels.Select(channel => ((BymlSingleValue)channel).Value)])
            : throw new InvalidDataException("A name color must hold two RGBA colors.");
}
