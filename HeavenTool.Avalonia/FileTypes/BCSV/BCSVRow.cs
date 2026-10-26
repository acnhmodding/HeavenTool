using System.ComponentModel;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

/// <summary>A grid item over one entry's values; cells refresh when a value is set through the indexer.</summary>
public sealed class BCSVRow(object[] values) : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs IndexerChanged = new("Item[]");

    public object[] Values { get; } = values;

    public object this[int index]
    {
        get => Values[index];
        set
        {
            Values[index] = value;
            PropertyChanged?.Invoke(this, IndexerChanged);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
