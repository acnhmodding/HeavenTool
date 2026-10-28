using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebananaAPI;
using GamebananaFile = GamebananaAPI.Data.GamebananaFile;
using HeavenTool.Avalonia.Settings;
using HeavenTool.Avalonia.Views;
using HeavenTool.Avalonia.Views.FileEditors;
using HeavenTool.ModManager.Library;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

public enum ModPage { Trending, Recent, Favorites, Installed }

public partial class MainWindowViewModel : ObservableObject
{
    private const int GameId = 7923;

    private readonly GamebananaClient client = new();
    // Library installs run one at a time so two never pick the same folder.
    private readonly SemaphoreSlim installLock = new(1, 1);
    private IMainView? view;
    private CancellationTokenSource? feedCancellation;
    private int nextFeedPage = 1;
    private List<InstalledMod> installed = [];
    private bool showRatedMods = AppSettings.Current.ShowRatedMods;
    private string libraryFolder = AppSettings.Current.LibraryFolder;

    public ObservableCollection<ModCardViewModel> Mods { get; } = [];

    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageIndex), nameof(IsOnlinePage), nameof(IsInstalledPage), nameof(SearchPlaceholder))]
    private ModPage page;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool hasMore;
    [ObservableProperty] private string? message;
    [ObservableProperty] private string? busyText;

    public bool IsOnlinePage => Page is ModPage.Trending or ModPage.Recent;
    public bool IsInstalledPage => Page == ModPage.Installed;
    public string SearchPlaceholder => IsOnlinePage ? "Search GameBanana..." : $"Filter {Page.ToString().ToLowerInvariant()} mods...";

    private ModLibrary Library => new(AppSettings.Current.LibraryFolder);

    public MainWindowViewModel()
    {
        if (Design.IsDesignMode) return;
        AppSettings.Saved += OnSettingsSaved;
    }

    /// <summary>Connects the window and loads the first page.</summary>
    public void Attach(IMainView view)
    {
        this.view = view;
        LoadInstalled();
        _ = ReloadAsync();
    }

    /// <summary>The page list's selection. Clearing the selection keeps the current page.</summary>
    public int PageIndex
    {
        get => (int)Page;
        set { if (value >= 0) Page = (ModPage)value; }
    }

    partial void OnPageChanged(ModPage value)
    {
        // Set the field so clearing the search doesn't reload the page a second time.
        searchText = "";
        OnPropertyChanged(nameof(SearchText));
        _ = ReloadAsync();
    }

    // Local pages filter as the user types; GameBanana searches when they press Enter.
    partial void OnSearchTextChanged(string value)
    {
        if (!IsOnlinePage) _ = ReloadAsync();
    }

    [RelayCommand]
    private Task Search() => ReloadAsync();

    private async Task ReloadAsync()
    {
        feedCancellation?.Cancel();
        feedCancellation = null;
        Mods.Clear();
        Message = null;
        HasMore = false;
        IsLoading = false;
        nextFeedPage = 1;

        var filter = SearchText.Trim();
        bool Matches(string? text) => filter.Length == 0 || text?.Contains(filter, StringComparison.CurrentCultureIgnoreCase) == true;
        switch (Page)
        {
            case ModPage.Trending or ModPage.Recent:
                await LoadMore();
                return;
            case ModPage.Favorites:
                foreach (var favorite in AppSettings.Current.Favorites.Where(favorite => Matches(favorite.Name) || Matches(favorite.Author)))
                    Mods.Add(WithState(new ModCardViewModel(this, favorite)));
                if (Mods.Count == 0)
                    Message = filter.Length > 0 ? "No favorites match." : "No favorites yet. Star a mod to keep it here.";
                return;
            case ModPage.Installed:
                foreach (var mod in installed.Where(mod => Matches(mod.Name) || Matches(mod.Author)))
                    Mods.Add(WithState(new ModCardViewModel(this, mod)));
                if (Mods.Count == 0)
                    Message = filter.Length > 0 ? "No installed mods match." : "No mods installed yet. Install one from Trending or Recent, or from a file.";
                return;
        }
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (!IsOnlinePage) return;
        feedCancellation?.Cancel();
        var cancellation = feedCancellation = new CancellationTokenSource();
        var sort = Page == ModPage.Trending ? SortType.Default : SortType.New;
        var query = SearchText.Trim();
        IsLoading = true;
        Message = null;
        try
        {
            var feed = await client.GetSubfeed(GameId, nextFeedPage, sort, Category.Mod, query, cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            nextFeedPage++;
            foreach (var record in feed?.Records ?? [])
            {
                if (record?.IdRow == null) continue;
                if (record.BHasContentRatings == true && !showRatedMods) continue;
                Mods.Add(WithState(new ModCardViewModel(this, record)));
            }
            HasMore = feed?.Metadata is { IsComplete: false };
            if (Mods.Count == 0 && !HasMore)
                Message = query.Length > 0 ? $"No mods match \"{query}\"." : "No mods found.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!cancellation.IsCancellationRequested) Message = $"Couldn't reach GameBanana: {ex.Message}";
        }
        finally
        {
            if (feedCancellation == cancellation) IsLoading = false;
        }
    }

    private ModCardViewModel WithState(ModCardViewModel card)
    {
        card.IsFavorite = card.ModId is { } id && AppSettings.Current.Favorites.Any(favorite => favorite.ModId == id);
        if (!card.IsLibraryCard) card.IsInstalled = card.ModId is { } modId && installed.Any(mod => mod.ModId == modId);
        return card;
    }

    private void LoadInstalled()
    {
        try { installed = Library.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            installed = [];
            Message = $"Couldn't read the mod library: {ex.Message}";
        }
        foreach (var card in Mods.Where(card => !card.IsLibraryCard))
            card.IsInstalled = card.ModId is { } modId && installed.Any(mod => mod.ModId == modId);
    }

    private void OnSettingsSaved()
    {
        var settings = AppSettings.Current;
        if (settings.LibraryFolder != libraryFolder)
        {
            libraryFolder = settings.LibraryFolder;
            LoadInstalled();
            if (IsInstalledPage) _ = ReloadAsync();
        }
        if (settings.ShowRatedMods != showRatedMods)
        {
            showRatedMods = settings.ShowRatedMods;
            if (IsOnlinePage) _ = ReloadAsync();
        }
    }

    internal async Task InstallAsync(ModCardViewModel card)
    {
        if (view == null || card.ModId is not { } modId || card.IsBusy) return;
        var cancellation = card.Cancellation = new CancellationTokenSource();
        var temporary = NewTemporaryFolder();
        card.IsBusy = true;
        card.IsProgressKnown = false;
        card.Status = "Getting the file list...";
        try
        {
            var files = (await client.GetDownloadPage(modId, cancellation.Token))?.Files ?? [];
            if (files.Length == 0)
            {
                if (card.CanOpenPage && await view.ConfirmAsync(card.Name, "This mod has no files to download on GameBanana. Open its page to see how to get it?", "Open page"))
                    await OpenPageAsync(card);
                return;
            }

            var file = files.Length == 1 ? files[0] : await ChooseFileAsync(card.Name, files);
            if (file == null) return;

            card.Status = $"Downloading {file.FileName}...";
            card.IsProgressKnown = file.Size > 0;
            card.Progress = 0;
            var archive = Path.Combine(temporary, ArchiveName(file.FileName));
            await using (var stream = File.Create(archive))
                await client.DownloadFile(file, stream, new Progress<long>(bytes => card.Progress = Math.Min(1, (double)bytes / file.Size)), cancellation.Token);
            await VerifyChecksumAsync(archive, file, cancellation.Token);

            var metadata = new InstalledMod
            {
                Name = card.Name,
                Author = card.Author,
                Version = file.Version,
                ModId = modId,
                FileId = file.IdRow,
                FileName = file.FileName,
                ProfileUrl = card.ProfileUrl,
                ImageUrl = card.PreviewUrl,
            };
            await InstallArchiveAsync(archive, temporary, metadata, card, cancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await view.ShowMessageAsync($"Couldn't install {card.Name}", ex.Message);
        }
        finally
        {
            card.IsBusy = false;
            card.Status = null;
            card.Cancellation = null;
            DeleteTemporaryFolder(temporary);
        }
    }

    [RelayCommand]
    private async Task InstallFromFile()
    {
        if (view == null) return;
        var picked = await view.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a mod archive",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Mod archives") { Patterns = ["*.zip", "*.7z", "*.rar", "*.tar", "*.tar.gz", "*.gz"] }],
        });
        if (picked.Count == 0 || picked[0].TryGetLocalPath() is not { } archive) return;

        var temporary = NewTemporaryFolder();
        var name = Path.GetFileNameWithoutExtension(archive);
        BusyText = $"Installing {name}...";
        try
        {
            var metadata = new InstalledMod { Name = name, FileName = Path.GetFileName(archive) };
            await InstallArchiveAsync(archive, temporary, metadata, null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await view.ShowMessageAsync($"Couldn't install {name}", ex.Message);
        }
        finally
        {
            BusyText = null;
            DeleteTemporaryFolder(temporary);
        }
    }

    /// <summary>Extracts an archive, asks which variants to keep when it holds more than one, and installs them in the library.</summary>
    private async Task InstallArchiveAsync(string archive, string temporary, InstalledMod metadata, ModCardViewModel? card, CancellationToken cancellation)
    {
        var extracted = Path.Combine(temporary, "files");
        if (card != null)
        {
            card.Status = "Extracting...";
            card.IsProgressKnown = true;
            card.Progress = 0;
        }
        var progress = new Progress<double>(share => { if (card != null) card.Progress = share; });
        await Task.Run(() => ModArchive.Extract(archive, extracted, progress, cancellation), cancellation);

        var variants = ModArchive.FindVariants(extracted);
        if (variants.Count == 0)
            throw new InvalidDataException("No romfs or exefs folder was found in this file, so Heaven Tool can't tell where its files go.");
        var chosen = variants.Count == 1 ? variants : await ChooseVariantsAsync(metadata.Name, variants);
        if (chosen == null) return;

        if (card != null)
        {
            card.Status = "Installing...";
            card.IsProgressKnown = false;
        }
        await installLock.WaitAsync(cancellation);
        try { await Task.Run(() => Library.Install(metadata, chosen), cancellation); }
        finally { installLock.Release(); }

        LoadInstalled();
        if (IsInstalledPage) await ReloadAsync();
    }

    private async Task<GamebananaFile?> ChooseFileAsync(string modName, GamebananaFile[] files)
    {
        var choices = files.Select(file =>
        {
            var details = new List<string> { ModCardViewModel.FormatSize(file.Size) };
            if (!string.IsNullOrWhiteSpace(file.Version)) details.Add("v" + file.Version.TrimStart('v', 'V'));
            if (file.DateAdded > 0) details.Add(ModCardViewModel.FormatDate(DateTimeOffset.FromUnixTimeSeconds(file.DateAdded)));
            details.Add($"{file.DownloadCount:N0} downloads");
            var description = string.IsNullOrWhiteSpace(file.Description) ? null : file.Description.Trim();
            return new Choice(description ?? file.FileName, description != null ? file.FileName : null, string.Join(" · ", details));
        }).ToList();
        var index = await view!.ChooseAsync(modName, "This mod has more than one file. Choose the one to install.", choices, "Download");
        return index is { } i ? files[i] : null;
    }

    private async Task<List<ModVariant>?> ChooseVariantsAsync(string modName, List<ModVariant> variants)
    {
        var choices = variants.Select(variant =>
        {
            var parts = new List<string>();
            if (variant.Romfs != null) parts.Add("romfs");
            if (variant.Exefs != null) parts.Add("exefs");
            var count = new[] { variant.Romfs, variant.Exefs }.Where(folder => folder != null)
                .Sum(folder => Directory.EnumerateFiles(folder!, "*", SearchOption.AllDirectories).Count());
            return new Choice(variant.Name.Length > 0 ? variant.Name : "Main files", null, $"{string.Join(" + ", parts)} · {count} file{(count == 1 ? "" : "s")}");
        }).ToList();
        // Archives split into parts usually mark the one every install needs.
        List<int> required = [.. variants.Select((variant, index) => (variant, index))
            .Where(pair => pair.variant.Name.Contains("required", StringComparison.OrdinalIgnoreCase)).Select(pair => pair.index)];
        var picked = await view!.ChooseManyAsync(modName,
            "This file holds more than one set of mod files: either versions to pick from, or parts to combine. Tick the ones to install; they're installed together as one mod.",
            choices, required.Count > 0 ? required : [0], "Install");
        return picked is { Count: > 0 } ? [.. picked.Order().Select(index => variants[index])] : null;
    }

    private static async Task VerifyChecksumAsync(string path, GamebananaFile file, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(file.Md5Checksum)) return;
        await using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(await MD5.HashDataAsync(stream, cancellation));
        if (!hash.Equals(file.Md5Checksum.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{file.FileName} didn't download correctly (its checksum doesn't match). Try again.");
    }

    internal void ToggleFavorite(ModCardViewModel card)
    {
        if (card.ModId is not { } modId) return;
        var favorites = AppSettings.Current.Favorites;
        card.IsFavorite = !card.IsFavorite;
        favorites.RemoveAll(favorite => favorite.ModId == modId);
        if (card.IsFavorite) favorites.Add(card.ToFavorite());
        // Keep the other cards for this mod in step.
        foreach (var other in Mods.Where(other => other != card && other.ModId == modId)) other.IsFavorite = card.IsFavorite;
        try { AppSettings.Current.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Message = $"Couldn't save favorites: {ex.Message}"; }
    }

    internal async Task OpenPageAsync(ModCardViewModel card)
    {
        if (view == null || !Uri.TryCreate(card.ProfileUrl, UriKind.Absolute, out var uri)) return;
        await view.Launcher.LaunchUriAsync(uri);
    }

    internal async Task OpenFolderAsync(ModCardViewModel card)
    {
        if (view == null || card.Installed is not { } mod || !Directory.Exists(mod.Folder)) return;
        await view.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(mod.Folder));
    }

    internal async Task RemoveAsync(ModCardViewModel card)
    {
        if (view == null || card.Installed is not { } mod) return;
        if (!await view.ConfirmAsync($"Remove {mod.Name}?", "Its files are deleted from the mod library.", "Remove")) return;
        try
        {
            await Task.Run(() => ModLibrary.Remove(mod));
            Mods.Remove(card);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await view.ShowMessageAsync($"Couldn't remove {mod.Name}", ex.Message);
        }
        LoadInstalled();
        if (Mods.Count == 0 && IsInstalledPage) await ReloadAsync();
    }

    private static string NewTemporaryFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "HeavenTool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void DeleteTemporaryFolder(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    // The server's name decides the archive type's extension, but must not leave the temporary folder.
    private static string ArchiveName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? "download" : name;
    }

    public void OpenBCSV() => new BCSVWindow().Show();

    public void OpenBFRES() => new BFRESWindow().Show();

    public void OpenSettings() => SettingsWindow.ShowSingle();
}
