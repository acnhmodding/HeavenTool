#nullable enable

using System.Text.RegularExpressions;

namespace HeavenTool.ModManager.Villagers;

/// <summary>
/// Villager labels, such as <c>wol12</c>: a three-letter species code and a two-digit number. The game finds the villager's
/// model, house, room and messages by this label.
/// </summary>
public static partial class VillagerLabel
{
    public static bool IsValid(string label) => Pattern().IsMatch(label);

    /// <summary>The species code at the start of a label: <c>wol</c> for <c>wol12</c>.</summary>
    public static string Species(string label) => label.Length >= 3 ? label[..3] : label;

    /// <summary>The model name the game loads for a label: <c>NpcNmlWol12</c> for <c>wol12</c>.</summary>
    public static string ModelName(string label) => label.Length == 0 ? "" : "NpcNml" + char.ToUpperInvariant(label[0]) + label[1..];

    /// <summary>
    /// The label after the species' highest one, or null when that would pass 99. Gaps between vanilla labels are skipped, as
    /// they can belong to villagers that were cut but are still mentioned in other files.
    /// </summary>
    public static string? NextFree(string species, IEnumerable<string> used)
    {
        var highest = used
            .Where(label => IsValid(label) && label.StartsWith(species, StringComparison.Ordinal))
            .Select(label => int.Parse(label.AsSpan(3)))
            .DefaultIfEmpty(-1)
            .Max();
        return highest < 99 ? $"{species}{highest + 1:00}" : null;
    }

    [GeneratedRegex("^[a-z]{3}[0-9]{2}$")]
    private static partial Regex Pattern();
}
