using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using FluentAvalonia.UI.Windowing;
using HeavenTool.Avalonia.ViewModels;
using IconPacks.Avalonia.FontAwesome6;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.Views
{
    public partial class MainWindow : FAAppWindow, IMainView
    {
        public static readonly FuncValueConverter<bool, PackIconFontAwesome6Kind> FavoriteIcon =
            new(favorite => favorite ? PackIconFontAwesome6Kind.StarSolid : PackIconFontAwesome6Kind.StarRegular);
        public static readonly FuncValueConverter<bool, IBrush> FavoriteBrush =
            new(favorite => App.Brush(favorite ? "WarningText" : "SecondaryText"));
        public static readonly FuncValueConverter<bool, string> FavoriteTip =
            new(favorite => favorite ? "Remove from favorites" : "Add to favorites");
        public static readonly FuncValueConverter<bool, string> EnabledTip =
            new(enabled => enabled ? "Deployed. Turn off to leave it out of the next deploy" : "Not deployed. Turn on to include it in the next deploy");

        private static readonly FuncDataTemplate<Choice> ChoiceTemplate = new((choice, _) => ChoiceView(choice));

        // A check box that follows its list item's selection, for lists where several can be picked.
        private static readonly FuncDataTemplate<Choice> CheckedChoiceTemplate = new((choice, _) =>
        {
            var check = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                [!ToggleButton.IsCheckedProperty] = new Binding(nameof(ListBoxItem.IsSelected))
                {
                    RelativeSource = new RelativeSource { AncestorType = typeof(ListBoxItem) },
                },
            };
            var view = ChoiceView(choice);
            Grid.SetColumn(view, 1);
            return new Grid { ColumnDefinitions = new("Auto,*"), Children = { check, view } };
        });

        private static Control ChoiceView(Choice? choice)
        {
            var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 4) };
            if (choice == null) return panel;
            panel.Children.Add(new TextBlock { Text = choice.Title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            if (choice.Subtitle != null)
                panel.Children.Add(new TextBlock { Text = choice.Subtitle, FontSize = 12, Foreground = App.Brush("SecondaryText"), TextWrapping = TextWrapping.Wrap });
            if (choice.Details != null)
                panel.Children.Add(new TextBlock { Text = choice.Details, FontSize = 12, Foreground = App.Brush("DimText") });
            return panel;
        }

        private readonly MainWindowViewModel viewModel;
        // FluentAvalonia shows one content dialog at a time, and two installs can ask at once.
        private readonly SemaphoreSlim dialogLock = new(1, 1);

        public MainWindow()
        {
            InitializeComponent();

            viewModel = new MainWindowViewModel();
            DataContext = viewModel;
            viewModel.Attach(this);

            // Load the next page as the list nears its end.
            var scroller = this.FindControl<ScrollViewer>("ModScroller")!;
            scroller.ScrollChanged += (_, _) =>
            {
                if (scroller.Offset.Y + scroller.Viewport.Height >= scroller.Extent.Height - 200
                    && viewModel.HasMore && !viewModel.IsLoading && viewModel.LoadMoreCommand.CanExecute(null))
                    viewModel.LoadMoreCommand.Execute(null);
            };
        }

        // The categories load the first time the filters open, and an edited author applies when they close.
        private void FilterFlyout_Opened(object? sender, System.EventArgs e) => _ = viewModel.LoadCategoriesAsync();

        private void FilterFlyout_Closed(object? sender, System.EventArgs e) => viewModel.ApplyFiltersCommand.Execute(null);

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            TitleBar.ExtendsContentIntoTitleBar = true;
            TitleBar.Height = 32;
        }

        public async Task<int?> ChooseAsync(string title, string message, IReadOnlyList<Choice> choices, string confirmText)
        {
            var list = new ListBox
            {
                ItemsSource = choices,
                ItemTemplate = ChoiceTemplate,
                SelectedIndex = 0,
                MaxHeight = 360,
            };
            var content = new StackPanel { Spacing = 12, Width = 460 };
            content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(list);

            var dialog = new FAContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = confirmText,
                CloseButtonText = "Cancel",
                DefaultButton = FAContentDialogButton.Primary,
            };
            list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = list.SelectedIndex >= 0;
            list.DoubleTapped += (_, _) => { if (list.SelectedIndex >= 0) dialog.Hide(FAContentDialogResult.Primary); };

            var result = await ShowDialogAsync(dialog);
            return result == FAContentDialogResult.Primary && list.SelectedIndex >= 0 ? list.SelectedIndex : null;
        }

        public async Task<IReadOnlyList<int>?> ChooseManyAsync(string title, string message, IReadOnlyList<Choice> choices, IReadOnlyList<int> selected, string confirmText)
        {
            var list = new ListBox
            {
                ItemsSource = choices,
                ItemTemplate = CheckedChoiceTemplate,
                SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle,
                MaxHeight = 360,
            };
            foreach (var index in selected) list.Selection.Select(index);
            var content = new StackPanel { Spacing = 12, Width = 460 };
            content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            content.Children.Add(list);

            var dialog = new FAContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = confirmText,
                CloseButtonText = "Cancel",
                DefaultButton = FAContentDialogButton.Primary,
            };
            list.SelectionChanged += (_, _) => dialog.IsPrimaryButtonEnabled = list.Selection.Count > 0;

            var result = await ShowDialogAsync(dialog);
            return result == FAContentDialogResult.Primary && list.Selection.Count > 0 ? [.. list.Selection.SelectedIndexes] : null;
        }

        public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
        {
            var dialog = new FAContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
                PrimaryButtonText = confirmText,
                CloseButtonText = "Cancel",
                DefaultButton = FAContentDialogButton.Primary,
            };
            return await ShowDialogAsync(dialog) == FAContentDialogResult.Primary;
        }

        public Task ShowMessageAsync(string title, string message) => ShowDialogAsync(new FAContentDialog
        {
            Title = title,
            // Long messages, like a deploy's list of conflicts, scroll.
            Content = new ScrollViewer
            {
                MaxHeight = 420,
                Content = new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460, Margin = new Thickness(0, 0, 14, 0) },
            },
            CloseButtonText = "OK",
            DefaultButton = FAContentDialogButton.Close,
        });

        private async Task<FAContentDialogResult> ShowDialogAsync(FAContentDialog dialog)
        {
            await dialogLock.WaitAsync();
            try { return await dialog.ShowAsync(this); }
            finally { dialogLock.Release(); }
        }
    }
}
