// UiSnapshots: renders C# Code Studio's real views off-screen (Avalonia headless platform + Skia) and saves PNG files.
// How it works and how to add a snapshot: docs/headless-ui-snapshots.md.
using PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;

var options = new Options(args);
if (options.Command is null or "help" or "-h")
{
    Console.WriteLine(Usage.Text);
    return 0;
}

try
{
    Snapshot.Start(options);
    switch (options.Command)
    {
        case "blind75": Blind75Snapshots.Browser(options); break;
        case "details": Blind75Snapshots.Details(options); break;
        case "markdown": Blind75Snapshots.Markdown(options); break;
        case "script": Blind75Snapshots.Script(options); break;
        case "studio": StudioSnapshots.CodeStudio(options); break;
        case "notebook": StudioSnapshots.Notebook(options); break;
        case "visualizer": VisualizerSnapshots.Frames(options); break;
        default: throw new ArgumentException($"Unknown command '{options.Command}'.");
    }
    return 0;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Run with `help` for the commands and options.");
    return 1;
}
finally
{
    Snapshot.DeleteTempFolders();
}
