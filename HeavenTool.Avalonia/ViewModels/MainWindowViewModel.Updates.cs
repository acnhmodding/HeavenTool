using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebananaAPI.Data;
using HeavenTool.ModManager.Library;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

/// <summary>
/// A newer GameBanana file for an installed mod. Several files when GameBanana doesn't say which one replaces the installed file.
/// </summary>
public sealed record ModUpdate(InstalledMod Mod, GamebananaFile[] Files, string? Version)
{
    public string Description => Files.Length > 1
        ? "Update available: the installed file was removed, so choose its replacement"
        : string.Join(" · ", new[]
        {
            string.IsNullOrWhiteSpace(Version) ? "Update available" : "Update to v" + Version.Trim().TrimStart('v', 'V'),
            Files[0].DateAdded > 0 ? "added " + ModCardViewModel.FormatDate(DateTimeOffset.FromUnixTimeSeconds(Files[0].DateAdded)) : null,
        }.Where(part => part != null));
}

// Checking the installed GameBanana mods for newer files. Installing one goes through InstallAsync, over the mod it updates.
public partial class MainWindowViewModel
{
    // The update found for each library mod, by folder.
    private readonly Dictionary<string, ModUpdate> updates = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasUpdates))] private int updateCount;
    [ObservableProperty] private string? updateStatus;

    public bool HasUpdates => UpdateCount > 0;

    /// <summary>Asks GameBanana for the files of every installed mod that came from it, a few at a time.</summary>
    [RelayCommand]
    private async Task CheckForUpdates()
    {
        var ids = installed.Select(mod => mod.ModId).OfType<int>().Distinct().ToList();
        if (ids.Count == 0)
        {
            UpdateStatus = installed.Count > 0 ? "None of the installed mods came from GameBanana, so there's nothing to check." : null;
            return;
        }

        UpdateStatus = "Checking for updates...";
        var profiles = new ConcurrentDictionary<int, ModProfile>();
        string? error = null;
        await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (id, cancellation) =>
        {
            try { if (await client.GetProfilePage(id, cancellation) is { } profile) profiles[id] = profile; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or Newtonsoft.Json.JsonException) { error = ex.Message; }
        });

        // The library can change while GameBanana answers, so the mods are matched up afterwards.
        foreach (var mod in installed)
        {
            if (mod.ModId is not { } id || !profiles.TryGetValue(id, out var profile)) continue;
            if (FindUpdate(mod, profile) is { } update) updates[mod.Folder] = update;
            else updates.Remove(mod.Folder);
        }
        ShowUpdates();

        UpdateStatus = profiles.IsEmpty && error != null ? $"Couldn't check for updates: {error}"
            : (updates.Count switch { 0 => "Every mod is up to date.", 1 => "1 mod has an update.", var count => $"{count} mods have updates." })
              + (profiles.Count < ids.Count ? " Some mods couldn't be checked." : "");
    }

    /// <summary>Installs every update found, one after another. Files that need a choice ask as they come.</summary>
    [RelayCommand]
    private async Task UpdateAll()
    {
        foreach (var update in updates.Values.ToList())
        {
            // An earlier update can reload the library, so look again for what's still to do and its card on the page.
            if (!updates.TryGetValue(update.Mod.Folder, out var current) || current != update) continue;
            var card = Mods.FirstOrDefault(card => card.Installed is { } mod && SameFolder(mod, update.Mod)) ?? WithState(new ModCardViewModel(this, update.Mod));
            if (!card.IsBusy) await InstallAsync(card, update);
        }
    }

    /// <summary>
    /// The newer file in the same line as the installed one. GameBanana keeps old versions next to new ones, and variants next
    /// to each other, so files are matched by name, without the version and the suffix GameBanana adds. When the installed file
    /// is gone, whatever matches it replaces it, or every file when nothing does and there's more than one.
    /// </summary>
    public static ModUpdate? FindUpdate(InstalledMod mod, ModProfile profile)
    {
        var files = profile.Files.Where(file => !file.IsArchived && file.DownloadUrl != null && file.IdRow != mod.FileId).ToArray();
        var installedFile = profile.Files.FirstOrDefault(file => file.IdRow == mod.FileId);
        var stem = FileStem(mod.FileName ?? installedFile?.FileName ?? "");
        var sameLine = files.Where(file => stem.Length > 0 && FileStem(file.FileName) == stem).OrderByDescending(file => file.DateAdded).ToArray();

        GamebananaFile[] found = installedFile is { IsArchived: false }
            ? [.. sameLine.Where(file => file.DateAdded > installedFile.DateAdded).Take(1)]
            : sameLine.Length > 0 ? [sameLine[0]]
            : [.. files.OrderByDescending(file => file.DateAdded)];
        return found.Length == 0 ? null : new ModUpdate(mod, found, found.Length == 1 ? VersionOf(found[0], profile) : null);
    }

    /// <summary>A file's version: its own, or the mod's when it's among the newest files, which are often uploaded together.</summary>
    private static string? VersionOf(GamebananaFile file, ModProfile profile) =>
        !string.IsNullOrWhiteSpace(file.Version) ? file.Version
        : !string.IsNullOrWhiteSpace(profile.Version) && profile.Files.All(other => other.DateAdded <= file.DateAdded) ? profile.Version
        : null;

    // "chubby_mod_v1_2__ffedc.rar", "reactioncat2-0-0.zip" and "speedfix-v1-4-2-acnh.zip" lose the suffix GameBanana adds to a
    // repeated name and their versions; other digits stay, so "60fps-emulator" and "120fps-emulator" are told apart.
    private static string FileStem(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        if (name.EndsWith(".tar")) name = name[..^4];
        name = Regex.Replace(name, "_[0-9a-f]{5}$", "").TrimEnd('_', '-', '.', ' ');
        name = Regex.Replace(name, @"(?<=^|(?<!\d)[-_. ])(v?\d+([-_.]\d+)+[a-z]?|v\d+[a-z]?)(?=[-_. ]|$)", "");
        name = Regex.Replace(name, @"v?\d+([-_.]\d+)*[a-z]?$", "");
        return new string([.. name.Where(char.IsLetterOrDigit)]);
    }

    // Drops what an install or a removal made out of date, and shows the rest on the cards.
    private void ShowUpdates()
    {
        foreach (var (folder, update) in updates.ToList())
            if (!installed.Any(mod => string.Equals(mod.Folder, folder, StringComparison.OrdinalIgnoreCase) && mod.FileId == update.Mod.FileId))
                updates.Remove(folder);
        UpdateCount = updates.Count;
        foreach (var card in Mods) card.Update = UpdateFor(card);
    }

    // A library card shows its own update; a GameBanana card shows the update of the mod installed from it.
    private ModUpdate? UpdateFor(ModCardViewModel card) =>
        card.Installed is { } mod ? updates.GetValueOrDefault(mod.Folder)
        : card.ModId is { } id ? updates.Values.FirstOrDefault(update => update.Mod.ModId == id)
        : null;

    private static bool SameFolder(InstalledMod a, InstalledMod b) => string.Equals(a.Folder, b.Folder, StringComparison.OrdinalIgnoreCase);
}
