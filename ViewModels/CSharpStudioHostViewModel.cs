using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpStudioHostViewModel : ObservableObject
{
    private readonly IScriptStorageService _storageService;
    private RoslynCompilerService? _compilerService;
    private ScriptExecutionEngine? _executionEngine;

    [ObservableProperty]
    private object _currentPage;

    [ObservableProperty]
    private bool _isOnManagerPage = true;

    [ObservableProperty]
    private bool _isEngineLoading = true;

    [ObservableProperty]
    private string _engineStatus = "Warming up Roslyn .NET engine…";

    [ObservableProperty]
    private string _activeDocumentTitle = "Hub";

    /// <summary>
    /// Action callback for standalone test runners or host shells to close the preview window.
    /// </summary>
    public Action? RequestClose { get; set; }

    public CSharpManagerViewModel ManagerViewModel { get; }
    public CSharpCodeStudioViewModel? CodeStudioViewModel { get; private set; }
    public CSharpNotebookStudioViewModel? NotebookStudioViewModel { get; private set; }

    public CSharpStudioHostViewModel(IServiceProvider? serviceProvider = null)
    {
        _storageService = new LocalScriptStorageService();

        // ── Show Manager immediately — it doesn't need the compiler ──
        ManagerViewModel = new CSharpManagerViewModel(
            _storageService,
            openScriptAction: NavigateToCodeStudio,
            openNotebookAction: NavigateToNotebookStudio,
            navigateToHomeAction: NavigateToHome);

        _currentPage = ManagerViewModel;
        _activeDocumentTitle = "Hub";

        // ── Boot the Roslyn compiler service off the UI thread ──
        // ⚠️  DO NOT move RoslynCompilerService or child ViewModel construction back into this
        //     constructor body. See the post-mortem comment on InitializeCompilerAsync below.
        _ = Task.Run(InitializeCompilerAsync);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════
    // ⚠️  POST-MORTEM: UI FREEZE BUG — 2026-09-14  (DO NOT REPEAT)
    // ══════════════════════════════════════════════════════════════════════════════════════
    // SYMPTOM:  The Runner window opened but showed a completely blank/frozen white screen
    //           for ~14 seconds before any UI became visible.
    //
    // ROOT CAUSE: `new RoslynCompilerService()` calls `InitializeDefaultReferences()` which
    //             loads 15+ `MetadataReference` objects from disk via reflection. This was
    //             called synchronously in this constructor — ON THE UI THREAD.
    //             The UI thread was blocked for 14,124 ms. Avalonia cannot paint any frames
    //             while the UI thread is blocked, so the window appeared frozen/invisible.
    //
    // DIAGNOSIS: Added `Program.Log()` timing stamps around each constructor call.
    //            Log showed:  "VM created OK (14124ms)"  → blocked inside `new RoslynCompilerService()`.
    //
    // FIX:       1. ManagerViewModel is created synchronously (fast, no compiler needed).
    //            2. RoslynCompilerService + ScriptExecutionEngine + both child ViewModels
    //               are ALL created inside `Task.Run` on a background thread.
    //            3. Only the 4 lightweight property assignments are posted back to the UI
    //               thread via `Dispatcher.UIThread.Post` (fire-and-forget, non-blocking).
    //
    // RULE:      NEVER construct RoslynCompilerService (or any Roslyn/MSBuild/heavy-reflection
    //            object) on the UI thread. Always use Task.Run. This applies to any future
    //            ViewModel or service that loads assemblies, reads disk at startup, or calls
    //            into Roslyn APIs during construction.
    // ══════════════════════════════════════════════════════════════════════════════════════
    private async Task InitializeCompilerAsync()
    {
        // Build ALL heavy objects on a background thread — none of them require the UI thread
        CSharpCodeStudioViewModel? codeVm = null;
        CSharpNotebookStudioViewModel? notebookVm = null;

        await Task.Run(() =>
        {
            _compilerService = new RoslynCompilerService();
            _executionEngine = new ScriptExecutionEngine();

            var initialScript = new ScriptDocumentItem
            {
                Title = "1. Two Sum (Algorithm Workspace)",
                Code = CodeTemplateLibrary.GetTemplates()[0].InitialCode,
                Notes = CodeTemplateLibrary.GetTemplates()[0].Notes
            };

            codeVm = new CSharpCodeStudioViewModel(
                initialScript,
                _storageService,
                _compilerService,
                _executionEngine,
                backToHubAction: NavigateToManager,
                backToHomeAction: NavigateToHome);

            var initialNotebook = new NotebookDocumentItem
            {
                Title = "Document Automation Notebook"
            };

            notebookVm = new CSharpNotebookStudioViewModel(
                initialNotebook,
                _storageService,
                _compilerService,
                _executionEngine,
                backToHubAction: NavigateToManager,
                backToHomeAction: NavigateToHome);
        });

        // Only lightweight property assignments go back to the UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            CodeStudioViewModel = codeVm;
            NotebookStudioViewModel = notebookVm;
            IsEngineLoading = false;
            EngineStatus = "Roslyn .NET 10 Engine Active";
        });
    }


    [RelayCommand]
    public void NavigateToHome()
    {
        // 1. Notify standalone runner or external host listener
        RequestClose?.Invoke();

        // 2. Signal FryPDF shell to restore Home workspace nav state and unhide sidebar
        try
        {
            var msgType = Type.GetType("PdfEditorApp.Messages.NavigateToHomeMessage, PdfEditorApp");
            if (msgType != null)
            {
                var instance = Activator.CreateInstance(msgType);
                var sendMethod = typeof(WeakReferenceMessenger).GetMethods()
                    .FirstOrDefault(m => m.Name == "Send" && m.IsGenericMethod && m.GetGenericArguments().Length == 1);
                sendMethod?.MakeGenericMethod(msgType).Invoke(WeakReferenceMessenger.Default, [instance]);
            }
        }
        catch
        {
            // Fallback for runner or standalone test environment
        }
    }

    public void NavigateToCodeStudio(ScriptDocumentItem script)
    {
        if (CodeStudioViewModel == null) return; // Engine still loading
        CodeStudioViewModel.UpdateActiveScript(script);
        CurrentPage = CodeStudioViewModel;
        IsOnManagerPage = false;
        ActiveDocumentTitle = string.IsNullOrWhiteSpace(script.Title) ? "Untitled Script" : script.Title;
    }

    public void NavigateToNotebookStudio(NotebookDocumentItem notebook)
    {
        if (NotebookStudioViewModel == null) return; // Engine still loading
        NotebookStudioViewModel.UpdateActiveNotebook(notebook);
        CurrentPage = NotebookStudioViewModel;
        IsOnManagerPage = false;
        ActiveDocumentTitle = string.IsNullOrWhiteSpace(notebook.Title) ? "Untitled Notebook" : notebook.Title;
    }

    [RelayCommand]
    public void NavigateToManager()
    {
        _ = ManagerViewModel.LoadWorkspaceItemsAsync();
        CurrentPage = ManagerViewModel;
        IsOnManagerPage = true;
        ActiveDocumentTitle = "Hub";
    }
}
