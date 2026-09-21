using System;
using System.Threading.Tasks;
using Avalonia;

namespace PdfEditorApp.Plugins.CSharpEditor.Runner;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine($"[AppDomain.UnhandledException] {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
            Console.Error.WriteLine($"[TaskScheduler.UnobservedTaskException] {e.Exception}");

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
