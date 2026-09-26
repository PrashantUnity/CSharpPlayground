using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PdfEditorApp.Core.Plugins.Settings;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.Services.Languages;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpStudioHostViewModel : ObservableObject
{
    private const string PluginId = "com.frypdf.plugin.csharpeditor";

    // 0 = no automatic timeout: execution runs until the user clicks Stop, matching Jupyter/
    // dotnet-interactive. A fixed ceiling only made sense as a safety net for when Stop couldn't
    // reliably interrupt a running script — now that it can (see ExecutionAbandonment), an
    // unrelated slow-but-working call (e.g. a network request) shouldn't be cut off arbitrarily.
    private const int DefaultExecutionTimeoutSeconds = 0;

    private readonly IScriptStorageService _storageService;
    private readonly IPluginSettingsStore? _settingsStore;

    // One progress store for the whole studio: the Blind 75 page and Code Studio (which marks a problem solved when
    // every case passes) must see each other's changes, and two stores on one file would overwrite each other.
    private readonly IBlindProgressService _blindProgress;

    // One set of languages for every page: the same registry lists the workspace's files, and the same saved toolchain
    // choices, studio environments and processes serve the Code Studio, the notebooks and the Hub.
    private readonly StudioLanguageServices _languages;
    public StudioLanguageServices Languages => _languages;
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

    private readonly Task _initTask;

    public CSharpManagerViewModel ManagerViewModel { get; }
    public CSharpDocsViewModel DocsViewModel { get; }
    public CSharpBlindProblemsViewModel BlindProblemsViewModel { get; }
    public CSharpCodeStudioViewModel? CodeStudioViewModel { get; private set; }
    public CSharpNotebookStudioViewModel? NotebookStudioViewModel { get; private set; }

    /// <param name="serviceProvider">Resolves the plugin settings store when <paramref name="settingsStore"/> isn't given.</param>
    /// <param name="settingsStore">The plugin's settings (execution timeout).</param>
    /// <param name="blindProgress">Where Blind 75 progress lives; the user's progress file when not given.</param>
    public CSharpStudioHostViewModel(
        IServiceProvider? serviceProvider = null,
        IPluginSettingsStore? settingsStore = null,
        IBlindProgressService? blindProgress = null,
        StudioLanguageServices? languages = null)
    {
        _languages = languages ?? StudioLanguageServices.Default;
        _storageService = new LocalScriptStorageService(languages: _languages.Registry);
        _blindProgress = blindProgress ?? new LocalBlindProgressService();
        // Prefer an explicitly-passed store (how the real plugin host wires it, via
        // IFryPluginContext.TryGetService inside CSharpEditorPlugin.ApplyAsync's ViewFactory), but
        // fall back to resolving it off the plain IServiceProvider — the standalone Runner already
        // supplies one this way via StandaloneServiceProvider/StandaloneSettingsStore.
        _settingsStore = settingsStore ?? serviceProvider?.GetService(typeof(IPluginSettingsStore)) as IPluginSettingsStore;

        // ── Initialize Documentation & Learning Center page ──
        DocsViewModel = new CSharpDocsViewModel(
            docService: DocumentationService.Instance,
            backToHubAction: NavigateToManager,
            openScriptAction: NavigateToCodeStudio,
            openNotebookAction: NavigateToNotebookStudio);

        // ── Initialize Blind 75 Algorithm Hub page ──
        BlindProblemsViewModel = new CSharpBlindProblemsViewModel(
            progressService: _blindProgress,
            backToHubAction: NavigateToManager,
            openScriptAction: NavigateToCodeStudio,
            openNotebookAction: NavigateToNotebookStudio);

        // ── Show Manager immediately — it doesn't need the compiler ──
        ManagerViewModel = new CSharpManagerViewModel(
            _storageService,
            openScriptAction: NavigateToCodeStudio,
            openNotebookAction: NavigateToNotebookStudio,
            navigateToHomeAction: NavigateToHome,
            navigateToDocsAction: () => NavigateToDocs(),
            navigateToBlindProblemsAction: () => NavigateToBlindProblems(),
            languages: _languages);

        _currentPage = ManagerViewModel;
        _activeDocumentTitle = "Hub";

        // ── Boot the Roslyn compiler service off the UI thread ──
        // ⚠️  DO NOT move RoslynCompilerService or child ViewModel construction back into this
        //     constructor body. See the post-mortem comment on InitializeCompilerAsync below.
        _initTask = Task.Run(InitializeCompilerAsync);
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
        CSharpCodeStudioViewModel? codeVm = null;
        CSharpNotebookStudioViewModel? notebookVm = null;

        await Task.Run(() =>
        {
            RoslynCompilerService.Warmup();
            NotebookExecutionKernel.Warmup();

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
                backToHomeAction: NavigateToHome,
                getTimeoutSeconds: GetExecutionTimeoutSeconds,
                openNotebookAction: NavigateToNotebookStudio,
                navigateToDocsAction: () => NavigateToDocs(),
                blindProgress: _blindProgress,
                languages: _languages);

            var initialNotebook = new NotebookDocumentItem
            {
                Title = "Interactive C# Notebook"
            };

            notebookVm = new CSharpNotebookStudioViewModel(
                initialNotebook,
                _storageService,
                _compilerService,
                _executionEngine,
                backToHubAction: NavigateToManager,
                backToHomeAction: NavigateToHome,
                getTimeoutSeconds: GetExecutionTimeoutSeconds,
                openScriptAction: NavigateToCodeStudio,
                navigateToDocsAction: () => NavigateToDocs(),
                languages: _languages);
        });

        void Publish()
        {
            CodeStudioViewModel = codeVm;
            NotebookStudioViewModel = notebookVm;
            IsEngineLoading = false;
            EngineStatus = "Roslyn .NET 10 Engine Active";
        }

        // Without an Avalonia app (unit tests) there is no UI thread to hand over to.
        if (Avalonia.Application.Current != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Publish);
        }
        else
        {
            Publish();
        }
    }


    [RelayCommand]
    public void NavigateToHome()
    {
        RequestClose?.Invoke();

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
        }
    }

    public async void NavigateToCodeStudio(ScriptDocumentItem script)
    {
        if (CodeStudioViewModel == null)
        {
            await _initTask;
        }
        if (CodeStudioViewModel == null) return;
        await CodeStudioViewModel.UpdateActiveScriptAsync(script);
        CurrentPage = CodeStudioViewModel;
        IsOnManagerPage = false;
        ActiveDocumentTitle = string.IsNullOrWhiteSpace(script.Title) ? "Untitled Script" : script.Title;
    }

    public async void NavigateToNotebookStudio(NotebookDocumentItem notebook)
    {
        if (NotebookStudioViewModel == null)
        {
            await _initTask;
        }
        if (NotebookStudioViewModel == null) return;
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

    [RelayCommand]
    public void NavigateToDocs(string? articleId = null)
    {
        if (!string.IsNullOrEmpty(articleId))
        {
            DocsViewModel.SelectTopic(articleId);
        }
        CurrentPage = DocsViewModel;
        IsOnManagerPage = false;
        ActiveDocumentTitle = "Documentation";
    }

    [RelayCommand]
    public void NavigateToBlindProblems(int? problemNumber = null)
    {
        if (problemNumber.HasValue)
        {
            // The page's own row for it (it keeps its own copy of the catalog), so the table highlights that row.
            var problem = BlindProblemsViewModel.AllProblems.FirstOrDefault(p => p.Number == problemNumber.Value);
            if (problem != null)
            {
                BlindProblemsViewModel.SelectedProblem = problem;
            }
        }
        CurrentPage = BlindProblemsViewModel;
        IsOnManagerPage = false;
        ActiveDocumentTitle = "Blind 75";
    }

    [RelayCommand]
    public async Task OpenExistingProjectAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        await ManagerViewModel.OpenExistingProjectAsync(path);
    }

    /// <summary>
    /// Reads the user-configured "ExecutionTimeoutSeconds" plugin setting (see plugin.json), falling
    /// back to the documented default if no settings store was supplied (e.g. the standalone Runner) or
    /// the host hasn't registered one — this never throws and never blocks execution on a missing service.
    /// </summary>
    public int GetExecutionTimeoutSeconds() =>
        _settingsStore?.GetSetting(PluginId, "ExecutionTimeoutSeconds", DefaultExecutionTimeoutSeconds) ?? DefaultExecutionTimeoutSeconds;
}
