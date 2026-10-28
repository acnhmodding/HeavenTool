using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HeavenTool.Avalonia.Settings;

/// <summary>An emulator and the folder it loads Animal Crossing mods from. <see cref="Found"/> is false when its data folder isn't on this PC.</summary>
public sealed record EmulatorInstall(string Name, string ModFolder, bool Found);

public static class Emulators
{
    public const string TitleId = "01006F8002326000";

    // Ryujinx loads <data>/mods/contents/<title id>/<mod>/romfs; yuzu forks load <data>/load/<TITLE ID>/<mod>/romfs.
    // Every emulator is offered, installed or not.
    private static readonly (string Name, string Folder, bool YuzuLayout)[] Known =
    [
        ("Ryujinx", "Ryujinx", false),
        ("Eden", "eden", true),
        ("Citron", "citron", true),
        ("Sudachi", "sudachi", true),
        ("Suyu", "suyu", true),
        ("yuzu", "yuzu", true),
    ];

    /// <summary>Every known emulator: installed ones first, then the rest at their default folders.</summary>
    public static IReadOnlyList<EmulatorInstall> Detect()
    {
        var emulators = new List<EmulatorInstall>();
        foreach (var (name, folder, yuzuLayout) in Known)
        {
            var root = FindRoot(folder);
            var data = root ?? Path.Combine(DefaultDataFolder(yuzuLayout), folder);
            var modFolder = yuzuLayout
                ? TitleFolder(Path.Combine(data, "load"), TitleId)
                : TitleFolder(Path.Combine(data, "mods", "contents"), TitleId.ToLowerInvariant());
            emulators.Add(new(name, modFolder, root != null));
        }
        return [.. emulators.OrderByDescending(emulator => emulator.Found)];
    }

    /// <summary>The name of the emulator that loads mods from <paramref name="modFolder"/>, or null for a folder none of them uses.</summary>
    public static string? NameFor(string modFolder)
    {
        if (Detect().FirstOrDefault(emulator => SamePath(emulator.ModFolder, modFolder)) is { } emulator) return emulator.Name;
        // A portable install keeps its data beside the emulator, in a folder named after it.
        var parts = modFolder.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return Known.FirstOrDefault(known => parts.Contains(known.Folder, StringComparer.OrdinalIgnoreCase)).Name;
    }

    public static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }

    private static string? FindRoot(string name) =>
        DataFolders().Select(folder => Path.Combine(folder, name)).FirstOrDefault(Directory.Exists);

    // Windows keeps both kinds under %AppData%. Linux splits them between config (Ryujinx) and data (yuzu forks).
    private static IEnumerable<string> DataFolders()
    {
        string[] folders =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            OperatingSystem.IsMacOS() ? MacApplicationSupport() : "",
        ];
        return folders.Where(folder => folder.Length > 0);
    }

    private static string DefaultDataFolder(bool yuzuLayout) =>
        OperatingSystem.IsMacOS() ? MacApplicationSupport()
        : OperatingSystem.IsLinux() && yuzuLayout ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    private static string MacApplicationSupport() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");

    // Emulators differ in the title ID's case, so reuse a folder that already exists whatever its case.
    private static string TitleFolder(string parent, string titleId)
    {
        try
        {
            if (Directory.Exists(parent))
                foreach (var folder in Directory.EnumerateDirectories(parent))
                    if (string.Equals(Path.GetFileName(folder), titleId, StringComparison.OrdinalIgnoreCase)) return folder;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return Path.Combine(parent, titleId);
    }
}
