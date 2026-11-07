using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.Settings;
using HeavenTool.ModManager.Library;
using HeavenTool.ModManager.NookKit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

public enum StatusKind { Ok, Info, Warning, Error }

public sealed record SettingStatus(StatusKind Kind, string Text);

/// <summary>One mod's line in the NookKit export results: the kinds of patch it got, and what couldn't be exported.</summary>
public sealed record NookKitResult(string Name, string Summary, IReadOnlyList<string> Notes)
{
    public static NookKitResult From(NookKitModExport export)
    {
        // Each file is NookKit/<kind>/<name>.json.
        var kinds = export.Files.Select(file => Path.GetFileName(Path.GetDirectoryName(file))).ToList();
        return new(export.Name, kinds.Count > 0 ? string.Join(", ", kinds) : "Nothing to export", export.Notes);
    }
}

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

    [ObservableProperty]
    public partial EmulatorInstall SelectedEmulator { get; set; } = CustomFolder;

    [ObservableProperty]
    public partial string ModFolder { get; set; }

    [ObservableProperty]
    public partial SettingStatus ModFolderStatus { get; set; } = null!;

    [ObservableProperty]
    public partial string LibraryFolder { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(RstbFixOff))]
    public partial bool InstallSkyline { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(RstbFixOff))]
    public partial bool InstallAutoRstb { get; set; }

    [ObservableProperty]
    public partial bool ShowRatedMods { get; set; }

    [ObservableProperty]
    public partial string RomfsFolder { get; set; }

    [ObservableProperty]
    public partial SettingStatus RomfsStatus { get; set; } = null!;

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial SettingStatus? NookKitStatus { get; set; }

    /// <summary>The folder the last NookKit export went to.</summary>
    [ObservableProperty]
    public partial string? NookKitFolder { get; set; }

    public ObservableCollection<NookKitResult> NookKitResults { get; } = [];

    public SettingsViewModel(AppSettings settings, IStorageProvider storage, ILauncher launcher)
    {
        this.settings = settings;
        this.storage = storage;
        this.launcher = launcher;
        EmulatorOptions = [.. emulators, CustomFolder];
        ModFolder = settings.ModFolder ?? "";
        LibraryFolder = settings.LibraryFolder;
        InstallSkyline = settings.InstallSkyline;
        InstallAutoRstb = settings.InstallAutoRstb;
        ShowRatedMods = settings.ShowRatedMods;
        RomfsFolder = settings.RomfsFolder ?? "";
        saveTimer.Tick += (_, _) => Save();
        UpdateModFolder();
        UpdateRomfs();
    }

    partial void OnSelectedEmulatorChanged(EmulatorInstall value)
    {
        // A typed path that already points at the emulator keeps the user's spelling.
        if (value != CustomFolder && !Emulators.SamePath(value.ModFolder, ModFolder)) ModFolder = value.ModFolder;
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

    partial void OnShowRatedModsChanged(bool value)
    {
        settings.ShowRatedMods = value;
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
        SelectedEmulator = emulators.FirstOrDefault(emulator => folder != null && Emulators.SamePath(emulator.ModFolder, folder)) ?? CustomFolder;
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

    /// <summary>Writes each enabled library mod's changes as NookKit patches, in the NookKit folder of a folder the user picks.</summary>
    [RelayCommand]
    private async Task GenerateNookKit()
    {
        if (GameFiles.FindRomfs(settings.RomfsFolder) is not { } romfs)
        {
            NookKitStatus = new(StatusKind.Error, "Set the extracted romfs in Game Settings first. Mods are compared with the original files.");
            return;
        }
        if (await PickFolderAsync("Choose where to write the NookKit folder", NookKitFolder ?? "") is not { } output) return;

        NookKitResults.Clear();
        NookKitStatus = new(StatusKind.Info, "Comparing the enabled mods with the game files…");
        try
        {
            var library = settings.LibraryFolder;
            var results = await Task.Run(() =>
            {
                var mods = new ModLibrary(library).Load().Where(mod => mod.Enabled).ToList();
                return new NookKitExporter(romfs).Export(mods, output);
            });
            NookKitFolder = output;
            foreach (var result in results) NookKitResults.Add(NookKitResult.From(result));
            var written = results.Sum(result => result.Files.Count);
            NookKitStatus = results.Count == 0
                ? new(StatusKind.Warning, "No mods are enabled in the library.")
                : new(written == 0 ? StatusKind.Warning : StatusKind.Ok,
                    $"Wrote {written} file{(written == 1 ? "" : "s")} for {results.Count} mod{(results.Count == 1 ? "" : "s")} to {Path.Combine(output, "NookKit")}");
        }
        catch (Exception ex) { NookKitStatus = new(StatusKind.Error, $"Could not generate the patches: {ex.Message}"); }
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
}
