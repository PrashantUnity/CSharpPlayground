using System;
using System.IO;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;
using PdfEditorApp.Plugins.CSharpEditor.Runner;
using PdfEditorApp.Plugins.CSharpEditor.Services.Execution;
using PdfEditorApp.Plugins.CSharpEditor.Services.Roslyn;
using PdfEditorApp.Plugins.CSharpEditor.Services.Storage;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Common;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class NativeMenuContextualTests
{
    private static readonly object InitLock = new();

    private static MainWindowMenuCoordinator GetWindow()
    {
        lock (InitLock)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "NativeMenuTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var storage = new LocalScriptStorageService(tempDir);
            var hostVm = new CSharpStudioHostViewModel(storageService: storage);

            var doc = new ScriptDocumentItem { Title = "TestScript", Code = "Console.WriteLine(1);" };
            var compiler = new RoslynCompilerService();
            var engine = new ScriptExecutionEngine();
            hostVm.CodeStudioViewModel = new CSharpCodeStudioViewModel(
                doc,
                hostVm.StorageService,
                compiler,
                engine,
                backToHubAction: hostVm.NavigateToManager,
                backToHomeAction: hostVm.NavigateToHome,
                languages: hostVm.Languages);

            var nbDoc = new NotebookDocumentItem { Title = "TestNotebook" };
            hostVm.NotebookStudioViewModel = new CSharpNotebookStudioViewModel(
                nbDoc,
                hostVm.StorageService,
                compiler,
                engine,
                backToHubAction: hostVm.NavigateToManager,
                backToHomeAction: hostVm.NavigateToHome,
                languages: hostVm.Languages);

            var menu = MainWindowMenuCoordinator.CreateDefaultMenu();
            var coordinator = new MainWindowMenuCoordinator(menu, hostVm);
            coordinator.UpdateMenuStates();
            return coordinator;
        }
    }

    [Fact]
    public void HubPage_DisablesEditorAndRunMenuItems()
    {
        lock (InitLock)
        {
            var window = GetWindow();
            window.StudioHostVm.NavigateToManager();
            window.UpdateMenuStates();

            // File Menu
            Assert.False(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
            Assert.False(window.GetSubItem("File", "Save As")?.IsEnabled);
            Assert.False(window.GetSubItem("File", "Save All")?.IsEnabled);
            Assert.False(window.GetSubItem("File", "Close Tab")?.IsEnabled);

            // Edit Menu
            Assert.False(window.GetSubItem("Edit", "Replace")?.IsEnabled);
            Assert.False(window.GetSubItem("Edit", "Find in Files")?.IsEnabled);
            Assert.False(window.GetSubItem("Edit", "Format Document")?.IsEnabled);

            // View Menu
            Assert.False(window.GetSubItem("View", "Explorer")?.IsEnabled);
            Assert.False(window.GetSubItem("View", "Search")?.IsEnabled);
            Assert.False(window.GetSubItem("View", "Toggle Primary")?.IsEnabled);
            Assert.False(window.GetSubItem("View", "Toggle Bottom")?.IsEnabled);
            Assert.False(window.GetSubItem("View", "Return to Hub")?.IsEnabled);

            // Run Menu
            Assert.False(window.GetSubItem("Run", "Start Debugging")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Stop Execution")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Step Over")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Step Into")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Toggle Breakpoint")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Clear All Breakpoints")?.IsEnabled);

            // Help Menu
            Assert.False(window.GetSubItem("Help", "Welcome")?.IsEnabled);
        }
    }

    [Fact]
    public void ScriptPage_EnablesEditorAndRunMenuItems()
    {
        lock (InitLock)
        {
            var window = GetWindow();
            var codeVm = window.StudioHostVm.CodeStudioViewModel!;

            window.StudioHostVm.CurrentPage = codeVm;
            window.StudioHostVm.IsOnManagerPage = false;
            window.UpdateMenuStates();

            // File Menu
            Assert.True(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
            Assert.True(window.GetSubItem("File", "Save As")?.IsEnabled);
            Assert.True(window.GetSubItem("File", "Save All")?.IsEnabled);
            Assert.True(window.GetSubItem("File", "Close Tab")?.IsEnabled);

            // Edit Menu
            Assert.True(window.GetSubItem("Edit", "Replace")?.IsEnabled);
            Assert.True(window.GetSubItem("Edit", "Find in Files")?.IsEnabled);
            Assert.True(window.GetSubItem("Edit", "Format Document")?.IsEnabled);

            // View Menu
            Assert.True(window.GetSubItem("View", "Explorer")?.IsEnabled);
            Assert.True(window.GetSubItem("View", "Search")?.IsEnabled);
            Assert.True(window.GetSubItem("View", "Toggle Primary")?.IsEnabled);
            Assert.True(window.GetSubItem("View", "Toggle Bottom")?.IsEnabled);
            Assert.True(window.GetSubItem("View", "Return to Hub")?.IsEnabled);

            // Run Menu (idle)
            Assert.True(window.GetSubItem("Run", "Start Debugging")?.IsEnabled);
            Assert.True(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Stop Execution")?.IsEnabled);
            Assert.True(window.GetSubItem("Run", "Toggle Breakpoint")?.IsEnabled);
        }
    }

    [Fact]
    public void ScriptPage_WhenExecuting_DisablesStartAndEnablesStop()
    {
        lock (InitLock)
        {
            var window = GetWindow();
            var codeVm = window.StudioHostVm.CodeStudioViewModel!;

            window.StudioHostVm.CurrentPage = codeVm;
            window.StudioHostVm.IsOnManagerPage = false;

            codeVm.IsExecuting = true;
            window.UpdateMenuStates();

            try
            {
                Assert.False(window.GetSubItem("Run", "Start Debugging")?.IsEnabled);
                Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
                Assert.True(window.GetSubItem("Run", "Stop Execution")?.IsEnabled);
            }
            finally
            {
                codeVm.IsExecuting = false;
                window.UpdateMenuStates();
            }
        }
    }

    [Fact]
    public void NotebookPage_EnablesCellExecutionAndDisablesDebugger()
    {
        lock (InitLock)
        {
            var window = GetWindow();
            var nbVm = window.StudioHostVm.NotebookStudioViewModel!;

            window.StudioHostVm.CurrentPage = nbVm;
            window.StudioHostVm.IsOnManagerPage = false;
            window.UpdateMenuStates();

            // File Menu
            Assert.True(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
            Assert.True(window.GetSubItem("File", "Close Tab")?.IsEnabled);

            // Edit Menu: Format Document supported for cells, Replace is script-specific
            Assert.True(window.GetSubItem("Edit", "Format Document")?.IsEnabled);
            Assert.False(window.GetSubItem("Edit", "Replace")?.IsEnabled);

            // Run Menu
            Assert.False(window.GetSubItem("Run", "Start Debugging")?.IsEnabled);
            Assert.True(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Stop Execution")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Step Over")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Toggle Breakpoint")?.IsEnabled);
        }
    }

    [Fact]
    public void ServerPage_AdaptsRunControlsToServerLifecycle()
    {
        lock (InitLock)
        {
            var window = GetWindow();
            window.StudioHostVm.NavigateToServerStudio(new FryServerDocumentItem { Title = "TestServer" });
            window.UpdateMenuStates();

            var srvVm = window.StudioHostVm.ServerStudioViewModel!;

            // When stopped
            Assert.True(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Start Debugging")?.IsEnabled);
            Assert.True(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Stop Execution")?.IsEnabled);

            // When running
            srvVm.IsServerRunning = true;
            window.UpdateMenuStates();

            try
            {
                Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
                Assert.True(window.GetSubItem("Run", "Stop Execution")?.IsEnabled);
            }
            finally
            {
                srvVm.IsServerRunning = false;
                window.UpdateMenuStates();
            }
        }
    }

    [Fact]
    public void DocsPage_DisablesSaveAndOwnNavigation()
    {
        lock (InitLock)
        {
            var window = GetWindow();
            window.StudioHostVm.NavigateToDocs();
            window.UpdateMenuStates();

            Assert.False(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
            Assert.True(window.GetSubItem("File", "Close Tab")?.IsEnabled);
            Assert.False(window.GetSubItem("View", "Documentation")?.IsEnabled);
            Assert.False(window.GetSubItem("Help", "Documentation")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
        }
    }

    [Fact]
    public void SettingsPage_DisablesSaveAndOwnNavigation()
    {
        lock (InitLock)
        {
            var window = GetWindow();
            window.StudioHostVm.NavigateToSettings();
            window.UpdateMenuStates();

            Assert.False(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
            Assert.True(window.GetSubItem("File", "Close Tab")?.IsEnabled);
            Assert.False(window.GetSubItem("View", "Settings")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
        }
    }

    [Fact]
    public void BlindProblemsPage_DisablesSaveAndOwnNavigation()
    {
        lock (InitLock)
        {
            var window = GetWindow();
            window.StudioHostVm.NavigateToBlindProblems();
            window.UpdateMenuStates();

            Assert.False(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
            Assert.True(window.GetSubItem("File", "Close Tab")?.IsEnabled);
            Assert.False(window.GetSubItem("View", "Blind 75")?.IsEnabled);
            Assert.False(window.GetSubItem("Help", "Blind 75")?.IsEnabled);
            Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
        }
    }
}
