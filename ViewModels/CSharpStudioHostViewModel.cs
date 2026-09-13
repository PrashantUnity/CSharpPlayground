using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels;

public partial class CSharpStudioHostViewModel : ObservableObject
{
    private readonly IScriptStorageService _storageService;
    private readonly RoslynCompilerService _compilerService;
    private readonly ScriptExecutionEngine _executionEngine;

    [ObservableProperty]
    private object _currentPage;

    [ObservableProperty]
    private bool _isOnManagerPage = true;

    public CSharpManagerViewModel ManagerViewModel { get; }
    public CSharpCodeStudioViewModel CodeStudioViewModel { get; }
    public CSharpNotebookStudioViewModel NotebookStudioViewModel { get; }

    public CSharpStudioHostViewModel(IServiceProvider? serviceProvider = null)
    {
        _storageService = new LocalScriptStorageService();
        _compilerService = new RoslynCompilerService();
        _executionEngine = new ScriptExecutionEngine();

        ManagerViewModel = new CSharpManagerViewModel(
            _storageService,
            openScriptAction: NavigateToCodeStudio,
            openNotebookAction: NavigateToNotebookStudio);

        var initialScript = new ScriptDocumentItem
        {
            Title = "1. Two Sum (Algorithm Workspace)",
            Code = CodeTemplateLibrary.GetTemplates()[0].InitialCode,
            Notes = CodeTemplateLibrary.GetTemplates()[0].Notes
        };

        CodeStudioViewModel = new CSharpCodeStudioViewModel(
            initialScript,
            _storageService,
            _compilerService,
            _executionEngine,
            backToHubAction: NavigateToManager);

        var initialNotebook = new NotebookDocumentItem
        {
            Title = "Document Automation Notebook"
        };

        NotebookStudioViewModel = new CSharpNotebookStudioViewModel(
            initialNotebook,
            _storageService,
            _compilerService,
            _executionEngine,
            backToHubAction: NavigateToManager);

        _currentPage = ManagerViewModel;
    }

    public void NavigateToCodeStudio(ScriptDocumentItem script)
    {
        CodeStudioViewModel.UpdateActiveScript(script);
        CurrentPage = CodeStudioViewModel;
        IsOnManagerPage = false;
    }

    public void NavigateToNotebookStudio(NotebookDocumentItem notebook)
    {
        NotebookStudioViewModel.UpdateActiveNotebook(notebook);
        CurrentPage = NotebookStudioViewModel;
        IsOnManagerPage = false;
    }

    public void NavigateToManager()
    {
        _ = ManagerViewModel.LoadWorkspaceItemsAsync();
        CurrentPage = ManagerViewModel;
        IsOnManagerPage = true;
    }
}
