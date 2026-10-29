#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using System.Text.Json.Nodes;

namespace HeavenTool.ModManager.NookKit;

/// <summary>
/// Adds or replaces one BYML file in a pack: <see cref="File"/> is its path in the pack (<c>Param/NpcHouse/wol13.byml</c>) and
/// <see cref="Archive"/> the pack's name (<c>StaticParam</c> for <c>Pack/StaticParam.pack</c>). <see cref="Data"/> is the whole
/// BYML as JSON, written by <see cref="BymlJson"/>.
/// </summary>
public sealed record PackPatch(string File, string Archive, JsonNode Data)
{
    public static PackPatch FromByml(string file, string archive, IBymlNode root) =>
        new(file, archive, BymlJson.ToJson(root) ?? throw new ArgumentException("A BYML root can't be null.", nameof(root)));
}
