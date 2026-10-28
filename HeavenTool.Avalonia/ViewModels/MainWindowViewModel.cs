using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebananaAPI;
using HeavenTool.Avalonia.Settings;
using HeavenTool.Avalonia.Views;
using HeavenTool.Avalonia.Views.FileEditors;
using HeavenTool.ModManager.Library;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageIndex), nameof(IsOnlinePage), nameof(IsInstalledPage), nameof(SearchPlaceholder), nameof(SortHint))]
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
        AppliedAuthor = AuthorFilter.Trim();

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
                foreach (var mod in installed.Where(mod => Matches(mod.Name) || Matches(mod.Author) || Matches(mod.Category) || Matches(mod.Subcategory) || mod.Tags.Any(Matches)))
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
        var sort = Page == ModPage.Trending ? SubfeedSort.Default : SubfeedSort.New;
        var query = SearchText.Trim();
        IsLoading = true;
        Message = null;
        try
        {
            var filter = await BuildFilterAsync(query, cancellation.Token);
            var feed = filter != null
                ? await client.GetModIndex(GameId, filter, nextFeedPage, cancellation: cancellation.Token)
                : await client.GetSubfeed(GameId, sort, nextFeedPage, query, cancellation.Token);
            if (cancellation.IsCancellationRequested) return;
            nextFeedPage++;
            foreach (var record in feed?.Records ?? [])
            {
                if (record.IdRow == null || record.HasContentRatings && !showRatedMods) continue;
                Mods.Add(WithState(new ModCardViewModel(this, record)));
            }
            HasMore = feed?.Metadata is { IsComplete: false };
            if (Mods.Count == 0 && !HasMore)
                Message = query.Length > 0 ? $"No mods match \"{query}\"{(filter != null ? " with these filters" : "")}."
                    : filter != null ? "No mods match these filters." : "No mods found.";
        }
        catch (OperationCanceledException) { }
        catch (AuthorNotFoundException ex)
        {
            if (!cancellation.IsCancellationRequested) Message = ex.Message;
        }
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

    public void OpenBCSV() => new BCSVWindow().Show();

    public void OpenBFRES() => new BFRESWindow().Show();

    public void OpenSettings() => SettingsWindow.ShowSingle();
}
