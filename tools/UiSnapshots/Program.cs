// UiSnapshots: renders FrySharp's real views off-screen (Avalonia headless platform + Skia) and saves PNG files.
// How it works and how to add a snapshot: docs/headless-ui-snapshots.md.
using PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots;
using PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots.Commands;

var options = new Options(args);
if (options.Command is null or "help" or "-h")
{
    Console.WriteLine(Usage.Text);
    return 0;
}

try
{
    if (options.Flag("dap-trace"))
    {
        PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Dap.DapClient.Trace = Console.WriteLine;
    }
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
        case "hub": HubAndDocsSnapshots.Hub(options); break;
        case "docs": HubAndDocsSnapshots.Docs(options); break;
        case "settings": HubAndDocsSnapshots.Settings(options); break;
        case "plot3d": Plot3DSnapshots.Render(options); break;
        case "visuals": VisualSnapshots.Render(options); break;
        case "perf": PerfSnapshots.Run(options); break;
        case "server": StudioSnapshots.ServerStudio(options); break;
        case "diagrams": DiagramSnapshots.Run(options); break;
        case "about": AppSnapshots.About(options); break;
        case "update": AppSnapshots.Update(options); break;
        case "loading": AppSnapshots.Loading(options); break;
        case "app-window" or "mainwindow": AppSnapshots.MainWindow(options); break;
        case "ai" or "composer": AppSnapshots.MainWindow(new Options(args.Append("--ai").Append("--demo-chat").ToArray())); break;
        case "ai-window": AppSnapshots.AiWindow(options); break;
        case "templates": TemplateSnapshots.Run(options); break;
        case "snake": AppSnapshots.Snake(options); break;
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
