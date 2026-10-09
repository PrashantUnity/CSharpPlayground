using PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using Xunit;
using NotebookCellViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookCellViewModel;
using NotebookTabViewModel = PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks.NotebookTabViewModel;

namespace CSharpEditorPlugin.Tests;

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
        Assert.False((bool)cellVm.HasInteractiveControl);
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
        Assert.False((bool)cellVm.InteractiveControlPlaceholderVisible);
    }

    [Fact]
    public void ReloadedCell_WithHadInteractiveControlFlag_ShowsPlaceholder()
    {
        var model = new NotebookCellItem { Type = CellType.Code, Source = "Display.Animate(...)", HadInteractiveControl = true };
        var cellVm = new NotebookCellViewModel(model);

        Assert.True((bool)cellVm.InteractiveControlPlaceholderVisible);
        Assert.True((bool)cellVm.HasOutput);
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

    [Fact]
    public void AFrameCallbackThatThrows_StopsTheAnimation_AndSaysWhy()
    {
        var frames = 0;
        var control = new AnimatedRenderControl((_, _) => { frames++; throw new InvalidOperationException("boom"); }, interval: TimeSpan.FromMilliseconds(50));
        Assert.Null(control.Failure);

        Assert.False(control.RunFrame(null!));

        Assert.Equal(1, frames);
        Assert.Contains("boom", control.Failure);
        Assert.Contains("InvalidOperationException", control.Failure);
        Assert.False(control.IsTicking);
    }

    [Fact]
    public void Stop_FreezesTheAnimation_ButItStillDrawsItsLastFrame()
    {
        var times = new List<TimeSpan>();
        var control = new AnimatedRenderControl((_, elapsed) => times.Add(elapsed), interval: TimeSpan.FromMilliseconds(50));

        control.Stop();
        control.Stop(); // idempotent

        Assert.True(control.IsStopped);
        Assert.False(control.IsTicking);
        Assert.True(control.RunFrame(null!)); // a repaint still draws the frame, with the time it stopped at
        Assert.True(control.RunFrame(null!));
        Assert.Equal(times[0], times[1]);
    }

    [Fact]
    public void AFrameCallbackThatWorks_KeepsAnimating()
    {
        var control = new AnimatedRenderControl((_, _) => { }, interval: TimeSpan.FromMilliseconds(50));

        Assert.True(control.RunFrame(null!));
        Assert.Null(control.Failure);
    }

    [Fact]
    public async Task DisplayAnimate_ShowsTheControlItMade_WhenTheScriptRunsOnAnotherThread()
    {
        RichCellOutput? shown = null;
        using var scope = InteractiveDisplayContext.EnterScope(output => shown = output);

        // (Without a studio there is no UI thread to build on, so this proves the call and its output; the real window is
        // checked with UiSnapshots: docs --article "Live Animations" --run.)
        var control = await Task.Run(() => Display.Animate((_, _) => { }, width: 120, height: 80));

        Assert.Same(control, shown!.InteractiveControl);
        Assert.Equal(CellOutputKind.Control, shown.Kind);
    }

    [Fact]
    public void DisplayControl_WithAFactory_ReturnsTheControlItShows()
    {
        RichCellOutput? shown = null;
        using var scope = InteractiveDisplayContext.EnterScope(output => shown = output);

        var button = Display.Control(() => new Avalonia.Controls.Button { Width = 70 });

        Assert.Same(button, shown!.InteractiveControl);
        Assert.Equal(70, button.Width);
    }
}
