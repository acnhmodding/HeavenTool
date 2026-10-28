using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebananaAPI;
using GamebananaFile = GamebananaAPI.Data.GamebananaFile;
using HeavenTool.Avalonia.Settings;
using HeavenTool.Avalonia.Views;
using HeavenTool.Avalonia.Views.FileEditors;
using HeavenTool.ModManager.Deploy;
using HeavenTool.ModManager.Library;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

public enum ModPage { Trending, Recent, Favorites, Installed }

public partial class MainWindowViewModel : ObservableObject
{
    private const int GameId = 7923;

    private readonly GamebananaClient client = new();
    private static readonly HttpClient Http = CreateHttpClient();
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
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(DeployCommand))] private bool isDeploying;
    [ObservableProperty] private string? deployStatus;

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
                UpdateMoveState();
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
        UpdateDeployStatus();
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
        UpdateDeployStatus();
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

            // GameBanana lists an archive's files, so a save can be turned down before downloading it.
            card.Status = "Checking the file...";
            if (await GetFileListAsync(file, cancellation.Token) is { } paths && ModArchive.ContainsSaveData(paths) && !ModArchive.ContainsGameFiles(paths))
            {
                await ShowSaveFileWarningAsync(card.Name);
                return;
            }

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

    /// <summary>The paths inside a GameBanana file, or null when GameBanana can't list them, in which case the archive is checked once extracted.</summary>
    private async Task<string[]?> GetFileListAsync(GamebananaFile file, CancellationToken cancellation)
    {
        try { return await client.GetRawFileList(file.IdRow, cancellation); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellation.IsCancellationRequested) { return null; }
    }

    private Task ShowSaveFileWarningAsync(string name) =>
        view!.ShowMessageAsync($"{name} is a save file",
            "This download is an Animal Crossing save (it has files like main.dat and mainHeader.dat), not a mod of the game's files. Heaven Tool can't install save files yet.");

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
        // A save that comes with game file changes still installs those; a save on its own can't be installed yet.
        if (variants.Count == 0 && ModArchive.ContainsSaveData(extracted))
        {
            await ShowSaveFileWarningAsync(metadata.Name);
            return;
        }
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

    internal void SetEnabled(ModCardViewModel card)
    {
        if (card.Installed is not { } mod) return;
        var index = IndexOfInstalled(mod);
        if (index < 0 || installed[index].Enabled == card.IsModEnabled) return;
        mod.Enabled = installed[index].Enabled = card.IsModEnabled;
        SaveLoadOrder();
    }

    /// <summary>Moves a library mod past its neighbour on the page, so a filtered list still moves by one visible card.</summary>
    internal void Move(ModCardViewModel card, int offset)
    {
        var index = Mods.IndexOf(card);
        var target = index + offset;
        if (card.Installed is not { } mod || index < 0 || target < 0 || target >= Mods.Count || Mods[target].Installed is not { } neighbour) return;
        var from = IndexOfInstalled(mod);
        if (from < 0) return;
        var moved = installed[from];
        installed.RemoveAt(from);
        var at = IndexOfInstalled(neighbour);
        installed.Insert(offset < 0 ? at : at + 1, moved);
        Mods.Move(index, target);
        UpdateMoveState();
        SaveLoadOrder();
    }

    // Cards can outlive a reload of the library, so mods are matched by folder rather than by instance.
    private int IndexOfInstalled(InstalledMod mod) =>
        installed.FindIndex(other => string.Equals(other.Folder, mod.Folder, StringComparison.OrdinalIgnoreCase));

    private void UpdateMoveState()
    {
        for (var i = 0; i < Mods.Count; i++)
        {
            Mods[i].CanMoveUp = Mods[i].IsLibraryCard && i > 0;
            Mods[i].CanMoveDown = Mods[i].IsLibraryCard && i < Mods.Count - 1;
        }
    }

    private void SaveLoadOrder()
    {
        try { Library.SaveLoadOrder(installed); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Message = $"Couldn't save the load order: {ex.Message}"; }
        UpdateDeployStatus();
    }

    private bool CanDeploy() => !IsDeploying;

    [RelayCommand(CanExecute = nameof(CanDeploy))]
    private async Task Deploy()
    {
        if (view == null) return;
        var settings = AppSettings.Current;
        if (string.IsNullOrWhiteSpace(settings.ModFolder))
        {
            if (await view.ConfirmAsync("Choose where to deploy", "Heaven Tool deploys mods into your emulator's mod folder for Animal Crossing. Choose it in Settings first.", "Open Settings"))
                OpenSettings();
            return;
        }

        IsDeploying = true;
        BusyText = "Deploying...";
        var enabled = installed.Where(mod => mod.Enabled).ToList();
        try
        {
            var warnings = new List<string>();
            string? skyline = null;
            if (settings.InstallSkyline && enabled.Count > 0)
            {
                BusyText = "Getting Skyline...";
                try { skyline = await Skyline.GetExefsAsync(Path.Combine(AppSettings.Folder, "Skyline"), Http); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"Skyline couldn't be downloaded, so it wasn't deployed, and mods that need it won't load: {ex.Message}");
                }
            }

            var deployer = new ModDeployer
            {
                ModFolder = settings.ModFolder,
                FolderName = AppSettings.DeployFolderName,
                VanillaRomfs = GameFiles.FindRomfs(settings.RomfsFolder),
                SkylineExefs = skyline,
            };
            var progress = new Progress<string>(text => { if (IsDeploying) BusyText = text; });
            // Installs move library folders, so they wait for the deploy to finish reading them.
            await installLock.WaitAsync();
            DeployResult result;
            try { result = await Task.Run(() => deployer.Deploy(enabled, progress)); }
            finally { installLock.Release(); }
            result.Warnings.InsertRange(0, warnings);
            IsDeploying = false;
            BusyText = null;
            UpdateDeployStatus();
            await view.ShowMessageAsync(enabled.Count > 0 ? "Mods deployed" : "Nothing deployed", DescribeDeploy(result));
        }
        catch (Exception ex)
        {
            IsDeploying = false;
            BusyText = null;
            await view.ShowMessageAsync("Couldn't deploy", ex is IOException
                ? $"{ex.Message}\n\nIf the emulator is running the game, close it and deploy again."
                : ex.Message);
        }
        finally
        {
            IsDeploying = false;
            BusyText = null;
            UpdateDeployStatus();
        }
    }

    private static string DescribeDeploy(DeployResult result)
    {
        const int listed = 12;
        var text = new StringBuilder();
        if (result.ModCount == 0)
            text.Append($"No mods are turned on, so nothing is deployed to {result.Target} now.");
        else
        {
            text.Append($"{result.ModCount} mod{(result.ModCount == 1 ? "" : "s")} ({result.FileCount:N0} files) deployed to {result.Target}.");
            if (result.SkylineInstalled) text.Append(" Skyline is included.");
            if (result.ResourceSizeTableUpdated) text.Append(" The resource size table was updated to fit them.");
        }

        if (result.MergedFiles.Count > 0)
        {
            text.Append($"\n\nMerged {result.MergedFiles.Count} file{(result.MergedFiles.Count == 1 ? "" : "s")} that several mods change:");
            foreach (var file in result.MergedFiles.Take(listed)) text.Append($"\n• {file}");
            if (result.MergedFiles.Count > listed) text.Append($"\n• ...and {result.MergedFiles.Count - listed} more");
        }
        if (result.Conflicts.Count > 0)
        {
            text.Append($"\n\n{result.Conflicts.Count} file{(result.Conflicts.Count == 1 ? "" : "s")} couldn't be merged, so the mod higher in the list wins:");
            foreach (var conflict in result.Conflicts.Take(listed))
                text.Append($"\n• {conflict.Path}: {conflict.Winner} over {string.Join(", ", conflict.Overwritten.Distinct())}");
            if (result.Conflicts.Count > listed) text.Append($"\n• ...and {result.Conflicts.Count - listed} more");
        }
        foreach (var warning in result.Warnings) text.Append($"\n\n{warning}");
        return text.ToString();
    }

    /// <summary>Compares what was last deployed with the library's enabled mods, for the line under the Deploy button.</summary>
    private void UpdateDeployStatus()
    {
        var settings = AppSettings.Current;
        if (string.IsNullOrWhiteSpace(settings.ModFolder))
        {
            DeployStatus = "Choose a mod folder in Settings";
            return;
        }
        var enabled = installed.Where(mod => mod.Enabled).ToList();
        var marker = ModDeployer.ReadMarker(Path.Combine(settings.ModFolder, AppSettings.DeployFolderName));
        DeployStatus = marker == null
            ? enabled.Count == 0 ? "No mods turned on" : "Not deployed yet"
            : marker.Matches(enabled, settings.InstallSkyline) ? "Up to date" : "Changes not deployed";
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("HeavenTool");
        return http;
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
            UpdateMoveState();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await view.ShowMessageAsync($"Couldn't remove {mod.Name}", ex.Message);
        }
        LoadInstalled();
        // Drop it from the load order, so a mod installed later in a folder of the same name starts fresh.
        SaveLoadOrder();
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
