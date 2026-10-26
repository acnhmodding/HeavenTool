using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.FileTypes.BCSV;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Avalonia.ViewModels;

public partial class BCSVViewModel
{
    /// <summary>A matching cell, positioned by its row in the current sort and its column among the visible ones.</summary>
    private readonly record struct SearchMatch(BCSVRow Row, HeavenDataGridColumn Column, int RowPosition, int ColumnPosition)
    {
        public int CompareTo(int row, int column) =>
            RowPosition != row ? RowPosition.CompareTo(row) : ColumnPosition.CompareTo(column);
    }

    private readonly SearchHighlights highlights = new();
    private readonly DispatcherTimer searchTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly List<SearchMatch> matches = [];
    private readonly Dictionary<BCSVRow, int> rowPositions = [];
    private readonly Dictionary<HeavenDataGridColumn, int> columnPositions = [];
    private SearchMatch? currentMatch;
    private bool searchDirty;

    [ObservableProperty] private bool isSearchOpen;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private bool matchCase;
    [ObservableProperty] private bool matchWholeCell;
    [ObservableProperty] private string searchSummary = "";

    partial void OnIsSearchOpenChanged(bool value) => InvalidateSearch();
    partial void OnSearchTextChanged(string value) => InvalidateSearch();
    partial void OnMatchCaseChanged(bool value) => InvalidateSearch();
    partial void OnMatchWholeCellChanged(bool value) => InvalidateSearch();

    private bool CanSearch() => CanSave();
    private StringComparison SearchComparison => MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private bool IsMatch(string text) => MatchWholeCell
        ? string.Equals(text, SearchText, SearchComparison)
        : text.Contains(SearchText, SearchComparison);

    private void InitializeSearch()
    {
        searchTimer.Tick += (_, _) => RunSearch();
        DataGrid.Sorting += (_, _) => InvalidateSearch();
        DataGrid.ColumnReordered += (_, _) => InvalidateSearch();
        undoManager.Changed += (_, _) => InvalidateSearch();
    }

    // Results are rebuilt once typing pauses, or right away when the user navigates.
    private void InvalidateSearch()
    {
        searchTimer.Stop();
        if (!IsSearchOpen)
        {
            if (searchDirty || matches.Count > 0 || SearchSummary.Length > 0) RunSearch();
            return;
        }
        searchDirty = true;
        searchTimer.Start();
    }

    private void FlushSearch()
    {
        if (searchDirty) RunSearch();
    }

    private void RunSearch()
    {
        searchTimer.Stop();
        searchDirty = false;
        matches.Clear();
        rowPositions.Clear();
        columnPositions.Clear();
        if (!IsSearchOpen || !IsFileLoaded || SearchText.Length == 0)
        {
            currentMatch = null;
            highlights.Clear();
            SearchSummary = "";
            return;
        }

        // Only what the user can see is searched, in the order they see it.
        var columns = DataGrid.Columns.OfType<HeavenDataGridColumn>()
            .Where(column => column.IsVisible).OrderBy(column => column.DisplayIndex).ToArray();
        for (int i = 0; i < columns.Length; i++) columnPositions[columns[i]] = i;
        var cells = new HashSet<(BCSVRow, int)>();
        int position = 0;
        foreach (BCSVRow row in (IEnumerable?)DataGrid.CollectionView ?? Rows)
        {
            rowPositions[row] = position;
            for (int i = 0; i < columns.Length; i++)
            {
                var column = columns[i];
                if (!IsMatch(column.FormatValue(row[column.FieldIndex]))) continue;
                matches.Add(new SearchMatch(row, column, position, i));
                cells.Add((row, column.FieldIndex));
            }
            position++;
        }

        // Stay on the current match while it still matches.
        int index = currentMatch is { } current
            ? matches.FindIndex(match => match.Row == current.Row && match.Column == current.Column) : -1;
        currentMatch = index >= 0 ? matches[index] : null;
        highlights.Set(cells, currentMatch?.Row, currentMatch?.Column.FieldIndex ?? -1);
        SearchSummary = Summary(index);
    }

    private string Summary(int index) =>
        matches.Count == 0 ? "No matches"
        : index >= 0 ? $"{index + 1:N0} of {matches.Count:N0}"
        : matches.Count == 1 ? "1 match" : $"{matches.Count:N0} matches";

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void OpenSearch()
    {
        IsSearchOpen = true;
        var box = window.FindControl<TextBox>("SearchBox");
        box?.Focus();
        box?.SelectAll();
    }

    [RelayCommand]
    private void CloseSearch()
    {
        IsSearchOpen = false;
        DataGrid.Focus();
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void FindNext() => StepSearch(forward: true);

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void FindPrevious() => StepSearch(forward: false);

    private void StepSearch(bool forward)
    {
        IsSearchOpen = true;
        FlushSearch();
        if (matches.Count == 0 || !CommitEdits()) return;
        int index = -1;
        // Continue from the current cell, wrapping around at either end.
        if (CurrentCellPosition() is var (row, column))
            index = forward ? matches.FindIndex(match => match.CompareTo(row, column) > 0)
                : matches.FindLastIndex(match => match.CompareTo(row, column) < 0);
        if (index < 0) index = forward ? 0 : matches.Count - 1;
        GoToMatch(index);
    }

    private (int Row, int Column)? CurrentCellPosition()
    {
        if (DataGrid.SelectedItem is not BCSVRow row || !rowPositions.TryGetValue(row, out int position)) return null;
        // A hidden or unset current column sits before the row's first visible one.
        int column = DataGrid.CurrentColumn is HeavenDataGridColumn current
            && columnPositions.TryGetValue(current, out int index) ? index : -1;
        return (position, column);
    }

    private void GoToMatch(int index)
    {
        var match = matches[index];
        currentMatch = match;
        DataGrid.SelectedItem = match.Row;
        DataGrid.CurrentColumn = match.Column;
        DataGrid.ScrollIntoView(match.Row, match.Column);
        highlights.SetCurrent(match.Row, match.Column.FieldIndex);
        SearchSummary = Summary(index);
    }
}
