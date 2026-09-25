using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

/// <summary>
/// The headless session: Avalonia with the Skia renderer but no screen and no running message loop, plus the few things
/// a snapshot needs: show a view in an off-screen window, let it settle, drive the mouse, and save what it drew.
/// </summary>
internal static class Snapshot
{
    private static readonly List<string> TempFolders = new();

    public static string OutputFolder { get; private set; } = string.Empty;

    public static void Start(Options options)
    {
        AppBuilder.Configure<SnapshotApp>()
            .UseSkia()
            // Draw real pixels with Skia; headless drawing would render nothing worth saving.
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        if (options.Flag("light")) Application.Current!.RequestedThemeVariant = ThemeVariant.Light;

        OutputFolder = Path.GetFullPath(options.Value("out") ?? DefaultOutputFolder());
        Directory.CreateDirectory(OutputFolder);
    }

    /// <summary>Puts <paramref name="content"/> in an off-screen window of that size (the image size) and lets it lay out.</summary>
    public static Window Show(Control content, double width, double height)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Settle();
        return window;
    }

    /// <summary>
    /// Runs the UI thread's queued work (bindings, layout, posted callbacks) for a few frames. Nothing else runs it:
    /// there is no message loop, so call this after anything that changes what's on screen.
    /// </summary>
    public static void Settle(int frames = 10)
    {
        for (int i = 0; i < frames; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(30);
        }
    }

    /// <summary>
    /// Waits for async work while keeping the UI thread's queue moving. Never <c>await</c> instead: the continuation is
    /// queued on the UI thread, nothing runs that queue while the program waits, and it hangs at 0% CPU.
    /// </summary>
    public static void Wait(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        task.GetAwaiter().GetResult();
    }

    public static T Wait<T>(Task<T> task)
    {
        Wait((Task)task);
        return task.Result;
    }

    /// <summary>
    /// Applies <c>--hover</c>, then saves what the window drew as <c>{--name or defaultName}.png</c> and prints its path.
    /// </summary>
    public static string Save(Window window, Options options, string defaultName)
    {
        if (options.Value("hover") is { } hover) Hover(window, ParsePoint(hover));
        return Save(window, options.Value("name") ?? defaultName);
    }

    public static string Save(Window window, string name)
    {
        Settle(2);
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window rendered nothing.");
        string path = Path.Combine(OutputFolder, name + ".png");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        Console.WriteLine(path);
        return path;
    }

    /// <summary>Moves the mouse to <paramref name="point"/> (window coordinates), so hover styles show.</summary>
    public static void Hover(Window window, Point point)
    {
        window.MouseMove(point);
        Settle(6);
    }

    /// <summary>Presses the left button at <paramref name="from"/>, moves by <paramref name="by"/> in small steps and lets go.</summary>
    public static void Drag(Window window, Point from, Vector by, int steps = 8)
    {
        window.MouseMove(from);
        window.MouseDown(from, MouseButton.Left);
        for (int step = 1; step <= steps; step++)
        {
            window.MouseMove(from + by * step / steps);
            Dispatcher.UIThread.RunJobs();
        }
        window.MouseUp(from + by, MouseButton.Left);
        Settle(3);
    }

    /// <summary>Adds the plugin's style sheets to a control shown on its own, outside the view that normally includes them.</summary>
    public static void AddPluginStyles(Control control, params string[] styleFiles)
    {
        foreach (string file in styleFiles)
        {
            control.Styles.Add(new StyleInclude(new Uri("avares://UiSnapshots/")) { Source = new Uri($"avares://CSharpEditorPlugin/{file}") });
        }
    }

    /// <summary>A throwaway folder (deleted on exit) for storage and progress, so snapshots never touch your real data.</summary>
    public static string TempFolder(string purpose)
    {
        string folder = Path.Combine(Path.GetTempPath(), "UiSnapshots", $"{purpose}_{Guid.NewGuid():N}");
        TempFolders.Add(folder);
        return folder;
    }

    public static void DeleteTempFolders()
    {
        foreach (string folder in TempFolders)
        {
            try
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // Something still has a file open; the OS cleans the temp folder eventually.
            }
        }
    }

    // "x,y" in window coordinates.
    private static Point ParsePoint(string text)
    {
        var parts = text.Split(',');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
        {
            return new Point(x, y);
        }
        throw new ArgumentException($"--hover wants x,y in window pixels, like 700,70; not '{text}'.");
    }

    // tools/UiSnapshots/out in the repository (git ignores it), found by walking up from the build output.
    private static string DefaultOutputFolder()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "CSharpEditorPlugin.slnx")))
            {
                return Path.Combine(folder.FullName, "tools", "UiSnapshots", "out");
            }
        }
        return Path.Combine(AppContext.BaseDirectory, "out");
    }
}
