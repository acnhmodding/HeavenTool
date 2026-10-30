#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using HeavenTool.ModManager.NookKit;
using System.Text.Json.Serialization;

namespace HeavenTool.ModManager.Villagers;

/// <summary>Where a room item stands: on the floor, on another item, on one of the four walls, or under everything as a rug.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RoomLayer>))]
public enum RoomLayer
{
    Floor = 0,
    OnFurniture = 1,
    BackWall = 2,
    LeftWall = 3,
    FrontWall = 4,
    RightWall = 5,
    Rug = 6,
}

/// <summary>
/// One item placed in a room. The room is a grid of half tiles: <paramref name="X"/> runs left to right and <paramref name="Z"/>
/// back to front, both from <see cref="VillagerRoom.FirstCell"/> to <see cref="VillagerRoom.LastCell"/> on the floor, at the
/// item's corner nearest the back left. On a wall, <paramref name="Z"/> is the row of the item's top edge counted down from
/// <see cref="VillagerRoom.TopRow"/>, and the other coordinate runs along the wall. <paramref name="Rotation"/> turns the item
/// in quarter turns, 0 facing the front; wall items face away from their wall. <paramref name="Variant"/> is the item's
/// remake, body | fabric &lt;&lt; 5. <paramref name="Flags"/> keeps the other bits of the stored value, such as the one clothes
/// hung on a wall have.
/// </summary>
public sealed record RoomItem(int Id, int X, int Z, int Rotation, RoomLayer Layer, uint Variant, int Flags = 0)
{
    [JsonIgnore] public bool OnWall => Layer is >= RoomLayer.BackWall and <= RoomLayer.RightWall;

    /// <summary>The rotation an item on <paramref name="layer"/> must have, or null when it may face any way.</summary>
    public static int? WallRotation(RoomLayer layer) =>
        layer is >= RoomLayer.BackWall and <= RoomLayer.RightWall ? layer - RoomLayer.BackWall : null;

    // The stored value packs the item ID, then the rotation and flags, then the variant in the upper half.
    internal long Value => (ushort)Id | (long)(ushort)((Rotation & 3) | Flags) << 16 | (long)Variant << 32;

    internal static (int Id, int Rotation, int Flags, uint Variant) Unpack(long value) =>
        ((ushort)value, (int)(value >> 16) & 3, (int)(value >> 16) & 0xFFFC, (uint)(value >> 32));
}

/// <summary>
/// A villager's room, as stored in <c>Param/NpcRoom/&lt;label&gt;.byml</c>: its wallpaper, flooring and music, and the items in it.
/// Other values of the file, such as where the villager crafts, stay as the room it was copied from has them.
/// </summary>
public sealed record VillagerRoom(int Wallpaper, int Flooring, int Music, IReadOnlyList<RoomItem> Items)
{
    /// <summary>The first and last half-tile cells of a villager's 6×6 room, on both axes.</summary>
    public const int FirstCell = 26, LastCell = 37;

    /// <summary>The wall row at the ceiling. Rows count down from it, so a 1-tile item on the floor is at row TopRow + 6.</summary>
    public const int TopRow = 22;

    // The param names hash to these keys. Found by matching the values of all 426 vanilla rooms against ItemParam.
    private const string WallpaperKey = "3503db35";
    private const string FlooringKey = "bc0eb310";
    private const string MusicKey = "cf194774";
    private const string ItemsKey = "b3a97a50";
    private const string ZKey = "42ea11db";
    private const string XKey = "e8e3d950";
    private const string LayerKey = "ec86704a";
    private const string PlacedItemKey = "d1541039";
    private const string ItemKey = "5bc12308";

    public static VillagerRoom Read(IBymlNode root)
    {
        var room = HouseExterior.Body(root);
        var items = new List<RoomItem>();
        if (room.TryGetValue(ItemsKey, out var list) && list is BymlArray array)
            foreach (var entry in array.OfType<BymlDictionary>())
            {
                var (id, rotation, flags, variant) = RoomItem.Unpack(entry.TryGetValue(PlacedItemKey, out var placed) && placed is BymlDictionary item
                    ? Long(item, ItemKey) : 0);
                items.Add(new RoomItem(id, (int)Long(entry, XKey), (int)Long(entry, ZKey), rotation, (RoomLayer)Long(entry, LayerKey), variant, flags));
            }
        return new VillagerRoom(Item(room, WallpaperKey), Item(room, FlooringKey), Item(room, MusicKey), items);
    }

    /// <summary>A room file for <paramref name="label"/>: a copy of <paramref name="template"/> with this room's items.</summary>
    public BymlDictionary Write(IBymlNode template, string label)
    {
        var room = (BymlDictionary)BymlJson.FromJson(BymlJson.ToJson(HouseExterior.Body(template)));
        room[WallpaperKey] = ItemNode(Wallpaper);
        room[FlooringKey] = ItemNode(Flooring);
        room[MusicKey] = ItemNode(Music);
        var items = new BymlArray(Items.Count);
        foreach (var item in Items)
            items.Add(new BymlDictionary
            {
                [ZKey] = new BymlInt32Value(item.Z),
                [PlacedItemKey] = ItemNode(item.Value),
                [XKey] = new BymlInt32Value(item.X),
                [LayerKey] = new BymlInt32Value((int)item.Layer),
            });
        room[ItemsKey] = items;
        return new BymlDictionary { [HouseExterior.RootKey(label)] = room };
    }

    // Vanilla rooms store a value that fits in 32 bits as an Int32 and only the bigger ones as an Int64.
    private static BymlDictionary ItemNode(long value) => new()
    {
        [ItemKey] = value is >= int.MinValue and <= int.MaxValue ? new BymlInt32Value((int)value) : new BymlInt64Value(value),
    };

    private static int Item(BymlDictionary room, string key) =>
        room.TryGetValue(key, out var value) && value is BymlDictionary item ? (int)Long(item, ItemKey) : 0;

    private static long Long(BymlDictionary dictionary, string key) => dictionary.TryGetValue(key, out var value)
        ? value switch { BymlInt32Value number => number.Value, BymlInt64Value number => number.Value, _ => 0 }
        : 0;
}
