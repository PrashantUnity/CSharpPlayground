using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Commands;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Editor;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Storage;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;

/// <summary>
/// Extensibility and in-app customization bridge partial for CSharpNotebookStudioViewModel.
/// Connects notebook cells with IStudioApp, editor API, command shortcuts, and hot-reload.
/// </summary>
public partial class CSharpNotebookStudioViewModel
{
    public void InitializeExtensibilityBridge()
    {
        UpdateExtensibilityDocumentResolver();

        StudioAppContext.Instance.CommandPipeline.CommandsChanged += () =>
        {
            Dispatcher.UIThread.Post(InitializeQuickOpenCommands);
        };

        StudioAppContext.Instance.UiService.NotificationPosted += note =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ActiveTab != null)
                {
                    ActiveTab.KernelStatusText = note.Severity switch
                    {
                        NotificationSeverity.Success => $"✓ {note.Message}",
                        NotificationSeverity.Error => $"✗ {note.Message}",
                        NotificationSeverity.Warning => $"⚠️ {note.Message}",
                        _ => $"ℹ {note.Message}"
                    };
                }
            });
        };
    }

    public void UpdateExtensibilityDocumentResolver()
    {
        var editorService = StudioAppContext.Instance.EditorService;
        editorService.ActiveDocumentResolver = () => new NotebookDocumentContextAdapter(this);
        editorService.SaveDocumentHandler = () => _ = SaveAsync();
        editorService.OpenFileHandler = async path => await OpenWorkspaceFileAsync(path);
        editorService.FormatDocumentHandler = () => ActiveTab?.ActiveCell?.FormatCode();
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
    /// Evaluates the code in the active cell directly into the in-memory customization engine.
    /// </summary>
    [RelayCommand]
    public async Task ApplyActiveCellAsCustomizationAsync()
    {
        var cellCode = ActiveTab?.ActiveCell?.Source;
        if (string.IsNullOrWhiteSpace(cellCode))
        {
            if (ActiveTab != null) ActiveTab.KernelStatusText = "⚠️ Active cell has no code to apply.";
            return;
        }

        if (ActiveTab != null) ActiveTab.KernelStatusText = "Applying customization...";
        var customizationManager = StudioAppContext.Instance.CustomizationManager;
        var result = await customizationManager.ApplyCodeAsync(cellCode);

        if (result.Success)
        {
            if (ActiveTab != null) ActiveTab.KernelStatusText = "✨ Customization applied successfully!";
            StudioAppContext.Instance.UI.ShowSuccess("Customization applied successfully!");
        }
        else
        {
            if (ActiveTab != null) ActiveTab.KernelStatusText = "❌ Customization failed: " + result.ErrorMessage;
            StudioAppContext.Instance.UI.ShowError($"Customization failed: {result.ErrorMessage}");
        }
    }

    /// <summary>
    /// Re-evaluates ~/.frysharp/init.csx and reloads all registered customizations.
    /// </summary>
    [RelayCommand]
    public async Task ReloadCustomizationsAsync()
    {
        if (ActiveTab != null) ActiveTab.KernelStatusText = "Reloading customizations...";
        var customizationManager = StudioAppContext.Instance.CustomizationManager;
        var result = await customizationManager.ReloadAsync();

        if (result.Success)
        {
            if (ActiveTab != null) ActiveTab.KernelStatusText = "✨ Customizations reloaded!";
            StudioAppContext.Instance.UI.ShowSuccess("Customizations reloaded successfully!");
        }
        else
        {
            if (ActiveTab != null) ActiveTab.KernelStatusText = "❌ Reload failed: " + result.ErrorMessage;
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
            var starter = CustomizationStorageService.GetStarterInitScript();
            await File.WriteAllTextAsync(wsPath, starter);
        }

        await OpenWorkspaceFileAsync(wsPath);
    }
}
