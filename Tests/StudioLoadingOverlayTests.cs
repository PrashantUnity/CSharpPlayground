using System;
using Avalonia.Controls;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class StudioLoadingOverlayTests
{
    private sealed class TestLoadingState : IStudioLoadingState
    {
        public bool IsLoading { get; set; }
        public string LoadingTitle { get; set; } = "Loading...";
        public string LoadingSubtitle { get; set; } = string.Empty;
    }

    [Fact]
    public void BeginLoading_SetsProperties_AndDisposingResetsLoading()
    {
        var state = new TestLoadingState();
        Assert.False(state.IsLoading);

        using (state.BeginLoading("Opening File...", "test.frynb"))
        {
            Assert.True(state.IsLoading);
            Assert.Equal("Opening File...", state.LoadingTitle);
            Assert.Equal("test.frynb", state.LoadingSubtitle);
        }

        Assert.False(state.IsLoading);
    }

    [Fact]
    public void BeginLoading_ResetsOnException()
    {
        var state = new TestLoadingState();
        try
        {
            using (state.BeginLoading("Processing...", "step 1"))
            {
                Assert.True(state.IsLoading);
                throw new InvalidOperationException("Simulated error");
            }
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        Assert.False(state.IsLoading);
    }

    [Fact]
    public void HostViewModel_ImplementsIStudioLoadingState()
    {
        var host = new CSharpStudioHostViewModel(blindProgress: new LocalBlindProgressService());
        Assert.IsAssignableFrom<IStudioLoadingState>(host);
        Assert.False(host.IsLoading);

        using (host.BeginLoading("Opening Studio...", "Roslyn"))
        {
            Assert.True(host.IsLoading);
            Assert.Equal("Opening Studio...", host.LoadingTitle);
            Assert.Equal("Roslyn", host.LoadingSubtitle);
        }

        Assert.False(host.IsLoading);
    }

    [Fact]
    public void ManagerViewModel_ImplementsIStudioLoadingState()
    {
        var storage = new LocalScriptStorageService();
        var manager = new CSharpManagerViewModel(
            storage,
            openScriptAction: _ => { },
            openNotebookAction: _ => { },
            navigateToHomeAction: () => { });

        Assert.IsAssignableFrom<IStudioLoadingState>(manager);

        using (manager.BeginLoading("Opening Project...", "Workspace1"))
        {
            Assert.True(manager.IsLoading);
            Assert.Equal("Opening Project...", manager.LoadingTitle);
            Assert.Equal("Workspace1", manager.LoadingSubtitle);
        }

        Assert.False(manager.IsLoading);
    }

    [Fact]
    public void OverlayControl_DefaultsAndPropertiesWork()
    {
        var control = new StudioLoadingOverlayControl();
        Assert.False(control.IsLoading);
        Assert.Equal("Loading...", control.LoadingTitle);
        Assert.Equal(string.Empty, control.LoadingSubtitle);

        control.IsLoading = true;
        control.LoadingTitle = "Saving...";
        control.LoadingSubtitle = "file.cs";
        control.IconKind = MaterialIconKind.SyncCircle;

        Assert.True(control.IsLoading);
        Assert.Equal("Saving...", control.LoadingTitle);
        Assert.Equal("file.cs", control.LoadingSubtitle);
        Assert.Equal(MaterialIconKind.SyncCircle, control.IconKind);
    }

    [Fact]
    public async System.Threading.Tasks.Task NotebookStudio_OpenDocumentAsync_SetsLoadingDuringFileTransition()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NotebookLoadingTest_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var storage = new LocalScriptStorageService(tempDir);
            var initial = new NotebookDocumentItem { Title = "Initial Notebook" };
            var studio = new CSharpNotebookStudioViewModel(
                initial,
                storage,
                new RoslynCompilerService(),
                new ScriptExecutionEngine(),
                backToHubAction: () => { },
                backToHomeAction: () => { });

            bool wasLoadingDuringOpen = false;
            string? capturedTitle = null;
            studio.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IStudioLoadingState.IsLoading) && studio.IsLoading)
                {
                    wasLoadingDuringOpen = true;
                    capturedTitle = studio.LoadingTitle;
                }
            };

            var item = new ExplorerItemViewModel
            {
                Name = "MachineLearning.frynb",
                DocumentId = "ml_doc_1",
                FileExtension = ".frynb",
                IsDirectory = false
            };

            await studio.OpenDocumentAsync(item);

            Assert.True(wasLoadingDuringOpen, "NotebookStudio should set IsLoading=true during OpenDocumentAsync");
            Assert.Equal("Opening File...", capturedTitle);
            Assert.False(studio.IsLoading, "IsLoading should be false after OpenDocumentAsync completes");
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task CodeStudio_SwitchToScriptAsync_SetsLoadingDuringFileTransition()
    {
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CodeLoadingTest_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(tempDir);
        try
        {
            var storage = new LocalScriptStorageService(tempDir);
            var script = new ScriptDocumentItem { Title = "Script 1", Code = "// code" };
            await storage.SaveScriptAsync(script);

            var secondScript = new ScriptDocumentItem { Title = "Script 2", Code = "// script 2" };
            await storage.SaveScriptAsync(secondScript);

            var studio = new CSharpCodeStudioViewModel(
                script,
                storage,
                new RoslynCompilerService(),
                new ScriptExecutionEngine(),
                backToHubAction: () => { },
                backToHomeAction: () => { });

            bool wasLoadingDuringSwitch = false;
            string? capturedTitle = null;
            studio.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IStudioLoadingState.IsLoading) && studio.IsLoading)
                {
                    wasLoadingDuringSwitch = true;
                    capturedTitle = studio.LoadingTitle;
                }
            };

            var item = new ExplorerItemViewModel
            {
                Name = "Script 2.frycs",
                DocumentId = secondScript.Id,
                FileExtension = ".frycs",
                IsDirectory = false
            };

            await studio.SwitchToScriptAsync(item);

            Assert.True(wasLoadingDuringSwitch, "CodeStudio should set IsLoading=true during SwitchToScriptAsync");
            Assert.Equal("Loading File...", capturedTitle);
            Assert.False(studio.IsLoading, "IsLoading should be false after SwitchToScriptAsync completes");
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
            {
                System.IO.Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
