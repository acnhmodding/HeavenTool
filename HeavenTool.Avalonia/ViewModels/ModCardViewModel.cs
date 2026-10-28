using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebananaAPI.Data;
using HeavenTool.Avalonia.Settings;
using HeavenTool.ModManager.Library;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

/// <summary>A mod in the main window's list: a GameBanana submission, a favorite or an installed mod.</summary>
public partial class ModCardViewModel : ObservableObject
{
    private readonly MainWindowViewModel owner;

    public int? ModId { get; }
    public string Name { get; }
    public string? Author { get; }
    /// <summary>The GameBanana member who submitted it, known on the online pages.</summary>
    public long? AuthorId { get; }
    public string? ImageUrl { get; }
    public string? PreviewUrl { get; }
    public string? ProfileUrl { get; }
    public string? Details { get; }
    /// <summary>The tags of an installed mod, shown over its details.</summary>
    public string? TagsTip { get; }

    /// <summary>The library copy, on the Installed page.</summary>
    public InstalledMod? Installed { get; }

    public bool HasImage => ImageUrl != null;
    public bool CanFavorite => ModId != null;
    public bool CanInstall => ModId != null && Installed == null && !IsBusy && !HasUpdate;
    public bool CanCancel => ModId != null && IsBusy;
    public bool HasUpdate => Update != null;
    public bool CanUpdate => HasUpdate && !IsBusy;
    public string? UpdateText => Update?.Description;
    public bool CanOpenPage => ProfileUrl != null;
    public bool CanFilterByAuthor => AuthorId != null && Author != null;
    public bool HasPlainAuthor => !string.IsNullOrEmpty(Author) && !CanFilterByAuthor;
    public bool IsLibraryCard => Installed != null;
    public bool IsTurnedOff => IsLibraryCard && !IsModEnabled;
    public string InstallIcon => IsInstalled ? "RotateSolid" : "DownloadSolid";

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(InstallIcon))]
    public partial bool IsInstalled { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanCancel), nameof(CanInstall), nameof(CanUpdate))]
    public partial bool IsBusy { get; set; }

    /// <summary>A newer GameBanana file for the installed mod, found by the update check.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasUpdate), nameof(CanUpdate), nameof(CanInstall), nameof(UpdateText))]
    public partial ModUpdate? Update { get; set; }
    [ObservableProperty]
    public partial bool IsProgressKnown { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }
    internal CancellationTokenSource? Cancellation { get; set; }

    /// <summary>Whether a library mod is deployed.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsTurnedOff))]
    public partial bool IsModEnabled { get; set; }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    public partial bool CanMoveUp { get; set; }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    public partial bool CanMoveDown { get; set; }

    public ModCardViewModel(MainWindowViewModel owner, ModRecord record)
    {
        this.owner = owner;
        ModId = record.IdRow;
        Name = record.Name ?? "Untitled mod";
        Author = record.Submitter?.Name;
        AuthorId = record.Submitter is { IdRow: > 0 } submitter ? submitter.IdRow : null;
        ProfileUrl = record.ProfileUrl?.ToString();
        var image = record.PreviewContent?.Screenshot;
        if (image?.BaseUrl != null && image.File != null)
        {
            ImageUrl = $"{image.BaseUrl}/{image.File220 ?? image.File}";
            // The full screenshot can be several hundred KB; the 530 pixel one loads fast enough for a tooltip.
            PreviewUrl = $"{image.BaseUrl}/{image.File530 ?? image.File}";
        }

        var details = new List<string>();
        if (CategoryText(record.Category?.Name, record.SubCategory?.Name) is { } category) details.Add(category);
        details.Add(Plural(record.LikeCount, "like"));
        if (record.ViewCount is { } views) details.Add(Plural(views, "view"));
        if (record.DateModified > 0) details.Add("Updated " + FormatDate(DateTimeOffset.FromUnixTimeSeconds(record.DateModified)));
        else if (record.DateAdded > 0) details.Add("Added " + FormatDate(DateTimeOffset.FromUnixTimeSeconds(record.DateAdded)));
        Details = string.Join(" · ", details);
    }

    public ModCardViewModel(MainWindowViewModel owner, FavoriteMod favorite)
    {
        this.owner = owner;
        ModId = favorite.ModId;
        Name = favorite.Name;
        Author = favorite.Author;
        ImageUrl = PreviewUrl = favorite.ImageUrl;
        ProfileUrl = favorite.ProfileUrl;
    }

    public ModCardViewModel(MainWindowViewModel owner, InstalledMod mod)
    {
        this.owner = owner;
        Installed = mod;
        ModId = mod.ModId;
        Name = mod.Name;
        Author = mod.Author;
        ImageUrl = PreviewUrl = mod.ImageUrl;
        ProfileUrl = mod.ProfileUrl;
        IsInstalled = true;
        IsModEnabled = mod.Enabled;

        var details = new List<string>();
        if (CategoryText(mod.Category, mod.Subcategory) is { } category) details.Add(category);
        if (!string.IsNullOrEmpty(mod.Variant)) details.Add(mod.Variant);
        if (!string.IsNullOrEmpty(mod.FileName)) details.Add(mod.FileName);
        details.Add("Installed " + FormatDate(mod.InstalledAt));
        Details = string.Join(" · ", details);
        if (mod.Tags.Count > 0) TagsTip = "Tags: " + string.Join(", ", mod.Tags);
    }

    /// <summary>The fields a favorite keeps, so the Favorites page works without asking GameBanana.</summary>
    public FavoriteMod ToFavorite() => new(ModId!.Value, Name, Author, PreviewUrl, ProfileUrl);

    [RelayCommand] private Task Install() => owner.InstallAsync(this);
    [RelayCommand] private Task UpdateMod() => owner.InstallAsync(this, Update);
    [RelayCommand] private void CancelInstall() => Cancellation?.Cancel();
    [RelayCommand] private void ToggleFavorite() => owner.ToggleFavorite(this);
    [RelayCommand] private Task OpenPage() => owner.OpenPageAsync(this);
    [RelayCommand] private Task FilterByAuthor() => owner.FilterByAuthorAsync(this);
    [RelayCommand] private Task OpenFolder() => owner.OpenFolderAsync(this);
    [RelayCommand] private Task Remove() => owner.RemoveAsync(this);
    [RelayCommand(CanExecute = nameof(CanMoveUp))] private void MoveUp() => owner.Move(this, -1);
    [RelayCommand(CanExecute = nameof(CanMoveDown))] private void MoveDown() => owner.Move(this, 1);

    partial void OnIsModEnabledChanged(bool value) => owner.SetEnabled(this);

    private static string? CategoryText(string? category, string? subcategory) =>
        string.IsNullOrEmpty(category) ? null : string.IsNullOrEmpty(subcategory) ? category : $"{category} › {subcategory}";

    private static string Plural(long count, string noun) =>
        $"{FormatCount(count)} {noun}{(count == 1 ? "" : "s")}";

    private static string FormatCount(long count) => count switch
    {
        >= 1_000_000 => (count / 1_000_000d).ToString("0.#", CultureInfo.CurrentCulture) + "M",
        >= 1_000 => (count / 1_000d).ToString("0.#", CultureInfo.CurrentCulture) + "K",
        _ => count.ToString(CultureInfo.CurrentCulture),
    };

    internal static string FormatDate(DateTimeOffset date) => date.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    internal static string FormatSize(long bytes) => bytes switch
    {
        >= 1 << 30 => (bytes / (double)(1 << 30)).ToString("0.0", CultureInfo.CurrentCulture) + " GB",
        >= 1 << 20 => (bytes / (double)(1 << 20)).ToString("0.0", CultureInfo.CurrentCulture) + " MB",
        >= 1 << 10 => (bytes / (double)(1 << 10)).ToString("0", CultureInfo.CurrentCulture) + " KB",
        _ => bytes.ToString(CultureInfo.CurrentCulture) + " B",
    };
}
