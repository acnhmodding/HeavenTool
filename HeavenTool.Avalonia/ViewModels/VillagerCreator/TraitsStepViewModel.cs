using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.ModManager.Villagers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>The villager's personality, birthday, favorites and default clothing, all from its NmlNpcParam row.</summary>
public partial class TraitsStepViewModel(VillagerCreatorViewModel creator) : ObservableObject
{
    // The personality each NpcLooks value is in the game's English text.
    private static readonly Dictionary<string, string> Personalities = new()
    {
        ["Boy_active"] = "Jock", ["Boy_normal"] = "Lazy", ["Boy_pride"] = "Cranky", ["Boy_snobby"] = "Smug",
        ["Girl_active"] = "Peppy", ["Girl_big_sis"] = "Sisterly", ["Girl_normal"] = "Normal", ["Girl_pride"] = "Snooty",
    };

    [ObservableProperty] private IReadOnlyList<ChoiceField> personality = [];
    [ObservableProperty] private IReadOnlyList<ChoiceField> favorites = [];
    [ObservableProperty] private IReadOnlyList<ItemField> items = [];
    [ObservableProperty] private IReadOnlyList<ObservableObject> birthday = [];

    internal void Initialize()
    {
        var data = creator.Data;
        var table = data.NmlNpcParam;
        string Key(string column) => RowValues.Key(table, column);
        ChoiceField Vanilla(string title, string key, System.Func<string, string>? text = null) =>
            new(title, key, RowValues.VanillaOptions(data, key, text));

        Personality =
        [
            Vanilla("Personality", Key(VillagerColumns.Personality),
                json => Personalities.GetValueOrDefault(RowValues.Unquote(json)) is { } name ? $"{name} ({RowValues.Unquote(json)})" : RowValues.Unquote(json)),
            Vanilla("Personality subtype", Key(VillagerColumns.SubPersonality), json => json == "0" ? "A" : json == "1" ? "B" : json),
            Vanilla("Hobby", Key(VillagerColumns.Hobby)),
        ];
        // The game files don't name styles and colors, so each value lists villagers that have it.
        Favorites =
        [
            Vanilla("Favorite style", RowValues.Key(table, VillagerColumns.Style1)),
            Vanilla("Second favorite style", RowValues.Key(table, VillagerColumns.Style2)),
            Vanilla("Favorite color", Key(VillagerColumns.Color1)),
            Vanilla("Second favorite color", Key(VillagerColumns.Color2)),
        ];
        Items =
        [
            new("Default clothing", Key(VillagerColumns.Tops), data.ItemNames, signed: false),
            new("Umbrella", Key(VillagerColumns.Umbrella), data.ItemNames, signed: false),
            new("Rain hat", Key(VillagerColumns.RainHat), data.ItemNames, signed: false),
            new("Raincoat", Key(VillagerColumns.RainWear), data.ItemNames, signed: false),
            new("Photo", Key(VillagerColumns.Photo), data.ItemNames, signed: true),
            new("Poster", Key(VillagerColumns.Poster), data.ItemNames, signed: true),
        ];
        Birthday =
        [
            new ChoiceField("Birthday month", Key(VillagerColumns.BirthMonth),
                [.. Enumerable.Range(1, 12).Select(month => new ValueOption($"{month}", CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)))]),
            new NumberField("Birthday day", Key(VillagerColumns.BirthDay), 1, 31),
            new NumberField("Name color", Key(VillagerColumns.NameColor), 0, 255, "The color of the villager's name in conversations."),
        ];
    }

    internal void Load(VillagerProject project)
    {
        foreach (var field in Personality.Concat(Favorites)) field.Load(project.Row);
        foreach (var field in Items) field.Load(project.Row);
        foreach (var field in Birthday)
        {
            if (field is ChoiceField choice) choice.Load(project.Row);
            else if (field is NumberField number) number.Load(project.Row);
        }
    }
}
