using Avalonia.Controls;
using Avalonia.Layout;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using Xunit;

namespace CSharpEditorPlugin.Tests;

public class VisualChromeAndFullscreenTests
{
    [Fact]
    public void VisualChromeControl_Defaults_HaveExpectedHeights()
    {
        var chrome = new VisualChromeControl();
        Assert.Equal(200, chrome.DefaultCanvasHeight);
        Assert.Equal(80, chrome.MinCanvasHeight);
        Assert.Equal(1200, chrome.MaxCanvasHeight);
    }

    [Fact]
    public void VisualChromeControl_SetCanvasContent_InitializesHeights()
    {
        var chrome = new VisualChromeControl();
        var canvas = new Border { Height = 280 };

        chrome.SetCanvasContent(canvas);

        Assert.Equal(280, chrome.DefaultCanvasHeight);
    }

    [Fact]
    public void VisualChromeControl_SetFullScreenState_TogglesVerticalAlignmentAndHeight()
    {
        var chrome = new VisualChromeControl();
        var canvas = new Border { Height = 220 };
        chrome.SetCanvasContent(canvas);

        chrome.SetFullScreenState(true);

        Assert.Equal(VerticalAlignment.Stretch, chrome.VerticalAlignment);
        Assert.True(double.IsNaN(canvas.Height));
        Assert.Equal(VerticalAlignment.Stretch, canvas.VerticalAlignment);

        chrome.SetFullScreenState(false);

        Assert.Equal(VerticalAlignment.Top, chrome.VerticalAlignment);
        Assert.Equal(220, canvas.Height);
        Assert.Equal(VerticalAlignment.Top, canvas.VerticalAlignment);
    }

    [Fact]
    public void InteractiveVisualizerControl_IsFullScreenView_SwitchesAlignment()
    {
        var options = new VisualizerOptions { Kind = VisualizerKind.Matrix, Title = "BFS Pathfinder" };
        var visualizer = new InteractiveVisualizerControl(options);

        // Inline starts at Top
        Assert.Equal(VerticalAlignment.Top, visualizer.VerticalAlignment);

        // Switching to full screen stretches to fill the window
        visualizer.IsFullScreenView = true;
        Assert.Equal(VerticalAlignment.Stretch, visualizer.VerticalAlignment);

        // Exiting full screen restores Top
        visualizer.IsFullScreenView = false;
        Assert.Equal(VerticalAlignment.Top, visualizer.VerticalAlignment);
    }

    [Theory]
    [InlineData(VisualizerKind.Matrix, 210)]
    [InlineData(VisualizerKind.ArrayPointers, 125)]
    [InlineData(VisualizerKind.Tree, 220)]
    [InlineData(VisualizerKind.Graph, 230)]
    public void InteractiveVisualizerControl_InitializesInlineHeight_PerKind(VisualizerKind kind, double expectedHeight)
    {
        var options = new VisualizerOptions { Kind = kind };
        var visualizer = new InteractiveVisualizerControl(options);

        Assert.Equal(expectedHeight, visualizer.GetInlineCanvasHeight());
    }

    [Fact]
    public void InteractiveVisualizerControl_HonorsCustomSpecifiedHeight()
    {
        var options = new VisualizerOptions { Kind = VisualizerKind.Matrix, Height = 420 };
        var visualizer = new InteractiveVisualizerControl(options);

        Assert.Equal(420, visualizer.GetInlineCanvasHeight());
    }

    [Fact]
    public void VisualChromeControl_CanvasResized_FiresAndUpdatesHeights()
    {
        var chrome = new VisualChromeControl();
        var canvas = new Border { Height = 250 };
        chrome.SetCanvasContent(canvas);

        Assert.Equal(250, chrome.DefaultCanvasHeight);

        chrome.DefaultCanvasHeight = 400;
        Assert.Equal(400, chrome.DefaultCanvasHeight);
    }
}

