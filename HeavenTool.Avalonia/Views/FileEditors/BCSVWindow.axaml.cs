using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Windowing;
using HeavenTool.Avalonia.ViewModels;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.Views.FileEditors;

public partial class BCSVWindow : FAAppWindow
{

    private readonly BCSVViewModel _viewModel;
    private bool closeConfirmed;
    private bool closingPrompt;

    public BCSVWindow()
    {
        InitializeComponent();
        _viewModel = new BCSVViewModel(this);
        DataContext = _viewModel;
        Closing += OnClosing;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closeConfirmed) return;
        e.Cancel = true;
        if (closingPrompt || _viewModel.IsBusy) return;
        closingPrompt = true;
        try
        {
            if (await _viewModel.ConfirmChangesAsync())
            {
                closeConfirmed = true;
                Close();
            }
        }
        finally { closingPrompt = false; }
    }

    public Task<bool?> ConfirmSaveChangesAsync()
    {
        var dialog = new Window
        {
            Title = "Unsaved BCSV changes", Width = 420, Height = 160,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var save = new Button { Content = "Save", IsDefault = true };
        var discard = new Button { Content = "Discard" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        save.Click += (_, _) => dialog.Close(true);
        discard.Click += (_, _) => dialog.Close(false);
        cancel.Click += (_, _) => dialog.Close(null);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20), Spacing = 20,
            Children =
            {
                new TextBlock { Text = "Save changes before closing this file?" },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8, Children = { save, discard, cancel }
                }
            }
        };
        return dialog.ShowDialog<bool?>(this);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = 32;
    }
}
