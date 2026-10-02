using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Commands;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Editor;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;

/// <summary>
/// Extensibility bridge partial integrating the studio ViewModel with IStudioApp,
/// editor API delegates, custom commands, and in-app customization script execution.
/// </summary>
public partial class CSharpCodeStudioViewModel
{
    public Func<string>? GetSelectedText { get; set; }
    public Action<string>? SetSelectedText { get; set; }
    public Action<string>? InsertEditorText { get; set; }
    public Action<int, int, int, int>? SetEditorSelection { get; set; }
    public Action<int>? ScrollEditorToLine { get; set; }

    public void InitializeExtensibilityBridge()
    {
        var editorService = StudioAppContext.Instance.EditorService;
        editorService.ActiveDocumentResolver = () => new StudioDocumentContextAdapter(this);
        editorService.OpenDocumentsResolver = () => OpenTabs.Select(_ => (IDocumentContext)new StudioDocumentContextAdapter(this)).ToList();
        editorService.FormatDocumentHandler = () => FormatCode();
        editorService.SaveDocumentHandler = () => _ = SaveAsync();
        editorService.OpenFileHandler = async path => await OpenWorkspaceFileAsync(path);
        editorService.CreateDocumentHandler = async (langId, initialCode) =>
        {
            await CreateNewSourceFileWithContentAsync(langId, initialCode);
        };
        editorService.CloseActiveDocumentHandler = async () =>
        {
            var active = OpenTabs.FirstOrDefault(t => t.IsActive);
            if (active != null) await CloseTabAsync(active);
        };

        // Workspace Service Bridge
        var ws = StudioAppContext.Instance.WorkspaceService;
        ws.RootPathResolver = () => _storageService.ActiveWorkspaceRootPath;
        ws.RefreshExplorerHandler = () => _ = RefreshExplorerCommand.ExecuteAsync(null);
        ws.OpenWorkspaceHandler = async folder => await OpenExternalProjectAsync(folder);

        // Terminal Service Bridge
        var term = StudioAppContext.Instance.TerminalService;
        term.OutputWriter = text => _postToUiThread(() =>
        {
            var tab = OpenTabs.FirstOrDefault(t => t.Id == Script?.Id);
            if (tab != null) tab.ConsoleOutput += text;
            ConsoleOutput += text;
        });
        term.ClearHandler = () => _postToUiThread(ClearConsole);

        // Results (.Dump) Bridge
        var results = StudioAppContext.Instance.ResultsService;
        results.ClearHandler = () => _postToUiThread(() =>
        {
            DumpResults.Clear();
            RichOutputs.Clear();
        });
        results.FocusHandler = () => _postToUiThread(() =>
        {
            SelectedBottomTabIndex = 0;
            IsBottomDeckExpanded = true;
        });
        results.ShowTableHandler = (data, title) => _postToUiThread(() =>
        {
            if (data is DumpTableResult tableRes)
            {
                DumpResults.Add(tableRes);
            }
            else
            {
                var table = new DumpTableResult(title ?? "Data");
                DumpResults.Add(table);
            }
            SelectedBottomTabIndex = 0;
            IsBottomDeckExpanded = true;
        });
        results.ShowControlHandler = (ctl, title) => _postToUiThread(() =>
        {
            if (ctl is Avalonia.Controls.Control avaloniaControl)
            {
                RichOutputs.Add(new Services.Display.RichCellOutput
                {
                    Kind = Services.Display.CellOutputKind.Control,
                    InteractiveControl = avaloniaControl,
                    Text = title ?? string.Empty
                });
            }
            SelectedBottomTabIndex = 0;
            IsBottomDeckExpanded = true;
        });

        StudioAppContext.Instance.CommandPipeline.CommandsChanged += () =>
        {
            _postToUiThread(InitializeQuickOpenCommands);
        };

        StudioAppContext.Instance.UiService.NotificationPosted += note =>
        {
            _postToUiThread(() =>
            {
                CompilerStatusText = note.Severity switch
                {
                    NotificationSeverity.Success => $"✓ {note.Message}",
                    NotificationSeverity.Error => $"✗ {note.Message}",
                    NotificationSeverity.Warning => $"⚠️ {note.Message}",
                    _ => $"ℹ {note.Message}"
                };
            });
        };
    }

    public async Task CreateNewSourceFileWithContentAsync(string languageId, string? initialCode = null)
    {
        var newDoc = await _storageService.CreateNewSourceFileAsync(languageId, initialContent: initialCode);
        if (newDoc != null)
        {
            await SaveDocumentAsync(userAsked: false);
            await UpdateActiveScriptAsync(newDoc);
        }
    }

    /// <summary>
    /// Checks registered extension commands for a matching keyboard shortcut and executes it.
    /// </summary>
    public bool TryExecuteExtensibilityShortcut(KeyEventArgs e)
    {
        var pipeline = StudioAppContext.Instance.CommandPipeline;
        foreach (var desc in pipeline.Descriptors)
        {
            if (ShortcutGestureMatcher.Matches(e, desc.Shortcut))
            {
                _ = pipeline.ExecuteAsync(desc.Id);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Evaluates the code in the active editor tab directly into the in-memory customization engine.
    /// </summary>
    [RelayCommand]
    public async Task ApplyActiveTabAsCustomizationAsync()
    {
        if (string.IsNullOrWhiteSpace(Code))
        {
            CompilerStatusText = "⚠️ Active tab has no code to apply.";
            return;
        }

        CompilerStatusText = "Applying customization...";
        var customizationManager = StudioAppContext.Instance.CustomizationManager;
        var result = await customizationManager.ApplyCodeAsync(Code);

        if (result.Success)
        {
            CompilerStatusText = "✨ Customization applied live!";
            StudioAppContext.Instance.UI.ShowSuccess("Customization applied live!");
        }
        else
        {
            CompilerStatusText = "❌ Customization failed. See Problems.";
            SelectedBottomTabIndex = 2;
            Diagnostics.Clear();
            foreach (var diag in result.Diagnostics)
            {
                Diagnostics.Add(new Common.DiagnosticItemViewModel(
                    diag,
                    (line, col) => RequestNavigateToCaret?.Invoke(line, col)));
            }
            ErrorCount = Diagnostics.Count(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            WarningCount = Diagnostics.Count(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);

            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                StudioAppContext.Instance.UI.ShowError($"Customization failed: {result.ErrorMessage}");
            }
        }
    }

    /// <summary>
    /// Re-evaluates ~/.frysharp/init.csx and reloads all registered customizations.
    /// </summary>
    [RelayCommand]
    public async Task ReloadCustomizationsAsync()
    {
        CompilerStatusText = "Reloading customizations...";
        var customizationManager = StudioAppContext.Instance.CustomizationManager;
        var result = await customizationManager.ReloadAsync();

        if (result.Success)
        {
            CompilerStatusText = "✨ Customizations reloaded!";
            StudioAppContext.Instance.UI.ShowSuccess("Customizations reloaded successfully!");
        }
        else
        {
            CompilerStatusText = "❌ Reload failed: " + result.ErrorMessage;
            StudioAppContext.Instance.UI.ShowError($"Reload failed: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// Opens the user's global ~/.frysharp/init.csx in an editor tab, creating it if needed.
    /// </summary>
    [RelayCommand]
    public async Task OpenUserInitScriptAsync()
    {
        var mgr = StudioAppContext.Instance.CustomizationManager;
        var scriptPath = mgr.Storage.GlobalInitScriptPath;
        await mgr.Storage.EnsureInitScriptExistsAsync();
        await OpenWorkspaceFileAsync(scriptPath);
    }

    /// <summary>
    /// Opens the workspace-level .frysharp/init.csx in an editor tab if a workspace folder is open.
    /// </summary>
    [RelayCommand]
    public async Task OpenWorkspaceInitScriptAsync()
    {
        var mgr = StudioAppContext.Instance.CustomizationManager;
        var wsPath = mgr.Storage.WorkspaceInitScriptPath;
        if (string.IsNullOrWhiteSpace(wsPath))
        {
            StudioAppContext.Instance.UI.ShowWarning("No workspace folder is currently open.");
            return;
        }

        var dir = Path.GetDirectoryName(wsPath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (!File.Exists(wsPath))
        {
            var starter = Services.Extensibility.Storage.CustomizationStorageService.GetStarterInitScript();
            await File.WriteAllTextAsync(wsPath, starter);
        }

        await OpenWorkspaceFileAsync(wsPath);
    }
}
