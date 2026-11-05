using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.ModManager.Villagers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>An item a villager can craft or cook at, for the menu that picks one, with its icon once that's loaded.</summary>
public sealed partial class ActivityItemOption : ObservableObject
{
    public ActivityItemOption(VillagerGameData data, int id)
    {
        Id = id;
        Name = data.ItemNames.TryGetValue(id, out var name) ? name : $"Item {id}";
        BodyCount = data.ItemBodyCount(id);
        LoadIcon(data);
    }

    public int Id { get; }
    public string Name { get; }

    /// <summary>How many body colors the item comes in, or 0 when it comes in one only.</summary>
    public int BodyCount { get; }

    [ObservableProperty]
    public partial Bitmap? Icon { get; set; }

    private async void LoadIcon(VillagerGameData data) => Icon = await ItemIcons.Load(data, Id);
}

/// <summary>
/// Where the villager crafts or cooks: the workbench or kitchen, where it stands and which way it faces. The area the villager
/// uses it in moves with it.
/// </summary>
public sealed partial class RoomActivityEntry : ObservableObject
{
    private readonly VillagerGameData data;
    private readonly Action changed;
    private RoomActivity activity;
    private bool loading = true;

    public RoomActivityEntry(string title, RoomActivity activity, IReadOnlyList<int> vanilla, VillagerGameData data, Action changed)
    {
        Title = title;
        this.activity = activity;
        this.data = data;
        this.changed = changed;
        // A room copied from a mod may use an item no vanilla villager does.
        Options = vanilla.Append(activity.Item.Id).Distinct()
            .Select(id => new ActivityItemOption(data, id)).ToList();
        Selected = Options.First(option => option.Id == activity.Item.Id);
        Facing = activity.Item.Rotation & 3;
        X = activity.Item.X;
        Z = activity.Item.Z;
        Body = activity.Item.Variant & 31;
        loading = false;
    }

    public string Title { get; }
    public IReadOnlyList<ActivityItemOption> Options { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasBody), nameof(BodyMaximum))]
    public partial ActivityItemOption? Selected { get; set; }
    [ObservableProperty]
    public partial int Facing { get; set; }
    [ObservableProperty]
    public partial decimal? X { get; set; }
    [ObservableProperty]
    public partial decimal? Z { get; set; }
    [ObservableProperty]
    public partial decimal? Body { get; set; }

    public RoomActivity Activity => activity;

    /// <summary>Whether the item comes in more than one body color, so the color can be picked.</summary>
    public bool HasBody => Selected?.BodyCount > 0;

    public int BodyMaximum => Math.Max(0, (Selected?.BodyCount ?? 0) - 1);

    partial void OnSelectedChanged(ActivityItemOption? value)
    {
        // Another item may come in fewer colors, or only one.
        if (!loading && Body > BodyMaximum)
        {
            loading = true;
            try { Body = BodyMaximum; }
            finally { loading = false; }
        }
        Update();
    }
    partial void OnFacingChanged(int value) => Update();
    partial void OnXChanged(decimal? value) => Update();
    partial void OnZChanged(decimal? value) => Update();
    partial void OnBodyChanged(decimal? value) => Update();

    private void Update()
    {
        if (loading || Selected == null) return;
        var old = activity.Item;
        var item = old with
        {
            Id = Selected.Id,
            X = (int)(X ?? old.X),
            Z = (int)(Z ?? old.Z),
            Rotation = Facing & 3,
            Variant = old.Variant & ~31u | (uint)(Body ?? 0) & 31,
        };
        activity = activity.MovedTo(item, data.ItemSize(item.Id));
        changed();
    }
}
