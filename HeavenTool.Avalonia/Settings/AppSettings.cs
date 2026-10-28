using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HeavenTool.Avalonia.Settings;

/// <summary>User settings, stored as JSON in the user's application data folder.</summary>
public sealed class AppSettings
{
    /// <summary>The folder deploys write to inside <see cref="ModFolder"/>, so the emulator can turn every managed mod off at once.</summary>
    public const string DeployFolderName = "Heaven Tool";

    public static string Folder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HeavenTool");
    public static string DefaultLibraryFolder { get; } = Path.Combine(Folder, "Library");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static AppSettings Current { get; } = Load();

    /// <summary>Raised after the settings are saved.</summary>
    public static event Action? Saved;

    /// <summary>The emulator folder that loads Animal Crossing mods.</summary>
    public string? ModFolder { get; set; }

    /// <summary>Where installed mods are kept, unpacked.</summary>
    public string LibraryFolder { get; set; } = DefaultLibraryFolder;

    /// <summary>Deploy skyline-acnh with the mods, so they can load code plugins.</summary>
    public bool InstallSkyline { get; set; } = true;

    /// <summary>Deploy the auto-rstb plugin, which fixes the RSTB as the game runs. Needs Skyline.</summary>
    public bool InstallAutoRstb { get; set; } = true;

    /// <summary>List GameBanana mods that have content ratings, which are hidden by default.</summary>
    public bool ShowRatedMods { get; set; }

    /// <summary>GameBanana mods the user starred, kept with what's needed to show them offline.</summary>
    public List<FavoriteMod> Favorites { get; set; } = [];

    /// <summary>The user's extracted romfs. Only ever read.</summary>
    public string? RomfsFolder { get; set; }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.WriteLine($"Could not read {FilePath}, using default settings: {ex.Message}");
        }
        // First run: deploy to the first emulator found.
        return new() { ModFolder = Emulators.Detect().FirstOrDefault(emulator => emulator.Found)?.ModFolder };
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        // Write a copy first so a failed write never leaves a truncated settings file.
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temporary, FilePath, true);
        Saved?.Invoke();
    }
}

public sealed record FavoriteMod(int ModId, string Name, string? Author, string? ImageUrl, string? ProfileUrl);
