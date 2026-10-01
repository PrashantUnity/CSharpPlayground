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
}
