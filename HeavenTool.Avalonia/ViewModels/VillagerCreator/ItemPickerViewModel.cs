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

/// <summary>
/// Picks a vanilla item by name, label or ID, from the kinds the field takes. Other kinds aren't offered, as the game may not
/// handle them where the field puts them.
/// </summary>
public sealed partial class ItemPickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<ItemChoice> choices;

    public ItemPickerViewModel(VillagerGameData data, string title, ItemKinds itemKinds, int? current)
    {
        Title = title;
        SearchHint = $"Search {itemKinds.Description} by name, label or ID";
        var kinds = itemKinds.Names.Select(name => name.ToCRC32()).ToHashSet();
        choices = [.. data.Items.Where(item => kinds.Contains(item.Kind)).Select(item => new ItemChoice(data, item))];
        Filter();
        Selected = choices.FirstOrDefault(choice => choice.Item.Id == current);
    }

    public string Title { get; }
    public string SearchHint { get; }

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<ItemChoice> Results { get; set; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial ItemChoice? Selected { get; set; }

    public bool HasSelection => Selected != null;
    public string CountText => Results.Count == 1 ? "1 item" : $"{Results.Count:N0} items";

    partial void OnSearchChanged(string value) => Filter();
    partial void OnResultsChanged(IReadOnlyList<ItemChoice> value) => OnPropertyChanged(nameof(CountText));

    private void Filter()
    {
        var text = Search.Trim();
        var id = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
        Results = [.. choices.Where(choice => text.Length == 0
            || choice.Item.Id == id
            || choice.Item.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || choice.Item.Label.Contains(text, StringComparison.OrdinalIgnoreCase))];
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
