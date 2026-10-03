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

        if (options.Flag("ai") || options.Flag("composer"))
        {
            window.StudioHostVm.AiComposer.IsVisible = true;
            if (options.Flag("minimized"))
            {
                window.StudioHostVm.AiComposer.IsMinimized = true;
            }
            if (options.Flag("demo-chat"))
            {
                var userMsg = new PdfEditorApp.Plugins.CSharpEditor.Models.AI.ChatMessageItem
                {
                    Role = Microsoft.Extensions.AI.ChatRole.User,
                    Content = "Please refactor Calculator.cs to modern C# 13 expression-bodied members and verify compilation."
                };
                var assistantMsg = new PdfEditorApp.Plugins.CSharpEditor.Models.AI.ChatMessageItem
                {
                    Role = Microsoft.Extensions.AI.ChatRole.Assistant,
                    Content = "I've inspected Calculator.cs, refactored the members into clean expression bodies, added Multiply, and confirmed compilation with Roslyn (0 errors).",
                    ReasoningContent = "1. Inspect Calculator.cs with read_file.\n2. Refactor Add method into modern C# 13 expression body.\n3. Add Multiply method.\n4. Run compile_and_get_diagnostics with Roslyn to verify 0 errors.",
                    IsReasoningExpanded = true
                };
                assistantMsg.Steps.Add(new PdfEditorApp.Plugins.CSharpEditor.Models.AI.AgentStepItem
                {
                    ToolName = "read_file",
                    Title = "Read Calculator.cs",
                    Status = PdfEditorApp.Plugins.CSharpEditor.Models.AI.AgentStepStatus.Completed
                });
                assistantMsg.Steps.Add(new PdfEditorApp.Plugins.CSharpEditor.Models.AI.AgentStepItem
                {
                    ToolName = "compile_and_get_diagnostics",
                    Title = "Roslyn compile check (0 errors)",
                    Status = PdfEditorApp.Plugins.CSharpEditor.Models.AI.AgentStepStatus.Completed
                });

                var modifiedFile = new PdfEditorApp.Plugins.CSharpEditor.Models.AI.ModifiedFileItem
                {
                    FilePath = "Calculator.cs",
                    RelativePath = "Calculator.cs",
                    OriginalContent = "public class Calculator\n{\n    public int Add(int a, int b)\n    {\n        return a + b;\n    }\n}\n",
                    ModifiedContent = "public class Calculator\n{\n    public int Add(int a, int b) => a + b;\n    public int Multiply(int a, int b) => a * b;\n}\n",
                    IsDiffExpanded = true
                };
                modifiedFile.CalculateLineMetrics();
                assistantMsg.ModifiedFiles.Add(modifiedFile);
                window.StudioHostVm.AiComposer.SessionModifiedFiles.Add(modifiedFile);

                window.StudioHostVm.AiComposer.Messages.Add(userMsg);
                window.StudioHostVm.AiComposer.Messages.Add(assistantMsg);
            }
            if (options.Flag("prompt-drawer"))
            {
                window.StudioHostVm.AiComposer.IsPromptEditorOpen = true;
            }
        }

        window.UpdateMenuStates();
        Snapshot.Settle(15);
        var shotName = options.Flag("ai") || options.Flag("composer")
            ? (options.Flag("minimized") ? "mainwindow_ai_minimized" : (options.Flag("prompt-drawer") ? "mainwindow_ai_prompt_drawer" : "mainwindow_ai_composer"))
            : (string.IsNullOrEmpty(page) || page == "hub" ? "mainwindow" : $"mainwindow_{page}");
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
