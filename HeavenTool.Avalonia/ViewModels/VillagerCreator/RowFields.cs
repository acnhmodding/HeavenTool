using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.IO.FileFormats.BCSV;
using HeavenTool.ModManager.NookKit;
using HeavenTool.ModManager.Villagers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>One value a column can take, as its NookKit JSON, with what to show for it.</summary>
public sealed record ValueOption(string Json, string Text, string? Hint = null)
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

    /// <summary>
    /// The values vanilla villagers use in a column, each with a few villagers that use it, since the game files name few of them.
    /// </summary>
    public static IReadOnlyList<ValueOption> VanillaOptions(VillagerGameData data, string key, Func<string, string>? text = null)
    {
        var table = data.NmlNpcParam;
        return [.. data.Villagers
            .GroupBy(villager => BcsvPatch.Row(table, table.Entries[villager.Row])[key]?.ToJsonString() ?? "null")
            .OrderBy(group => long.TryParse(group.Key, out var number) ? number : long.MaxValue)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ValueOption(group.Key, text?.Invoke(group.Key) ?? Unquote(group.Key), Examples(group)))];
    }

    private static string Examples(IEnumerable<VillagerInfo> villagers)
    {
        var names = villagers.Select(villager => villager.Name).ToList();
        var shown = string.Join(", ", names.Take(4));
        return names.Count > 4 ? $"{shown} and {names.Count - 4} more" : shown;
    }

    public static string Unquote(string json) => json.Length >= 2 && json[0] == '"' ? json[1..^1] : json;
}

/// <summary>A row column picked from a list.</summary>
public sealed partial class ChoiceField(string title, string key, IReadOnlyList<ValueOption> options) : ObservableObject
{
    private JsonObject? row;

    public string Title { get; } = title;
    public ObservableCollection<ValueOption> Options { get; } = [.. options];
    [ObservableProperty] private ValueOption? selected;

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
    [ObservableProperty] private decimal? value;

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

/// <summary>A row column that holds an item ID, shown with the item's name.</summary>
public sealed partial class ItemField(string title, string key, IReadOnlyDictionary<int, string> names, bool signed) : ObservableObject
{
    private JsonObject? row;

    public string Title { get; } = title;
    public int Minimum { get; } = signed ? short.MinValue : 0;
    public int Maximum { get; } = signed ? short.MaxValue : ushort.MaxValue;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ItemName), nameof(IsKnown))] private decimal? value;

    public bool IsKnown => Value is { } id && names.ContainsKey((int)id);
    public string ItemName => Value is { } id && names.TryGetValue((int)id, out var name) ? name : "Not a vanilla item";

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
