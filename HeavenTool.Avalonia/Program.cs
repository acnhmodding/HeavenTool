using Avalonia;
using Avalonia.OpenGL;
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
                GlProfiles = [new(GlProfileType.OpenGL, 4, 4), new(GlProfileType.OpenGL, 4, 1), new(GlProfileType.OpenGL, 3, 3)],
            })
            .With(new Win32PlatformOptions
            {

                WinUICompositionBackdropCornerRadius = 8.0f,
                // BFRES shaders target desktop GL. ANGLE keeps the rest of the app GPU-rendered
                // where WGL is unavailable; the viewer then reports that it needs OpenGL 4.4.
                RenderingMode = [Win32RenderingMode.Wgl, Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
                WglProfiles = [new(GlProfileType.OpenGL, 4, 4), new(GlProfileType.OpenGL, 4, 1), new(GlProfileType.OpenGL, 3, 3)],
            })
            .LogToTrace();
            //.UseReactiveUI();
            //.WithInterFont()
    }
}
