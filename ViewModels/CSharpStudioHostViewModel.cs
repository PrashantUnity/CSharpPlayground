using System;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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

    [ObservableProperty]
    private string _activeDocumentTitle = "Hub";

    /// <summary>
    /// Action callback for standalone test runners or host shells to close the preview window.
    /// </summary>
    public Action? RequestClose { get; set; }

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
            openNotebookAction: NavigateToNotebookStudio,
            navigateToHomeAction: NavigateToHome);

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
            backToHubAction: NavigateToManager,
            backToHomeAction: NavigateToHome);

        var initialNotebook = new NotebookDocumentItem
        {
            Title = "Document Automation Notebook"
        };

        NotebookStudioViewModel = new CSharpNotebookStudioViewModel(
            initialNotebook,
            _storageService,
            _compilerService,
            _executionEngine,
            backToHubAction: NavigateToManager,
            backToHomeAction: NavigateToHome);

        _currentPage = ManagerViewModel;
        _activeDocumentTitle = "Hub";
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
        CodeStudioViewModel.UpdateActiveScript(script);
        CurrentPage = CodeStudioViewModel;
        IsOnManagerPage = false;
        ActiveDocumentTitle = string.IsNullOrWhiteSpace(script.Title) ? "Untitled Script" : script.Title;
    }

    public void NavigateToNotebookStudio(NotebookDocumentItem notebook)
    {
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
