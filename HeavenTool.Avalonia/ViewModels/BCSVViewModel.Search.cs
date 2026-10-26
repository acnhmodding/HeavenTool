using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.FileTypes.BCSV;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private DataGridCollectionView? rowsView;
    private HashSet<BCSVRow> hiddenRows = [];
    private string? filterKey;

    [ObservableProperty] private bool isSearchOpen;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private bool matchCase;
    [ObservableProperty] private bool matchWholeCell;
    [ObservableProperty] private string searchSummary = "";
    [ObservableProperty] private bool isReplaceOpen;
    [ObservableProperty] private string replaceText = "";
    [ObservableProperty] private bool filterMatches;

    partial void OnIsSearchOpenChanged(bool value) => InvalidateSearch();
    partial void OnSearchTextChanged(string value) => InvalidateSearch();
    partial void OnMatchCaseChanged(bool value) => InvalidateSearch();
    partial void OnMatchWholeCellChanged(bool value) => InvalidateSearch();
    partial void OnFilterMatchesChanged(bool value)
    {
        InvalidateSearch();
        FlushSearch();
    }

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
            ApplyFilter(null, []);
            return;
        }

        // Only what the user can see is searched, in the order they see it.
        var columns = DataGrid.Columns.OfType<HeavenDataGridColumn>()
            .Where(column => column.IsVisible).OrderBy(column => column.DisplayIndex).ToArray();
        for (int i = 0; i < columns.Length; i++) columnPositions[columns[i]] = i;
        var cells = new HashSet<(BCSVRow, int)>();
        bool IsCellMatch(BCSVRow row, int i)
        {
            var column = columns[i];
            if (!IsMatch(column.FormatValue(row[column.FieldIndex]))) return false;
            cells.Add((row, column.FieldIndex));
            return true;
        }

        // Filtering checks hidden rows too, so rows that match again come back.
        Dictionary<BCSVRow, List<int>>? rowHits = null;
        if (FilterMatches || hiddenRows.Count > 0)
        {
            rowHits = [];
            foreach (var row in Rows)
            {
                List<int>? hits = null;
                for (int i = 0; i < columns.Length; i++)
                    if (IsCellMatch(row, i)) (hits ??= []).Add(i);
                if (hits != null) rowHits[row] = hits;
            }
            ApplyFilter(FilterMatches ? rowHits : null, columns);
        }

        int position = 0;
        foreach (BCSVRow row in (IEnumerable?)DataGrid.CollectionView ?? Rows)
        {
            rowPositions[row] = position;
            if (rowHits == null)
            {
                for (int i = 0; i < columns.Length; i++)
                    if (IsCellMatch(row, i)) matches.Add(new SearchMatch(row, columns[i], position, i));
            }
            else if (rowHits.TryGetValue(row, out var hits))
                foreach (int i in hits) matches.Add(new SearchMatch(row, columns[i], position, i));
            position++;
        }

        // Stay on the current match while it still matches.
        int index = currentMatch is { } current
            ? matches.FindIndex(match => match.Row == current.Row && match.Column == current.Column) : -1;
        currentMatch = index >= 0 ? matches[index] : null;
        highlights.Set(cells, currentMatch?.Row, currentMatch?.Column.FieldIndex ?? -1);
        SearchSummary = Summary(index);
    }

    private bool IsRowShown(object item) => !hiddenRows.Contains((BCSVRow)item);

    // A new search hides every row without a match. After that, edits only bring rows back, so a row
    // being worked on never vanishes and new rows stay in view.
    private void ApplyFilter(Dictionary<BCSVRow, List<int>>? rowHits, HeavenDataGridColumn[] columns)
    {
        bool changed;
        if (rowHits == null)
        {
            filterKey = null;
            changed = hiddenRows.Count > 0;
            hiddenRows.Clear();
        }
        else
        {
            string key = string.Join('\0', SearchText, MatchCase, MatchWholeCell,
                string.Join(',', columns.Select(column => column.FieldIndex).Order()));
            if (key != filterKey)
            {
                filterKey = key;
                var hidden = Rows.Where(row => !rowHits.ContainsKey(row)).ToHashSet();
                changed = !hidden.SetEquals(hiddenRows);
                hiddenRows = hidden;
            }
            else changed = hiddenRows.RemoveWhere(rowHits.ContainsKey) > 0;
        }
        if (!changed) return;
        RefreshFilter();
        StatusText = hiddenRows.Count == 0 ? $"Showing all {RowCount(Rows.Count)}"
            : $"Showing {Rows.Count - hiddenRows.Count:N0} of {RowCount(Rows.Count)}";
    }

    // Rows that undo or redo changed come back into view, so the change can be seen.
    private void ShowRows(List<BCSVRow> rows)
    {
        int count = hiddenRows.Count;
        if (count == 0) return;
        hiddenRows.ExceptWith(rows);
        if (hiddenRows.Count != count) RefreshFilter();
    }

    // The view only carries a filter while it hides rows, so unfiltered sorting and scrolling cost nothing extra.
    private void RefreshFilter()
    {
        if (rowsView == null) return;
        if (hiddenRows.Count == 0) rowsView.Filter = null;
        else if (rowsView.Filter == null) rowsView.Filter = IsRowShown;
        else rowsView.Refresh();
    }

    private string Summary(int index) =>
        matches.Count == 0 ? "No matches"
        : index >= 0 ? $"{index + 1:N0} of {matches.Count:N0}"
        : matches.Count == 1 ? "1 match" : $"{matches.Count:N0} matches";

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void OpenSearch() => ShowSearch(replace: false);

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void OpenReplace() => ShowSearch(replace: true);

    private void ShowSearch(bool replace)
    {
        IsSearchOpen = true;
        IsReplaceOpen = replace;
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

    private static string CellCount(int count) => count == 1 ? "1 cell" : $"{count:N0} cells";

    /// <summary>Replaces the current match and moves to the next one; the first press only finds a match.</summary>
    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void ReplaceNext()
    {
        IsSearchOpen = true;
        FlushSearch();
        if (matches.Count == 0 || !CommitEdits()) return;
        int index = currentMatch is { } current && ReferenceEquals(DataGrid.SelectedItem, current.Row)
            && DataGrid.CurrentColumn == current.Column
            ? matches.FindIndex(match => match.Row == current.Row && match.Column == current.Column) : -1;
        if (index < 0)
        {
            StepSearch(forward: true);
            return;
        }

        var replaced = matches[index];
        if (!TryReplace(replaced, out var change, out var error, out var warning))
        {
            ErrorMessage = error;
            return;
        }
        ErrorMessage = null;
        if (change is { } cell)
        {
            undoManager.Execute(new SetCellsCommand([cell]));
            WarningMessage = warning;
            // Recheck only the replaced cell so stepping on needs no full rescan.
            searchTimer.Stop();
            searchDirty = false;
            if (!IsMatch(replaced.Column.FormatValue(cell.NewValue)))
            {
                matches.RemoveAt(index);
                currentMatch = null;
                highlights.Remove(cell.Row, cell.FieldIndex);
                SearchSummary = Summary(-1);
            }
            StatusText = $"Replaced 1 cell in {replaced.Column.Header}";
        }
        if (matches.Count > 0) StepSearch(forward: true);
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private void ReplaceAll()
    {
        IsSearchOpen = true;
        FlushSearch();
        if (matches.Count == 0 || !CommitEdits()) return;
        var changes = new List<CellChange>();
        var warnings = new List<string>();
        int readOnly = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            if (!CanReplace(matches[i].Column))
            {
                readOnly++;
                continue;
            }
            // Nothing is applied unless every replacement is valid; show the first one that isn't.
            if (!TryReplace(matches[i], out var change, out var error, out var warning))
            {
                GoToMatch(i);
                ErrorMessage = error;
                return;
            }
            if (change is { } cell) changes.Add(cell);
            if (warning != null) warnings.Add(warning);
        }
        ErrorMessage = null;
        if (changes.Count > 0) undoManager.Execute(new SetCellsCommand(changes));
        WarningMessage = warnings.Count switch
        {
            0 => null,
            1 => warnings[0],
            _ => $"{CellCount(warnings.Count)} now hold text that isn't a known label, stored as new hashes. First: {warnings[0]}"
        };
        RunSearch();
        StatusText = $"Replaced {CellCount(changes.Count)}{(readOnly > 0 ? $" · skipped {CellCount(readOnly)} that can't be edited" : "")}";
    }

    private static bool CanReplace(HeavenDataGridColumn column) => !column.IsReadOnly && column.EditsAsText;

    private bool TryReplace(SearchMatch match, out CellChange? change, [NotNullWhen(false)] out string? error, out string? warning)
    {
        change = null;
        warning = null;
        var column = match.Column;
        if (!CanReplace(column))
        {
            error = $"{column.Header} can't be replaced as text.";
            return false;
        }
        var original = match.Row[column.FieldIndex];
        var text = column.FormatValue(original);
        var replaced = MatchWholeCell ? ReplaceText : text.Replace(SearchText, ReplaceText, SearchComparison);
        if (!column.TryParseText(replaced, original, out var value, out var reason))
        {
            error = $"Can't replace \"{text}\" with \"{replaced}\" in {column.Header}: {reason}";
            return false;
        }
        if (!Equals(value, original))
        {
            change = new CellChange(match.Row, column.FieldIndex, original, value);
            warning = column.Warning(replaced, value);
        }
        error = null;
        return true;
    }
}
