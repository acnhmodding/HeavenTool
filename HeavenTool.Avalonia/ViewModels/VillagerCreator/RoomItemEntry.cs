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

    private readonly VillagerGameData data;
    private readonly Action changed;
    // The stored bits the editor doesn't show, kept as they were.
    private readonly uint otherVariant;
    private readonly int flags;
    private bool loading = true;

    public RoomItemEntry(RoomItem item, VillagerGameData data, Func<ItemPickerViewModel, Task<bool>> pick, Action changed)
    {
        this.data = data;
        this.changed = changed;
        otherVariant = item.Variant & ~0x3FFu;
        flags = item.Flags;
        Item = new ItemField("Item", "", data, false, Furniture, pick);
        Item.Show(item.Id);
        Item.Changed += _ => Update();
        Layer = (int)item.Layer;
        Facing = item.Rotation;
        X = item.X;
        Z = item.Z;
        Body = item.Variant & 31;
        Fabric = item.Variant >> 5 & 31;
        loading = false;
    }

    public ItemField Item { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(OnWall), nameof(Summary), nameof(ZTitle))] private int layer;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private int facing;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private decimal? x;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Summary))] private decimal? z;
    [ObservableProperty] private decimal? body;
    [ObservableProperty] private decimal? fabric;

    /// <summary>Items on a wall face away from it, so they can't be turned.</summary>
    public bool OnWall => RoomItem.WallRotation((RoomLayer)Layer) != null;

    public string ZTitle => OnWall ? "Row" : "Z";

    public string Name => Item.Value is { } id && data.ItemNames.TryGetValue((int)id, out var name) ? name
        : string.IsNullOrEmpty(data.ItemResource((int)(Item.Value ?? 0))) ? $"Room marker ({Item.Value})" : $"Item {Item.Value}";

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
