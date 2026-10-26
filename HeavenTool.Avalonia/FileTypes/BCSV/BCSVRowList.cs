using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

/// <summary>The document's rows, with a bulk replace that the grid sees as one reset.</summary>
public sealed class BCSVRowList : ObservableCollection<BCSVRow>
{
    public BCSVRowList() { }
    public BCSVRowList(IEnumerable<BCSVRow> rows) : base(rows) { }

    /// <summary>
    /// Swaps in new contents with a single Reset. The grid's view handles a range Add one row at a
    /// time, and an empty Reset makes it drop its recycled rows, so neither suits large changes.
    /// </summary>
    public void ReplaceAll(IEnumerable<BCSVRow> rows)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var row in rows) Items.Add(row);
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
