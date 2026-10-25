using Avalonia;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Windowing;
using HeavenTool.Avalonia.ViewModels;

namespace HeavenTool.Avalonia.Views.FileEditors;

public partial class BCSVWindow : FAAppWindow
{

    private readonly BCSVViewModel _viewModel;

    public BCSVWindow()
    {
        InitializeComponent();
        _viewModel = new BCSVViewModel(this);
        DataContext = _viewModel;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = 32;
    }
}
