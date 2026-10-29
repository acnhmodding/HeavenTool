#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using HeavenTool.IO;
using HeavenTool.ModManager.NookKit;

namespace HeavenTool.ModManager.Villagers;

/// <summary>
/// A villager's house exterior, as stored in <c>Param/NpcHouse/&lt;label&gt;.byml</c>. Shape, wall, roof and door are
/// UniqueIDs in the <c>StructureHouse*Param</c> tables; a wall and a roof only fit the shape their row names.
/// </summary>
public sealed record HouseExterior(int Shape, int Wall, int Roof, int Door, int Curtain, int DecoItem)
{
    /// <summary>The deco item value vanilla houses use for "no deco".</summary>
    public const int NoDeco = 65534;

    // The param names hash to these keys. Found by matching the values of all 417 vanilla houses against the part tables.
    private const string ShapeKey = "e343733b";
    private const string WallKey = "1c37a23";
    private const string RoofKey = "d5386515";
    private const string DoorKey = "fa8c6cfb";
    private const string CurtainKey = "5ba5def0";
    private const string DecoKey = "6e37c6ed";
    private const string ItemKey = "5bc12308";

    public static string PathFor(string label) => $"Param/NpcHouse/{label}.byml";

    /// <summary>Reads a house file's root node.</summary>
    public static HouseExterior Read(IBymlNode root)
    {
        var house = Body(root);
        return new HouseExterior(
            Int(house, ShapeKey), Int(house, WallKey), Int(house, RoofKey), Int(house, DoorKey), Int(house, CurtainKey),
            house.TryGetValue(DecoKey, out var deco) && deco is BymlDictionary item ? Int(item, ItemKey) : NoDeco);
    }

    /// <summary>A house file for <paramref name="label"/>: a copy of <paramref name="template"/> with these parts.</summary>
    public BymlDictionary Write(IBymlNode template, string label)
    {
        var house = (BymlDictionary)BymlJson.FromJson(BymlJson.ToJson(Body(template)));
        house[ShapeKey] = new BymlInt32Value(Shape);
        house[WallKey] = new BymlInt32Value(Wall);
        house[RoofKey] = new BymlInt32Value(Roof);
        house[DoorKey] = new BymlInt32Value(Door);
        house[CurtainKey] = new BymlInt32Value(Curtain);
        house[DecoKey] = new BymlDictionary { [ItemKey] = new BymlInt32Value(DecoItem) };
        return new BymlDictionary { [RootKey(label)] = house };
    }

    /// <summary>House and room files hold one dictionary, keyed by the CRC32 of the villager's label.</summary>
    public static string RootKey(string label) => label.ToCRC32().ToString("x");

    /// <summary>A copy of a house or room file with its root key changed to another villager's label.</summary>
    public static BymlDictionary Rekey(IBymlNode root, string label) =>
        new() { [RootKey(label)] = BymlJson.FromJson(BymlJson.ToJson(Body(root))) };

    private static BymlDictionary Body(IBymlNode root) =>
        root is BymlDictionary { Count: 1 } dictionary && dictionary.Values.Single() is BymlDictionary body
            ? body
            : throw new InvalidDataException("A villager house or room file must hold a single dictionary.");

    // A missing value means 0, as in the few vanilla houses that leave some out.
    private static int Int(BymlDictionary dictionary, string key) =>
        dictionary.TryGetValue(key, out var value) && value is BymlInt32Value number ? number.Value : 0;
}
