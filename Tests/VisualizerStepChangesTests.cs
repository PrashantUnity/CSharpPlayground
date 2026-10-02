using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Building;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>
/// A step says only what changed since the one before. The literal null once became a value there (every cell of every
/// step was "set to null"), so a 500-step grid was 14 MB, and in-process every cell a step didn't touch read "null".
/// </summary>
public class VisualizerStepChangesTests
{
    private static VisualizerSpec Grid500()
    {
        var tracker = MatrixTracker.CreateEmpty(30, 30, "500 steps");
        for (int i = 0; i < 500; i++) tracker.Visit(i / 30, i % 30, $"step {i}");
        return VisualizerOptionsConverter.ToSpec(tracker.Options);
    }

    [Fact]
    public void AStep_SaysOnlyWhatChanged()
    {
        var spec = Grid500();

        Assert.All(spec.Steps.Skip(1), step => Assert.InRange(step.Changes?.Cells?.Count ?? 0, 1, 2));
        Assert.True(VisualJson.SerializeToUtf8Bytes(spec).Length < 300 * 1024, "a step of two cells should not cost 28 KB");
    }

    // The view draws a C# visual from its spec, without JSON in between.
    [Fact]
    public void ACellAStepDoesntTouch_KeepsItsValue_InTheSpecItself()
    {
        var tracker = MatrixTracker.Create(new[,] { { 1, 2 }, { 3, 4 } }, "values");
        tracker.Visit(0, 0, "first");
        tracker.Visit(1, 1, "last");

        var drawn = VisualizerRenderModelBuilder.Build(VisualizerOptionsConverter.ToSpec(tracker.Options));

        var last = Assert.IsType<GridMatrixData>(drawn.Sequence!.Steps[^1].Snapshot);
        Assert.Equal(["1", "2", "3", "4"], new[] { last[0, 0], last[0, 1], last[1, 0], last[1, 1] }.Select(c => c.DisplayValue));
    }

    // In a change, null means "unchanged", so a value that becomes null can't be a change: the step carries its state.
    [Fact]
    public void AValueThatBecomesNull_IsSentAsTheWholeState()
    {
        var grid = new GridMatrixData(1, 2);
        grid[0, 0].RawValue = 5;
        grid[0, 0].DisplayValue = "5";
        var emptied = grid.Clone();
        emptied[0, 0].RawValue = null;
        emptied[0, 0].DisplayValue = "null";
        var options = new VisualizerOptions
        {
            Kind = VisualizerKind.Matrix,
            MatrixData = grid,
            Sequence = new VisualizerSequence([
                new VisualizerStep(0, "five", VisualizerKind.Matrix) { Snapshot = grid.Clone() },
                new VisualizerStep(1, "emptied", VisualizerKind.Matrix) { Snapshot = emptied }
            ])
        };

        var spec = VisualizerOptionsConverter.ToSpec(options);

        Assert.Null(spec.Steps[1].Changes);
        Assert.Equal(ScalarValue.Null, spec.Steps[1].State!.Grid!.Values[0][0]);
        var drawn = Assert.IsType<GridMatrixData>(VisualizerRenderModelBuilder.Build(VisualJson.Clone(spec)).Sequence!.Steps[1].Snapshot);
        Assert.Equal("null", drawn[0, 0].DisplayValue);
    }

    // `changed ? value : null` must mean "no value", which it only does while null can't become a ScalarValue on its own.
    [Fact]
    public void NullDoesntTurnIntoAValue_WithoutBeingAskedTo()
    {
        var implicitFromText = typeof(ScalarValue).GetMethods()
            .Where(m => m.Name == "op_Implicit" && m.GetParameters()[0].ParameterType == typeof(string));

        Assert.Empty(implicitFromText);
    }
}
