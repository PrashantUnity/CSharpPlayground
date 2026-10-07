using PdfEditorApp.Plugins.CSharpEditor.Charting.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using Xunit;

namespace CSharpEditorPlugin.Tests;

/// <summary>The expand button of a 2D chart opens it over the window (it was wired to nothing, unlike 3D plots and visualizers).</summary>
public class ChartFullScreenTests
{
    [Fact]
    public void TheFullScreenCopy_KeepsTheChosenTypeAndGrid_ButOpensFitted()
    {
        var inline = new ChartViewState { OverrideType = ChartType.Bar, OverrideShowGrid = false, Zoom = 3, PanOffsetX = 40, PanOffsetY = -12 };

        var copy = new ChartViewState(inline);

        Assert.Equal(ChartType.Bar, copy.OverrideType);
        Assert.False(copy.OverrideShowGrid);
        Assert.Equal((1.0, 0.0, 0.0), (copy.Zoom, copy.PanOffsetX, copy.PanOffsetY));
        Assert.Equal(3, inline.Zoom); // the inline chart keeps its own view
    }

    [Fact]
    public void AChartOutsideAWindow_HasNothingToOpenOver()
    {
        Assert.Null(ChartFullScreenOverlay.Open(new InteractiveChartControl()));
        Assert.Null(ChartFullScreenOverlay.Open(new InteractiveChartControl(new ChartOptions { Title = "t" })));
    }
}
