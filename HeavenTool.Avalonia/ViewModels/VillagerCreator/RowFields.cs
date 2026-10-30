using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.IO.FileFormats.BCSV;
using HeavenTool.ModManager.NookKit;
using HeavenTool.ModManager.Villagers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>
/// One value a column can take, as its NookKit JSON, with what to show for it. With <paramref name="SwatchText"/>, the text is
/// drawn in that color on the swatch, as a name plate.
/// </summary>
public sealed record ValueOption(string Json, string Text, IBrush? Swatch = null, IBrush? SwatchText = null)
{
    public override string ToString() => Text;
}

/// <summary>Reads and writes the project's NmlNpcParam row, which is kept in NookKit's row format.</summary>
public static class RowValues
{
    /// <summary>The row key of a column: its name when known, or its hash.</summary>
    public static string Key(BinaryCSV table, string column) => BcsvPatch.ColumnKey(table.Fields[table.ColumnIndex(column)]);

    public static string Key(BinaryCSV table, uint column) => BcsvPatch.ColumnKey(table.Fields[table.ColumnIndex(column)]);

    public static long Integer(JsonObject row, string key) =>
        row[key] is JsonValue value && long.TryParse(value.ToJsonString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;

    /// <summary>The values vanilla villagers use in a column, in order, shown as <paramref name="text"/> or as the value itself.</summary>
    public static IReadOnlyList<ValueOption> VanillaOptions(VillagerGameData data, string key, Func<string, string>? text = null) =>
        VanillaOptions(data, key, json => new ValueOption(json, text?.Invoke(json) ?? Unquote(json)));

    /// <summary>The values vanilla villagers use in a column, in order, each made by <paramref name="option"/>.</summary>
    public static IReadOnlyList<ValueOption> VanillaOptions(VillagerGameData data, string key, Func<string, ValueOption> option)
    {
        var table = data.NmlNpcParam;
        return [.. data.Villagers
            .Select(villager => BcsvPatch.Row(table, table.Entries[villager.Row])[key]?.ToJsonString() ?? "null")
            .Distinct()
            .OrderBy(json => long.TryParse(json, out var number) ? number : long.MaxValue)
            .ThenBy(json => json, StringComparer.Ordinal)
            .Select(option)];
    }

    public static string Unquote(string json) => json.Length >= 2 && json[0] == '"' ? json[1..^1] : json;
}

/// <summary>A row column picked from a list.</summary>
public sealed partial class ChoiceField(string title, string key, IReadOnlyList<ValueOption> options) : ObservableObject
{
    private JsonObject? row;

    public string Title { get; } = title;
    public ObservableCollection<ValueOption> Options { get; } = [.. options];
    [ObservableProperty]
    public partial ValueOption? Selected { get; set; }

    public void Load(JsonObject value)
    {
        row = null;
        var json = value[key]?.ToJsonString() ?? "null";
        // A project can hold a value no vanilla villager uses; keep it rather than change it.
        var option = Options.FirstOrDefault(option => option.Json == json);
        if (option == null) Options.Add(option = new ValueOption(json, $"Custom ({RowValues.Unquote(json)})"));
        Selected = option;
        row = value;
    }

    partial void OnSelectedChanged(ValueOption? value)
    {
        if (row != null && value != null) row[key] = JsonNode.Parse(value.Json);
    }
}

/// <summary>A numeric row column.</summary>
public sealed partial class NumberField(string title, string key, int minimum, int maximum, string? description = null) : ObservableObject
{
    private JsonObject? row;

    public string Title { get; } = title;
    public string? Description { get; } = description;
    public int Minimum { get; } = minimum;
    public int Maximum { get; } = maximum;
    [ObservableProperty]
    public partial decimal? Value { get; set; }

    public void Load(JsonObject source)
    {
        row = null;
        Value = RowValues.Integer(source, key);
        row = source;
    }

    partial void OnValueChanged(decimal? value)
    {
        if (row != null && value is { } number) row[key] = (long)Math.Clamp(number, Minimum, Maximum);
    }
}

/// <summary>
/// A row column that holds an item ID, shown with the item's name and, when the romfs has one, its icon. The item can also be
/// chosen by name in a picker, which <paramref name="pick"/> shows, from <paramref name="kinds"/> first. A field kept outside a row
/// is shown with <see cref="Show"/> and reports what the user picks through <see cref="Changed"/>.
/// </summary>
public sealed partial class ItemField(string title, string key, VillagerGameData data, bool signed, ItemKinds kinds,
    Func<ItemPickerViewModel, Task<bool>> pick) : ObservableObject
{
    private readonly IReadOnlyDictionary<int, string> names = data.ItemNames;
    private JsonObject? row;
    private bool loading;

    public string Title { get; } = title;
    public int Minimum { get; } = signed ? short.MinValue : 0;
    public int Maximum { get; } = signed ? short.MaxValue : ushort.MaxValue;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ItemName), nameof(IsKnown))]
    public partial decimal? Value { get; set; }

    [ObservableProperty]
    public partial Bitmap? Icon { get; set; }

    public bool IsKnown => Value is { } id && names.ContainsKey((int)id);
    public string ItemName => Value is { } id && names.TryGetValue((int)id, out var name) ? name : "Not a vanilla item";

    /// <summary>Raised with the new ID when the user types or picks another item.</summary>
    public event Action<int>? Changed;

    public void Load(JsonObject source)
    {
        row = null;
        Show(RowValues.Integer(source, key));
        row = source;
    }

    /// <summary>Shows an item without raising <see cref="Changed"/>.</summary>
    public void Show(decimal? id)
    {
        loading = true;
        try { Value = id; }
        finally { loading = false; }
    }

    partial void OnValueChanged(decimal? value)
    {
        if (row != null && value is { } number) row[key] = (long)Math.Clamp(number, Minimum, Maximum);
        if (!loading && value is { } id) Changed?.Invoke((int)Math.Clamp(id, Minimum, Maximum));
        _ = ShowIcon(value);
    }

    private async Task ShowIcon(decimal? id)
    {
        var icon = id is { } number ? await ItemIcons.Load(data, (int)number) : null;
        // The value may have changed while the icon loaded.
        if (Value == id) Icon = icon;
    }

    [RelayCommand]
    private async Task Pick()
    {
        var picker = new ItemPickerViewModel(data, Title, kinds, Value is { } id ? (int)id : null);
        if (await pick(picker) && picker.Selected is { } choice) Value = choice.Item.Id;
    }
}
