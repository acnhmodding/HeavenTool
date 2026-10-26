using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using HeavenTool.Avalonia.Views;
using HeavenTool.Avalonia.Views.FileEditors;

namespace HeavenTool.Avalonia;

public partial class App : Application
{
    /// <summary>The version title bars show, from the project's Version property.</summary>
    public static string DisplayVersion { get; } = $"v{typeof(App).Assembly.GetName().Version?.ToString(2)}";

    /// <summary>One of the app's colours from Assets/Styles/Colors.axaml, for controls built in code.</summary>
    public static IBrush Brush(string key) =>
        Current?.TryGetResource(key, Current.ActualThemeVariant, out var value) == true && value is IBrush brush ? brush : Brushes.Transparent;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
#if DEBUG
        this.AttachDeveloperTools();
#endif
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
