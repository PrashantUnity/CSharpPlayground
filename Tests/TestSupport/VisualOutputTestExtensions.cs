using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;
using Xunit;

namespace CSharpEditorPlugin.Tests.TestSupport;

/// <summary>
/// A visual output as a test looks at it: its spec (the contract), what a view draws from that spec (the render model),
/// or the C# model it was made from.
/// </summary>
internal static class VisualOutputTestExtensions
{
    public static ChartSpec ChartSpec(this RichCellOutput output) => Assert.IsType<ChartSpec>(output.Visual?.Spec);
    public static Plot3DSpec Plot3DSpec(this RichCellOutput output) => Assert.IsType<Plot3DSpec>(output.Visual?.Spec);
    public static VisualizerSpec VisualizerSpec(this RichCellOutput output) => Assert.IsType<VisualizerSpec>(output.Visual?.Spec);

    /// <summary>The chart as a view draws it.</summary>
    public static ChartOptions ChartModel(this RichCellOutput output) => ChartRenderModelBuilder.Build(output.ChartSpec());

    public static Plot3DOptions Plot3DModel(this RichCellOutput output) => Plot3DRenderModelBuilder.Build(output.Plot3DSpec());

    public static VisualizerOptions VisualizerModel(this RichCellOutput output) => VisualizerRenderModelBuilder.Build(output.VisualizerSpec());

}
