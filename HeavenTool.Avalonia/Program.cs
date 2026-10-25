using Avalonia;
using System;

namespace HeavenTool.Avalonia;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("Initializing HeavenTool...");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                EnableMultiTouch = true,
                EnableIme = true,
            })
            .With(new Win32PlatformOptions
            {

                WinUICompositionBackdropCornerRadius = 8.0f,
            })
            .LogToTrace();
            //.UseReactiveUI();
            //.WithInterFont()
    }
}
