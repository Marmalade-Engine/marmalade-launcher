using Avalonia;
using System;

namespace MarmaladeLauncher;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions  {
                RenderingMode = new[] { Win32RenderingMode.Software }
            })
            .With(new AvaloniaNativePlatformOptions {
                RenderingMode = new[] { AvaloniaNativeRenderingMode.Software }
            })
            .With(new X11PlatformOptions {
                RenderingMode = new[] { X11RenderingMode.Software }
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}