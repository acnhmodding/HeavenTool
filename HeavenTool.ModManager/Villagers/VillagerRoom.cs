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
/// Where the villager crafts or cooks: the workbench or kitchen it uses, which the game puts in the room only while the villager
/// does so, and the area of cells that takes up, which the game's names call LU (left, up) X and Z, width and height. The area
/// holds the item and the cells in front of it where the villager stands; it's deeper for the bigger species.
/// </summary>
public sealed record RoomActivity(RoomItem Item, int AreaX, int AreaZ, int AreaWidth, int AreaDepth)
{
    // The villager stands this many cells in front of the item.
    private const int StandDepth = 2;

    /// <summary>
    /// This activity with its item at <paramref name="item"/>, and the area moved to go with it: the item and the cells in front
    /// of it, centered in an area of the same size and kept in the room. 719 of the 834 vanilla areas are where this puts them;
    /// the others were moved a cell or two by hand.
    /// </summary>
    public RoomActivity MovedTo(RoomItem item, ItemFootprint size)
    {
        var (width, depth) = item.Rotation % 2 == 0 ? (size.X, size.Z) : (size.Z, size.X);
        int left = item.X, right = item.X + width, back = item.Z, front = item.Z + depth;
        // Facing front, right, back or left, by rotation.
        switch (item.Rotation & 3)
        {
            case 0: front += StandDepth; break;
            case 1: right += StandDepth; break;
            case 2: back -= StandDepth; break;
            default: left -= StandDepth; break;
        }
        return new RoomActivity(item, Fit(left, right, AreaWidth), Fit(back, front, AreaDepth), AreaWidth, AreaDepth);
    }

    // The first cell of an area of `size` cells centered on [start, end), kept inside the room.
    private static int Fit(int start, int end, int size) =>
        Math.Clamp(start + (int)Math.Floor((end - start - size) / 2.0), VillagerRoom.FirstCell, VillagerRoom.LastCell + 1 - size);
}

/// <summary>
/// A villager's room, as stored in <c>Param/NpcRoom/&lt;label&gt;.byml</c>: its wallpaper, flooring and music, the items in it,
/// where the villager crafts and cooks, and whether the flooring is turned a quarter turn. A null <paramref name="Crafting"/>,
/// <paramref name="Cooking"/> or <paramref name="FloorRotated"/>, as in projects from before they could be changed, keeps the
/// copied room's. The room's life and hobby points always stay as the room it was copied from has them.
/// </summary>
public sealed record VillagerRoom(int Wallpaper, int Flooring, int Music, IReadOnlyList<RoomItem> Items,
    RoomActivity? Crafting = null, RoomActivity? Cooking = null, bool? FloorRotated = null)
{
    /// <summary>The first and last half-tile cells of a villager's 6×6 room, on both axes.</summary>
    public const int FirstCell = 26, LastCell = 37;

    /// <summary>The wall row at the ceiling. Rows count down from it, so a 1-tile item on the floor is at row TopRow + 6.</summary>
    public const int TopRow = 22;

    // The param names hash to these keys: the CRC32 of the name, with its type after a dot for a number, as in mUnitX.u8.
    private const string WallpaperKey = "3503db35"; // mWall
    private const string FlooringKey = "bc0eb310"; // mFloor
    private const string MusicKey = "cf194774"; // mMusic
    private const string FloorRotateKey = "e954c311"; // mFloorRotate
    private const string ItemsKey = "b3a97a50"; // mFtrList
    private const string ZKey = "42ea11db"; // mUnitZ
    private const string XKey = "e8e3d950"; // mUnitX
    private const string LayerKey = "ec86704a";
    private const string PlacedItemKey = "d1541039";
    private const string ItemKey = "5bc12308";

    // An activity's item, and its area's first cells across and deep, width and depth.
    private static readonly ActivityKeys CraftingKeys =
        new("3b0c4ece", "ed83df67", "478a17ec", "f2925a13", "3f811252"); // mDIYWorkbench, mDIYRangeLUX, LUZ, Width, Height
    private static readonly ActivityKeys CookingKeys =
        new("96be42d5", "8549484e", "2f4080c5", "d8bfabd5", "4dac5826"); // mKitchen, mCookingRangeLUX, LUZ, Width, Height

    private sealed record ActivityKeys(string Item, string X, string Z, string Width, string Depth);

    public static VillagerRoom Read(IBymlNode root)
    {
        var room = HouseExterior.Body(root);
        var items = new List<RoomItem>();
        if (room.TryGetValue(ItemsKey, out var list) && list is BymlArray array)
            foreach (var entry in array.OfType<BymlDictionary>()) items.Add(ReadItem(entry));
        return new VillagerRoom(Item(room, WallpaperKey), Item(room, FlooringKey), Item(room, MusicKey), items,
            ReadActivity(room, CraftingKeys), ReadActivity(room, CookingKeys),
            room.ContainsKey(FloorRotateKey) ? Long(room, FloorRotateKey) != 0 : null);
    }

    private static RoomItem ReadItem(BymlDictionary entry)
    {
        var (id, rotation, flags, variant) = RoomItem.Unpack(entry.TryGetValue(PlacedItemKey, out var placed) && placed is BymlDictionary item
            ? Long(item, ItemKey) : 0);
        return new RoomItem(id, (int)Long(entry, XKey), (int)Long(entry, ZKey), rotation, (RoomLayer)Long(entry, LayerKey), variant, flags);
    }

    private static RoomActivity? ReadActivity(BymlDictionary room, ActivityKeys keys) =>
        room.TryGetValue(keys.Item, out var value) && value is BymlDictionary entry
            ? new RoomActivity(ReadItem(entry), (int)Long(room, keys.X), (int)Long(room, keys.Z), (int)Long(room, keys.Width), (int)Long(room, keys.Depth))
            : null;

    /// <summary>A room file for <paramref name="label"/>: a copy of <paramref name="template"/> with this room's items.</summary>
    public BymlDictionary Write(IBymlNode template, string label)
    {
        var room = (BymlDictionary)BymlJson.FromJson(BymlJson.ToJson(HouseExterior.Body(template)));
        room[WallpaperKey] = ItemNode(Wallpaper);
        room[FlooringKey] = ItemNode(Flooring);
        room[MusicKey] = ItemNode(Music);
        var items = new BymlArray(Items.Count);
        foreach (var item in Items) items.Add(ItemEntry(item));
        room[ItemsKey] = items;
        if (Crafting != null) WriteActivity(room, CraftingKeys, Crafting);
        if (Cooking != null) WriteActivity(room, CookingKeys, Cooking);
        if (FloorRotated is { } rotated) room[FloorRotateKey] = new BymlInt32Value(rotated ? 1 : 0);
        return new BymlDictionary { [HouseExterior.RootKey(label)] = room };
    }

    private static BymlDictionary ItemEntry(RoomItem item) => new()
    {
        [ZKey] = new BymlInt32Value(item.Z),
        [PlacedItemKey] = ItemNode(item.Value),
        [XKey] = new BymlInt32Value(item.X),
        [LayerKey] = new BymlInt32Value((int)item.Layer),
    };

    private static void WriteActivity(BymlDictionary room, ActivityKeys keys, RoomActivity activity)
    {
        room[keys.Item] = ItemEntry(activity.Item);
        room[keys.X] = new BymlInt32Value(activity.AreaX);
        room[keys.Z] = new BymlInt32Value(activity.AreaZ);
        room[keys.Width] = new BymlInt32Value(activity.AreaWidth);
        room[keys.Depth] = new BymlInt32Value(activity.AreaDepth);
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
