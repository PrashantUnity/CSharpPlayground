using System;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using PdfEditorApp.Plugins.CSharpEditor.Models.Server;
using PdfEditorApp.Plugins.CSharpEditor.Runner;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class NativeMenuContextualTests
{
    private static readonly object InitLock = new();
    private static bool _isAvaloniaInitialized;

    private static void EnsureHeadlessApp()
    {
        lock (InitLock)
        {
            if (_isAvaloniaInitialized || Application.Current != null) return;
            AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();
            _isAvaloniaInitialized = true;
        }
    }

    private static void PumpUntil(Func<bool> condition, int timeoutMs = 15000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
                throw new TimeoutException("Condition not met within timeout.");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    private static MainWindow CreateTestWindow()
    {
        EnsureHeadlessApp();
        var window = new MainWindow();
        PumpUntil(() => window.StudioHostVm.CodeStudioViewModel != null && window.StudioHostVm.NotebookStudioViewModel != null);
        window.UpdateMenuStates();
        return window;
    }

    [Fact]
    public void HubPage_DisablesEditorAndRunMenuItems()
    {
        var window = CreateTestWindow();
        window.StudioHostVm.NavigateToManager();
        Dispatcher.UIThread.RunJobs();
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

    [Fact]
    public void ScriptPage_EnablesEditorAndRunMenuItems()
    {
        var window = CreateTestWindow();
        var codeVm = window.StudioHostVm.CodeStudioViewModel!;

        window.StudioHostVm.CurrentPage = codeVm;
        window.StudioHostVm.IsOnManagerPage = false;
        Dispatcher.UIThread.RunJobs();
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

    [Fact]
    public void ScriptPage_WhenExecuting_DisablesStartAndEnablesStop()
    {
        var window = CreateTestWindow();
        var codeVm = window.StudioHostVm.CodeStudioViewModel!;

        window.StudioHostVm.CurrentPage = codeVm;
        window.StudioHostVm.IsOnManagerPage = false;

        codeVm.IsExecuting = true;
        window.UpdateMenuStates();

        Assert.False(window.GetSubItem("Run", "Start Debugging")?.IsEnabled);
        Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
        Assert.True(window.GetSubItem("Run", "Stop Execution")?.IsEnabled);
    }

    [Fact]
    public void NotebookPage_EnablesCellExecutionAndDisablesDebugger()
    {
        var window = CreateTestWindow();
        var nbVm = window.StudioHostVm.NotebookStudioViewModel!;

        window.StudioHostVm.CurrentPage = nbVm;
        window.StudioHostVm.IsOnManagerPage = false;
        Dispatcher.UIThread.RunJobs();
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

    [Fact]
    public void ServerPage_AdaptsRunControlsToServerLifecycle()
    {
        var window = CreateTestWindow();
        window.StudioHostVm.NavigateToServerStudio(new FryServerDocumentItem { Title = "TestServer" });
        Dispatcher.UIThread.RunJobs();
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

        Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
        Assert.True(window.GetSubItem("Run", "Stop Execution")?.IsEnabled);
    }

    [Fact]
    public void DocsPage_DisablesSaveAndOwnNavigation()
    {
        var window = CreateTestWindow();
        window.StudioHostVm.NavigateToDocs();
        Dispatcher.UIThread.RunJobs();
        window.UpdateMenuStates();

        Assert.False(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
        Assert.True(window.GetSubItem("File", "Close Tab")?.IsEnabled);
        Assert.False(window.GetSubItem("View", "Documentation")?.IsEnabled);
        Assert.False(window.GetSubItem("Help", "Documentation")?.IsEnabled);
        Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
    }

    [Fact]
    public void SettingsPage_DisablesSaveAndOwnNavigation()
    {
        var window = CreateTestWindow();
        window.StudioHostVm.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();
        window.UpdateMenuStates();

        Assert.False(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
        Assert.True(window.GetSubItem("File", "Close Tab")?.IsEnabled);
        Assert.False(window.GetSubItem("View", "Settings")?.IsEnabled);
        Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
    }

    [Fact]
    public void BlindProblemsPage_DisablesSaveAndOwnNavigation()
    {
        var window = CreateTestWindow();
        window.StudioHostVm.NavigateToBlindProblems();
        Dispatcher.UIThread.RunJobs();
        window.UpdateMenuStates();

        Assert.False(window.GetSubItem("File", "Save", exact: true)?.IsEnabled);
        Assert.True(window.GetSubItem("File", "Close Tab")?.IsEnabled);
        Assert.False(window.GetSubItem("View", "Blind 75")?.IsEnabled);
        Assert.False(window.GetSubItem("Help", "Blind 75")?.IsEnabled);
        Assert.False(window.GetSubItem("Run", "Run Without Debugging")?.IsEnabled);
    }
}
