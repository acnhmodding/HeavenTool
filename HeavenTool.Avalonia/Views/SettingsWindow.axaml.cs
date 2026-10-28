using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using FluentAvalonia.UI.Windowing;
using HeavenTool.Avalonia.Settings;
using HeavenTool.Avalonia.ViewModels;
using IconPacks.Avalonia.FontAwesome6;

namespace HeavenTool.Avalonia.Views;

public partial class SettingsWindow : FAAppWindow
{
    private static SettingsWindow? instance;
    private readonly SettingsViewModel viewModel;

    public static readonly FuncValueConverter<StatusKind, PackIconFontAwesome6Kind> StatusIcon = new(kind => kind switch
    {
        StatusKind.Ok => PackIconFontAwesome6Kind.CircleCheckSolid,
        StatusKind.Info => PackIconFontAwesome6Kind.CircleInfoSolid,
        StatusKind.Warning => PackIconFontAwesome6Kind.TriangleExclamationSolid,
        _ => PackIconFontAwesome6Kind.CircleXmarkSolid,
    });

    public static readonly FuncValueConverter<StatusKind, IBrush> StatusBrush = new(kind => App.Brush(kind switch
    {
        StatusKind.Ok => "ACNHGreen",
        StatusKind.Info => "SecondaryText",
        StatusKind.Warning => "WarningText",
        _ => "ErrorText",
    }));

    public SettingsWindow()
    {
        AvaloniaXamlLoader.Load(this);
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = 32;
        viewModel = new(AppSettings.Current, StorageProvider, Launcher);
        DataContext = viewModel;
        Closing += (_, _) => viewModel.Save();
        KeyDown += (_, e) => { if (e.Key == Key.Escape && !e.Handled) Close(); };
    }

    /// <summary>Shows the settings, bringing the window forward if it's already open.</summary>
    public static void ShowSingle()
    {
        if (instance != null)
        {
            instance.Activate();
            return;
        }
        instance = new SettingsWindow();
        instance.Closed += (_, _) => instance = null;
        instance.Show();
    }
}
