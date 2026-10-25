using CommunityToolkit.Mvvm.ComponentModel;

namespace HeavenTool.Avalonia.ViewModels;

public partial class ColumnVisibilityModel(int index, string header) : ObservableObject
{
    public int Index { get; } = index;

    public string Header { get; } = header;

    [ObservableProperty]
    private bool isVisible = true;
}
