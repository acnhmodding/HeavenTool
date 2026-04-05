using System.Collections.Generic;

namespace HeavenTool.Forms.Search;

/// <summary>
/// Host for table find-and-replace using exact displayed cell text.
/// </summary>
public interface IFindReplaceable
{
    /// <summary>
    /// Replaces all cells whose displayed text exactly matches <paramref name="find"/>.
    /// </summary>
    /// <param name="excludedColumnNames">Column header texts to skip (exact match, case-insensitive). Empty or null skips no columns.</param>
    /// <returns>Number of grid cells that were updated.</returns>
    int ReplaceAllExact(string find, string replace, bool caseSensitive, IReadOnlyList<string>? excludedColumnNames = null);
}
