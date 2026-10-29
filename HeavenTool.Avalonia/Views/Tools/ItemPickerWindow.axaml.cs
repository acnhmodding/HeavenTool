using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Windowing;
using HeavenTool.Avalonia.ViewModels.VillagerCreator;

namespace HeavenTool.Avalonia.Views.Tools;

/// <summary>A dialog that picks an item; it closes with true when one was chosen.</summary>
public partial class ItemPickerWindow : FAAppWindow
{
    private readonly ItemPickerViewModel viewModel;

    public ItemPickerWindow() : this(null!) { }

    public ItemPickerWindow(ItemPickerViewModel viewModel)
    {
        AvaloniaXamlLoader.Load(this);
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = 32;
        this.viewModel = viewModel;
        DataContext = viewModel;

        Opened += (_, _) =>
        {
            this.FindControl<TextBox>("SearchBox")!.Focus();
            // Show the field's current item, once the list has laid out its rows.
            if (viewModel.Selected is { } selected)
                Dispatcher.UIThread.Post(() => this.FindControl<ListBox>("List")!.ScrollIntoView(selected), DispatcherPriority.Loaded);
        };
    }

    private void OnSelect(object? sender, RoutedEventArgs e) => Close(viewModel.HasSelection);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);

    private void OnItemDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Only a double click on a row picks it, not one on the scroll bar.
        if (e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) != null && viewModel.HasSelection)
            Close(true);
    }
}
