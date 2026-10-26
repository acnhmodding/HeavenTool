using System;
using System.Collections.Generic;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public enum SearchHit { None, Match, Current }

/// <summary>The cells a search matched, shared by every column so visible cells can mark themselves.</summary>
public sealed class SearchHighlights
{
    private HashSet<(BCSVRow Row, int Field)> matches = [];
    private (BCSVRow? Row, int Field) current;

    public event EventHandler? Changed;

    public SearchHit this[BCSVRow row, int field] =>
        ReferenceEquals(current.Row, row) && current.Field == field ? SearchHit.Current
        : matches.Contains((row, field)) ? SearchHit.Match : SearchHit.None;

    public void Set(HashSet<(BCSVRow Row, int Field)> cells, BCSVRow? currentRow = null, int currentField = -1)
    {
        matches = cells;
        current = (currentRow, currentField);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetCurrent(BCSVRow? row, int field)
    {
        current = (row, field);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(BCSVRow row, int field)
    {
        if (matches.Remove((row, field))) Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear() => Set([]);
}
