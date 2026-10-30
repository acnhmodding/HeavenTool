using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.ModManager.Villagers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>One item in the room list, with what the editor below the list changes about it.</summary>
public sealed partial class RoomItemEntry : ObservableObject
{
    /// <summary>What a room can hold, for the item picker.</summary>
    public static readonly ItemKinds Furniture = new("furniture and wall items",
        "Ftr", "Rug", "Poster", "Bromide", "Picture", "Sculpture", "Gyroid", "Fossil", "Dishes");

    /// <summary>The names of <see cref="RoomLayer"/>, in order.</summary>
    public static IReadOnlyList<string> Layers { get; } =
        ["On the floor", "On furniture", "Back wall", "Left wall", "Front wall", "Right wall", "Rug"];

    /// <summary>The ways a floor item can face, by rotation.</summary>
    public static IReadOnlyList<string> Facings { get; } = ["Front", "Right", "Back", "Left"];

    // What each room marker holds a spot for, by the end of its label.
    private static readonly Dictionary<string, string> MarkerKinds = new()
    {
        ["FtrFloor1x1"] = "1×1 furniture", ["FtrFloor2x1"] = "2×1 furniture", ["FtrFloor2x2"] = "2×2 furniture",
        ["ClothFloor"] = "clothes on the floor", ["ClothWall"] = "clothes on a wall",
        ["Insect"] = "bug", ["Fish"] = "fish", ["Music"] = "music",
    };

    /// <summary>What a room marker holds a spot for, from the end of its label, such as "2×1 furniture".</summary>
    public static string MarkerKind(string kind) => MarkerKinds.GetValueOrDefault(kind, kind);

    public static string MarkerName(string kind) => $"Gift spot · {MarkerKind(kind)}";

    private readonly VillagerGameData data;
    private readonly Action changed;
    // The stored bits the editor doesn't show, kept as they were.
    private readonly uint otherVariant;
    private readonly int flags;
    private bool loading = true;
    private decimal? shownId;

    public RoomItemEntry(RoomItem item, VillagerGameData data, Func<ItemPickerViewModel, Task<bool>> pick, Action changed)
    {
        this.data = data;
        this.changed = changed;
        otherVariant = item.Variant & ~0x3FFu;
        flags = item.Flags;
        Item = new ItemField("Item", "", data, false, Furniture, pick);
        Item.Show(shownId = item.Id);
        Item.Changed += OnItemChanged;
        Layer = (int)item.Layer;
        Facing = item.Rotation;
        X = item.X;
        Z = item.Z;
        Body = item.Variant & 31;
        Fabric = item.Variant >> 5 & 31;
        loading = false;
    }

    public ItemField Item { get; }

    /// <summary>
    /// Whether this is a room marker, a gift spot the game fills itself. A marker stays a marker, and furniture can't become one,
    /// so only where it is changes.
    /// </summary>
    public bool IsMarker => Item.Value is { } id && data.RoomMarkers.ContainsKey((int)id);

    [ObservableProperty, NotifyPropertyChangedFor(nameof(OnWall), nameof(Summary), nameof(ZTitle))] private int layer;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private int facing;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private decimal? x;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private decimal? z;
    [ObservableProperty] private decimal? body;
    [ObservableProperty] private decimal? fabric;

    /// <summary>Items on a wall face away from it, so they can't be turned.</summary>
    public bool OnWall => RoomItem.WallRotation((RoomLayer)Layer) != null;

    public string ZTitle => OnWall ? "Row" : "Z";

    public string Name => Item.Value is not { } id ? "No item"
        : data.RoomMarkers.TryGetValue((int)id, out var marker) ? MarkerName(marker)
        : data.ItemNames.TryGetValue((int)id, out var name) ? name : $"Item {id}";

    public string Summary => OnWall
        ? $"{Layers[Layer]} · row {Z}, at {X}"
        : $"{Layers[Layer]} · {X}, {Z} · facing {Facings[Facing & 3].ToLowerInvariant()}";

    public RoomItem ToRoomItem()
    {
        var layer = (RoomLayer)Layer;
        uint variant = otherVariant | (uint)(Body ?? 0) & 31 | ((uint)(Fabric ?? 0) & 31) << 5;
        return new RoomItem((int)(Item.Value ?? 0), (int)(X ?? 0), (int)(Z ?? 0), RoomItem.WallRotation(layer) ?? Facing & 3, layer,
            variant, flags);
    }

    private void OnItemChanged(int id)
    {
        // The ID box can take any number, so a marker typed into furniture, or the other way round, goes back.
        bool wasMarker = shownId is { } shown && data.RoomMarkers.ContainsKey((int)shown);
        if (id != shownId && (wasMarker || data.RoomMarkers.ContainsKey(id)))
        {
            Dispatcher.UIThread.Post(() => Item.Show(shownId));
            return;
        }
        shownId = id;
        Update();
    }

    partial void OnLayerChanged(int value)
    {
        if (RoomItem.WallRotation((RoomLayer)value) is { } rotation) Facing = rotation;
        Update();
    }

    partial void OnFacingChanged(int value) => Update();
    partial void OnXChanged(decimal? value) => Update();
    partial void OnZChanged(decimal? value) => Update();
    partial void OnBodyChanged(decimal? value) => Update();
    partial void OnFabricChanged(decimal? value) => Update();

    private void Update()
    {
        if (loading) return;
        OnPropertyChanged(nameof(Name));
        changed();
    }
}
