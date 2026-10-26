using CommunityToolkit.Mvvm.ComponentModel;

namespace HeavenTool.Avalonia.ViewModels;

public partial class ColumnVisibilityModel(int index, string header) : ObservableObject
{
    public int Index { get; } = index;

    public string Header { get; } = header;

    /// <summary>False for a colour's other fields, which follow the one listed under the colour's name.</summary>
    public bool IsListed { get; init; } = true;

    [ObservableProperty]
    private bool isVisible = true;
}
