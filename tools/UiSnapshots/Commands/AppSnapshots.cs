using PdfEditorApp.Plugins.CSharpEditor.Runner;

namespace PdfEditorApp.Plugins.CSharpEditor.Tools.UiSnapshots.Commands;

/// <summary>Snapshots for application-level windows: About, Update Dialog, and MainWindow.</summary>
internal static class AppSnapshots
{
    public static void About(Options options)
    {
        var window = new AboutWindow
        {
            Width = options.Int("width", 520),
            Height = options.Int("height", 460)
        };
        window.Show();
        Snapshot.Settle();
        Snapshot.Save(window, options, "about");
    }

    public static void Update(Options options)
    {
        var window = new UpdateDialogWindow
        {
            Width = options.Int("width", 440),
            Height = options.Int("height", 260)
        };
        window.Show();
        Snapshot.Settle();
        Snapshot.Save(window, options, "update");
    }

    public static void MainWindow(Options options)
    {
        var window = new MainWindow
        {
            Width = options.Int("width", 1400),
            Height = options.Int("height", 900)
        };

        var page = options.Value("page")?.ToLowerInvariant();
        window.Show();
        Snapshot.Settle();

        switch (page)
        {
            case "script":
                Snapshot.WaitFor(() => window.StudioHostVm.CodeStudioViewModel != null, TimeSpan.FromSeconds(15));
                window.StudioHostVm.NavigateToCodeStudio(new PdfEditorApp.Plugins.CSharpEditor.Models.ScriptDocumentItem
                {
                    Title = "DemoScript.csx",
                    Code = "// Demo Script\nSystem.Console.WriteLine(\"Hello World\");\n"
                });
                Snapshot.WaitFor(() => window.StudioHostVm.CurrentPage is PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio.CSharpCodeStudioViewModel, TimeSpan.FromSeconds(10));
                break;
            case "notebook":
                Snapshot.WaitFor(() => window.StudioHostVm.NotebookStudioViewModel != null, TimeSpan.FromSeconds(15));
                window.StudioHostVm.NavigateToNotebookStudio(new PdfEditorApp.Plugins.CSharpEditor.Models.NotebookDocumentItem
                {
                    Title = "DemoNotebook.ipynb",
                    Cells = new()
                    {
                        new PdfEditorApp.Plugins.CSharpEditor.Models.NotebookCellItem
                        {
                            Source = "Console.WriteLine(\"Cell 1\");",
                            Type = PdfEditorApp.Plugins.CSharpEditor.Models.CellType.Code
                        }
                    }
                });
                Snapshot.WaitFor(() => window.StudioHostVm.CurrentPage is PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.CSharpNotebookStudioViewModel, TimeSpan.FromSeconds(10));
                break;
            case "server":
                window.StudioHostVm.NavigateToServerStudio(new PdfEditorApp.Plugins.CSharpEditor.Models.Server.FryServerDocumentItem
                {
                    Title = "DemoServer"
                });
                Snapshot.WaitFor(() => window.StudioHostVm.CurrentPage is PdfEditorApp.Plugins.CSharpEditor.ViewModels.Server.FryServerStudioViewModel, TimeSpan.FromSeconds(10));
                break;
            case "docs":
                window.StudioHostVm.NavigateToDocs();
                break;
            case "settings":
                window.StudioHostVm.NavigateToSettings();
                break;
            case "blind75":
                window.StudioHostVm.NavigateToBlindProblems();
                break;
            default:
                window.StudioHostVm.NavigateToManager();
                break;
        }

        window.UpdateMenuStates();
        Snapshot.Settle(15);
        var shotName = string.IsNullOrEmpty(page) || page == "hub" ? "mainwindow" : $"mainwindow_{page}";
        Snapshot.Save(window, options, shotName);
    }

    public static void Snake(Options options)
    {
        var window = new PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI.SnakeGameWindow
        {
            Width = options.Int("width", 360),
            Height = options.Int("height", 440)
        };
        window.Show();
        Snapshot.Settle();
        Snapshot.Save(window, options, "snake");
    }
}
