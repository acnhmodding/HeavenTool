using System.IO;
using System.Linq;

namespace HeavenTool.Avalonia.Settings;

/// <summary>Finds the user's extracted romfs. Only ever reads.</summary>
public static class GameFiles
{
    /// <summary>The romfs in <paramref name="folder"/>: the folder itself or its "romfs" subfolder.</summary>
    public static string? FindRomfs(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return null;
        string[] candidates = [folder, Path.Combine(folder, "romfs")];
        return candidates.FirstOrDefault(IsRomfs);
    }

    private static bool IsRomfs(string folder) =>
        Directory.Exists(Path.Combine(folder, "Bcsv")) && File.Exists(Path.Combine(folder, "System", "Resource", "ResourceSizeTable.srsizetable"));
}
