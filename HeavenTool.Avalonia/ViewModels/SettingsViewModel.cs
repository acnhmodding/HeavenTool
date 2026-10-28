using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

public enum StatusKind { Ok, Info, Warning, Error }

public sealed record SettingStatus(StatusKind Kind, string Text);

public partial class SettingsViewModel : ObservableObject
{
    /// <summary>The emulator choice for a folder that matches no known emulator.</summary>
    public static EmulatorInstall CustomFolder { get; } = new("Custom folder", "", true);
    public static SettingStatus RstbFixOffWarning { get; } =
        new(StatusKind.Warning, "Without auto-rstb, mods that make game files larger than the originals can crash the game.");

    private readonly AppSettings settings;
    private readonly IStorageProvider storage;
    private readonly ILauncher launcher;
    private readonly IReadOnlyList<EmulatorInstall> emulators = Emulators.Detect();
    // Typing a path saves once the user pauses rather than on every key.
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool dirty;

    public IReadOnlyList<EmulatorInstall> EmulatorOptions { get; }
    public bool RstbFixOff => !InstallSkyline || !InstallAutoRstb;

    [ObservableProperty] private EmulatorInstall selectedEmulator = CustomFolder;
    [ObservableProperty] private string modFolder;
    [ObservableProperty] private SettingStatus modFolderStatus = null!;
    [ObservableProperty] private string libraryFolder;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RstbFixOff))] private bool installSkyline;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(RstbFixOff))] private bool installAutoRstb;
    [ObservableProperty] private string romfsFolder;
    [ObservableProperty] private SettingStatus romfsStatus = null!;
    [ObservableProperty] private string? error;

    public SettingsViewModel(AppSettings settings, IStorageProvider storage, ILauncher launcher)
    {
        this.settings = settings;
        this.storage = storage;
        this.launcher = launcher;
        EmulatorOptions = [.. emulators, CustomFolder];
        modFolder = settings.ModFolder ?? "";
        libraryFolder = settings.LibraryFolder;
        installSkyline = settings.InstallSkyline;
        installAutoRstb = settings.InstallAutoRstb;
        romfsFolder = settings.RomfsFolder ?? "";
        saveTimer.Tick += (_, _) => Save();
        UpdateModFolder();
        UpdateRomfs();
    }

    partial void OnSelectedEmulatorChanged(EmulatorInstall value)
    {
        // A typed path that already points at the emulator keeps the user's spelling.
        if (value != CustomFolder && !SamePath(value.ModFolder, ModFolder)) ModFolder = value.ModFolder;
    }

    partial void OnModFolderChanged(string value)
    {
        settings.ModFolder = NullIfEmpty(value);
        UpdateModFolder();
        QueueSave();
    }

    partial void OnLibraryFolderChanged(string value)
    {
        settings.LibraryFolder = NullIfEmpty(value) ?? AppSettings.DefaultLibraryFolder;
        QueueSave();
    }

    partial void OnInstallSkylineChanged(bool value)
    {
        settings.InstallSkyline = value;
        QueueSave();
    }

    partial void OnInstallAutoRstbChanged(bool value)
    {
        settings.InstallAutoRstb = value;
        QueueSave();
    }

    partial void OnRomfsFolderChanged(string value)
    {
        settings.RomfsFolder = NullIfEmpty(value);
        UpdateRomfs();
        QueueSave();
    }

    private void UpdateModFolder()
    {
        var folder = settings.ModFolder;
        SelectedEmulator = emulators.FirstOrDefault(emulator => folder != null && SamePath(emulator.ModFolder, folder)) ?? CustomFolder;
        if (folder == null)
        {
            ModFolderStatus = new(StatusKind.Error, "Choose an emulator or the folder it loads Animal Crossing mods from.");
            return;
        }
        var target = Path.Combine(folder, AppSettings.DeployFolderName);
        ModFolderStatus = !SelectedEmulator.Found
            ? new(StatusKind.Warning, $"{SelectedEmulator.Name} wasn't found in its usual folder. For a portable install, browse to the {Emulators.TitleId} mod folder inside its user folder.")
            : Directory.Exists(folder)
                ? new(StatusKind.Ok, $"Mods deploy to {target}")
                : new(StatusKind.Info, $"Mods deploy to {target}. The folder is created on the first deploy.");
    }

    private void UpdateRomfs()
    {
        var folder = settings.RomfsFolder;
        RomfsStatus = folder == null
            ? new(StatusKind.Warning, "Not set. Merging mods and comparing their changes need the original game files.")
            : !Directory.Exists(folder)
                ? new(StatusKind.Error, "This folder doesn't exist.")
                : GameFiles.FindRomfs(folder) is { } romfs
                    ? new(StatusKind.Ok, $"romfs found in {romfs}")
                    : new(StatusKind.Error, "No romfs found. Choose the folder that contains Bcsv, Model and System.");
    }

    [RelayCommand]
    private async Task BrowseModFolder()
    {
        if (await PickFolderAsync("Choose the emulator's Animal Crossing mod folder", ModFolder) is { } folder) ModFolder = folder;
    }

    [RelayCommand]
    private async Task BrowseLibraryFolder()
    {
        if (await PickFolderAsync("Choose where to keep installed mods", LibraryFolder) is { } folder) LibraryFolder = folder;
    }

    [RelayCommand]
    private void ResetLibraryFolder() => LibraryFolder = AppSettings.DefaultLibraryFolder;

    [RelayCommand]
    private async Task BrowseRomfsFolder()
    {
        if (await PickFolderAsync("Choose your extracted romfs", RomfsFolder) is { } folder) RomfsFolder = folder;
    }

    [RelayCommand]
    private async Task OpenFolder(string? path)
    {
        // A folder that isn't created yet opens its closest existing parent.
        var folder = NullIfEmpty(path);
        while (folder != null && !Directory.Exists(folder)) folder = Path.GetDirectoryName(folder);
        if (folder == null) return;
        try { await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder)); }
        catch (Exception ex) { Error = $"Could not open {folder}: {ex.Message}"; }
    }

    private async Task<string?> PickFolderAsync(string title, string current)
    {
        try
        {
            var start = Directory.Exists(current) ? await storage.TryGetFolderFromPathAsync(current) : null;
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, SuggestedStartLocation = start });
            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
        catch (Exception ex) { Error = ex.Message; return null; }
    }

    private void QueueSave()
    {
        dirty = true;
        saveTimer.Stop();
        saveTimer.Start();
    }

    /// <summary>Writes pending changes now. The window calls this when it closes.</summary>
    public void Save()
    {
        saveTimer.Stop();
        if (!dirty) return;
        try
        {
            settings.Save();
            dirty = false;
            Error = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Error = $"Could not save settings: {ex.Message}"; }
    }

    // Explorer's "Copy as path" wraps paths in quotes.
    private static string? NullIfEmpty(string? path) => path?.Trim().Trim('"') is { Length: > 0 } trimmed ? trimmed : null;

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
}
