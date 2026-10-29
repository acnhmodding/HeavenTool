using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.IO;
using HeavenTool.ModManager.Villagers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>The kinds of item a field holds in vanilla villagers, by ItemKind name, and how to call them.</summary>
public sealed record ItemKinds(string Description, params string[] Names);

/// <summary>Picks a vanilla item by name, label or ID, from the kinds the field takes or from every item.</summary>
public sealed partial class ItemPickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<ItemChoice> choices;
    private readonly HashSet<uint> kinds;

    public ItemPickerViewModel(VillagerGameData data, string title, ItemKinds itemKinds, int? current)
    {
        Title = title;
        OnlyKindsText = $"Only {itemKinds.Description}";
        kinds = [.. itemKinds.Names.Select(name => name.ToCRC32())];
        choices = [.. data.Items.Select(item => new ItemChoice(data, item))];
        Filter();
        Selected = choices.FirstOrDefault(choice => choice.Item.Id == current);
    }

    public string Title { get; }
    public string OnlyKindsText { get; }

    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool onlyKinds = true;
    [ObservableProperty] private IReadOnlyList<ItemChoice> results = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelection))] private ItemChoice? selected;

    public bool HasSelection => Selected != null;
    public string CountText => Results.Count == 1 ? "1 item" : $"{Results.Count:N0} items";

    partial void OnSearchChanged(string value) => Filter();
    partial void OnOnlyKindsChanged(bool value) => Filter();
    partial void OnResultsChanged(IReadOnlyList<ItemChoice> value) => OnPropertyChanged(nameof(CountText));

    private void Filter()
    {
        var text = Search.Trim();
        var id = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
        Results = [.. choices.Where(choice => (!OnlyKinds || kinds.Contains(choice.Item.Kind)) && (text.Length == 0
            || choice.Item.Id == id
            || choice.Item.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || choice.Item.Label.Contains(text, StringComparison.OrdinalIgnoreCase)))];
    }
}

/// <summary>An item in the picker's list. Its icon loads when the list first shows it.</summary>
public sealed partial class ItemChoice(VillagerGameData data, ItemInfo item) : ObservableObject
{
    private Bitmap? icon;
    private bool requested;

    public ItemInfo Item { get; } = item;

    public Bitmap? Icon
    {
        get
        {
            if (!requested)
            {
                requested = true;
                _ = LoadIcon();
            }
            return icon;
        }
    }

    private async Task LoadIcon()
    {
        icon = await ItemIcons.Load(data, Item.Id);
        if (icon != null) OnPropertyChanged(nameof(Icon));
    }
}
