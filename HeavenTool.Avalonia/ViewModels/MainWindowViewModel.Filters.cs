using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GamebananaAPI;
using GamebananaAPI.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

/// <summary>A choice in the category filter. A root category also lists its subcategories' mods.</summary>
public record CategoryOption(int? Id, string Name, bool IsSubcategory = false)
{
    public static readonly CategoryOption Any = new(null, "Any category");
}

public record SortOption(ModSort Sort, string Name);

// The search filters of the online pages. GameBanana's subfeed ignores filters, so a filtered search lists Mod/Index instead.
public partial class MainWindowViewModel
{
    public ObservableCollection<CategoryOption> Categories { get; } = [CategoryOption.Any];

    private static readonly SortOption[] SortList =
    [
        new(ModSort.Default, "Default"),
        new(ModSort.Newest, "Newest"),
        new(ModSort.LatestUpdated, "Recently updated"),
        new(ModSort.MostLiked, "Most liked"),
        new(ModSort.MostDownloaded, "Most downloaded"),
        new(ModSort.MostViewed, "Most viewed"),
        new(ModSort.Alphabetically, "Name (A to Z)"),
    ];

    public IReadOnlyList<SortOption> Sorts => SortList;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ActiveFilterCount), nameof(HasActiveFilters), nameof(SortHint))]
    private CategoryOption selectedCategory = CategoryOption.Any;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ActiveFilterCount), nameof(HasActiveFilters), nameof(SortHint))]
    private SortOption? selectedSort = SortList[0];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ActiveFilterCount), nameof(HasActiveFilters), nameof(SortHint))]
    private bool featuredOnly;
    /// <summary>The author box's text, applied on Enter or when the filters close.</summary>
    [ObservableProperty] private string authorFilter = "";
    [ObservableProperty] private bool isLoadingCategories;

    // The author the listing uses, and the member it was last matched to.
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ActiveFilterCount), nameof(HasActiveFilters), nameof(SortHint))]
    private string appliedAuthor = "";
    private string? resolvedAuthorName;
    private long? resolvedAuthorId;
    private bool categoriesLoaded;
    private bool suppressFilterReload;

    private ModSort Sort => SelectedSort?.Sort ?? ModSort.Default;

    public int ActiveFilterCount =>
        (SelectedCategory.Id != null ? 1 : 0) + (AppliedAuthor.Length > 0 ? 1 : 0) + (FeaturedOnly ? 1 : 0) + (Sort != ModSort.Default ? 1 : 0);

    public bool HasActiveFilters => ActiveFilterCount > 0;

    // Filters that only Mod/Index understands. A sort is one too, but doesn't change which mods are listed.
    private bool NarrowsListing => SelectedCategory.Id != null || AppliedAuthor.Length > 0 || FeaturedOnly;

    public string SortHint => Sort == ModSort.Default
        ? NarrowsListing && Page == ModPage.Trending
            ? "Trending can't be filtered, so filtered mods are listed most liked first."
            : Page == ModPage.Trending ? "Default lists trending mods." : "Default lists the newest mods first."
        : "";

    partial void OnSelectedCategoryChanged(CategoryOption value)
    {
        // A combo box can clear its selection while its items change.
        if (value == null!) selectedCategory = CategoryOption.Any;
        else ReloadForFilters();
    }

    partial void OnSelectedSortChanged(SortOption? value) => ReloadForFilters();
    partial void OnFeaturedOnlyChanged(bool value) => ReloadForFilters();

    private void ReloadForFilters()
    {
        if (!suppressFilterReload && IsOnlinePage) _ = ReloadAsync();
    }

    /// <summary>Applies the author box after it was edited, from Enter or the filters closing.</summary>
    [RelayCommand]
    private Task ApplyFilters() =>
        AuthorFilter.Trim() == AppliedAuthor ? Task.CompletedTask : ReloadAsync();

    [RelayCommand]
    private Task ClearFilters()
    {
        suppressFilterReload = true;
        SelectedCategory = CategoryOption.Any;
        SelectedSort = Sorts[0];
        FeaturedOnly = false;
        AuthorFilter = "";
        suppressFilterReload = false;
        return ReloadAsync();
    }

    /// <summary>Lists the mods of a card's author, picked from the card so the name needs no lookup.</summary>
    internal Task FilterByAuthorAsync(ModCardViewModel card)
    {
        if (card.Author == null || card.AuthorId is not { } id) return Task.CompletedTask;
        resolvedAuthorName = card.Author;
        resolvedAuthorId = id;
        AuthorFilter = card.Author;
        return ReloadAsync();
    }

    /// <summary>Fills the category list the first time the filters open.</summary>
    public async Task LoadCategoriesAsync()
    {
        if (categoriesLoaded || IsLoadingCategories) return;
        IsLoadingCategories = true;
        try
        {
            var roots = await client.GetModCategories(GameId);
            var subcategories = await Task.WhenAll(roots.Select(root => root.CategoryCount > 0
                ? client.GetModSubcategories(root.IdRow)
                : Task.FromResult<ModCategory[]>([])));
            // Added after "Any category", which stays selected: replacing it would leave the combo box blank.
            for (var i = 0; i < roots.Length; i++)
            {
                Categories.Add(new CategoryOption(roots[i].IdRow, roots[i].Name));
                foreach (var subcategory in subcategories[i])
                    Categories.Add(new CategoryOption(subcategory.IdRow, subcategory.Name, true));
            }
            categoriesLoaded = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // The list stays at "Any category", and opening the filters again tries once more.
        }
        finally
        {
            IsLoadingCategories = false;
        }
    }

    /// <summary>The filter Mod/Index needs, or null when the subfeed can list the page as it is.</summary>
    private async Task<ModFilter?> BuildFilterAsync(string query, CancellationToken cancellation)
    {
        if (!NarrowsListing && Sort == ModSort.Default) return null;
        var filter = new ModFilter
        {
            Name = query,
            CategoryId = SelectedCategory.Id,
            FeaturedOnly = FeaturedOnly,
            // Mod/Index has no trending order, so filtered trending mods fall back to the most liked.
            Sort = Sort == ModSort.Default && Page == ModPage.Trending ? ModSort.MostLiked : Sort,
        };
        if (AppliedAuthor.Length > 0)
            filter.SubmitterId = await ResolveAuthorAsync(AppliedAuthor, cancellation)
                ?? throw new AuthorNotFoundException(AppliedAuthor);
        return filter;
    }

    // GameBanana's member search matches any word, so only a name that matches exactly, or else contains the text, counts.
    private async Task<long?> ResolveAuthorAsync(string name, CancellationToken cancellation)
    {
        if (string.Equals(resolvedAuthorName, name, StringComparison.OrdinalIgnoreCase)) return resolvedAuthorId;
        var members = await client.FindMembers(name, cancellation);
        var member = members.FirstOrDefault(member => string.Equals(member.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? members.FirstOrDefault(member => member.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true);
        resolvedAuthorName = name;
        resolvedAuthorId = member?.IdRow;
        return resolvedAuthorId;
    }

    private sealed class AuthorNotFoundException(string name) : Exception($"No GameBanana member is named \"{name}\".");
}
