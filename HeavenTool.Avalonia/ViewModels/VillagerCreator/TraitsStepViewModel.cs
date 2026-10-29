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

    // The styles a villager can like, in the order of the game's style enum (Style1 and Style2).
    private static readonly string[] Styles = ["None", "Simple", "Gorgeous", "Cool", "Cute", "Active", "Elegant"];

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
        ChoiceField Color(string title, string key) => new(title, key, RowValues.VanillaOptions(data, key, json =>
        {
            var (name, swatch) = VillagerColors.Describe(json);
            return new ValueOption(json, name, Swatch: swatch);
        }));

        static string Style(string json) => int.TryParse(json, out var index) && index >= 0 && index < Styles.Length ? Styles[index] : $"Unknown ({json})";

        Favorites =
        [
            Vanilla("Favorite style", RowValues.Key(table, VillagerColumns.Style1), Style),
            Vanilla("Second favorite style", RowValues.Key(table, VillagerColumns.Style2), Style),
            Color("Favorite color", Key(VillagerColumns.Color1)),
            Color("Second favorite color", Key(VillagerColumns.Color2)),
        ];
        // The kinds are the ones vanilla villagers use in each column.
        ItemField Item(string title, string column, bool signed, ItemKinds kinds) =>
            new(title, Key(column), data, signed, kinds, picker => creator.PickItem(picker));
        Items =
        [
            Item("Default clothing", VillagerColumns.Tops, signed: false, new("tops and dresses", "Tops", "OnePiece")),
            Item("Umbrella", VillagerColumns.Umbrella, signed: false, new("umbrellas", "Umbrella")),
            Item("Rain hat", VillagerColumns.RainHat, signed: false, new("hats", "Cap")),
            Item("Raincoat", VillagerColumns.RainWear, signed: false, new("tops", "Tops")),
            Item("Photo", VillagerColumns.Photo, signed: true, new("photos", "Bromide")),
            Item("Poster", VillagerColumns.Poster, signed: true, new("posters", "Poster")),
        ];
        Birthday =
        [
            new ChoiceField("Birthday month", Key(VillagerColumns.BirthMonth),
                [.. Enumerable.Range(1, 12).Select(month => new ValueOption($"{month}", CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month)))]),
            new NumberField("Birthday day", Key(VillagerColumns.BirthDay), 1, 31),
            new ChoiceField("Name color", Key(VillagerColumns.NameColor), NameColors(Key(VillagerColumns.NameColor))),
        ];

        // Each value is shown as the name plate it draws in conversations, or as its number when the romfs has no palette.
        IReadOnlyList<ValueOption> NameColors(string key) => data.NameColors.Count == 0
            ? RowValues.VanillaOptions(data, key)
            : [.. data.NameColors.Select((color, index) => new ValueOption(
                index.ToString(CultureInfo.InvariantCulture), $"Color {index}",
                Swatch: VillagerColors.Brush(color.Plate), SwatchText: VillagerColors.Brush(color.Text)))];
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
