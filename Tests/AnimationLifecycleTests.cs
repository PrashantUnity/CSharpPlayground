using System;
using PdfEditorApp.Plugins.CSharpEditor.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels;
using Xunit;

namespace PdfEditorApp.Plugins.CSharpEditor.Tests;

public class AnimationLifecycleTests
{
    private sealed class DisposableTestControl : Avalonia.Controls.Control, IDisposable
    {
        public bool WasDisposed { get; private set; }
        public void Dispose() => WasDisposed = true;
    }

    [Fact]
    public void AnimatedRenderControl_Dispose_IsIdempotentAndDoesNotThrow()
    {
        var control = new AnimatedRenderControl((ctx, t) => { }, interval: TimeSpan.FromMilliseconds(50));
        control.Dispose();
        control.Dispose();
    }

    [Fact]
    public void InteractiveControlLifecycle_DisposeIfNeeded_DisposesOnlyWhenDisposable()
    {
        var disposable = new DisposableTestControl();
        InteractiveControlLifecycle.DisposeIfNeeded(disposable);
        Assert.True(disposable.WasDisposed);

        var nonDisposable = new Avalonia.Controls.Border();
        InteractiveControlLifecycle.DisposeIfNeeded(nonDisposable);
        InteractiveControlLifecycle.DisposeIfNeeded(null);
    }

    [Fact]
    public void SetInteractiveControl_ReplacingControl_DisposesThePreviousOne()
    {
        var cellVm = new NotebookCellViewModel(new NotebookCellItem { Type = CellType.Code, Source = "" });
        var first = new DisposableTestControl();
        var second = new DisposableTestControl();

        cellVm.SetInteractiveControl(first);
        Assert.False(first.WasDisposed);

        cellVm.SetInteractiveControl(second);
        Assert.True(first.WasDisposed);
        Assert.False(second.WasDisposed);
        Assert.True(cellVm.Model.HadInteractiveControl);
    }

    [Fact]
    public void DisposeLiveResources_DisposesCurrentControl()
    {
        var cellVm = new NotebookCellViewModel(new NotebookCellItem { Type = CellType.Code, Source = "" });
        var control = new DisposableTestControl();
        cellVm.SetInteractiveControl(control);

        cellVm.DisposeLiveResources();

        Assert.True(control.WasDisposed);
        Assert.False(cellVm.HasInteractiveControl);
    }

    [Fact]
    public void ClearOutput_DisposesInteractiveControl_AndResetsPlaceholderFlag()
    {
        var cellVm = new NotebookCellViewModel(new NotebookCellItem { Type = CellType.Code, Source = "" });
        var control = new DisposableTestControl();
        cellVm.SetInteractiveControl(control);

        cellVm.ClearOutput();

        Assert.True(control.WasDisposed);
        Assert.False(cellVm.Model.HadInteractiveControl);
        Assert.False(cellVm.InteractiveControlPlaceholderVisible);
    }

    [Fact]
    public void ReloadedCell_WithHadInteractiveControlFlag_ShowsPlaceholder()
    {
        var model = new NotebookCellItem { Type = CellType.Code, Source = "Display.Animate(...)", HadInteractiveControl = true };
        var cellVm = new NotebookCellViewModel(model);

        Assert.True(cellVm.InteractiveControlPlaceholderVisible);
        Assert.True(cellVm.HasOutput);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeleteCell_DisposesItsInteractiveControl()
    {
        var notebook = new NotebookDocumentItem { Title = "Lifecycle Test" };
        var tab = new NotebookTabViewModel(notebook);
        var cell = tab.Cells[0];
        var control = new DisposableTestControl();
        cell.SetInteractiveControl(control);

        tab.DeleteCell(cell);
        await System.Threading.Tasks.Task.CompletedTask;

        Assert.True(control.WasDisposed);
    }

    [Fact]
    public void DisposeAllCellResources_DisposesEveryCellsControl()
    {
        var notebook = new NotebookDocumentItem { Title = "Lifecycle Test" };
        var tab = new NotebookTabViewModel(notebook);
        var controls = new System.Collections.Generic.List<DisposableTestControl>();
        foreach (var cell in tab.Cells)
        {
            var control = new DisposableTestControl();
            cell.SetInteractiveControl(control);
            controls.Add(control);
        }

        tab.DisposeAllCellResources();

        Assert.All(controls, c => Assert.True(c.WasDisposed));
    }
}
