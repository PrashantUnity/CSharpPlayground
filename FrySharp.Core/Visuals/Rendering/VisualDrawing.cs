namespace PdfEditorApp.Plugins.CSharpEditor.Visuals.Rendering;

/// <summary>
/// What a view needs to draw a spec: the render model its control takes, any notice for its header, or why it couldn't
/// be built. Preparing touches no control, so a view can do it on any thread.
/// </summary>
public sealed record VisualDrawing(object? Model, string? Notice, string? Error)
{
    public static VisualDrawing Prepare(VisualSpec spec)
    {
        try
        {
            switch (spec)
            {
                case ChartSpec chart:
                    var chartModel = ChartRenderModelBuilder.Build(chart);
                    return new VisualDrawing(chartModel, chartModel.Notice, null);
                case Plot3DSpec plot:
                    var plotModel = Plot3DRenderModelBuilder.Build(plot);
                    return new VisualDrawing(plotModel, plotModel.Notice, null);
                case VisualizerSpec visualizer:
                    var visualizerModel = VisualizerRenderModelBuilder.Build(visualizer);
                    return new VisualDrawing(visualizerModel, visualizerModel.Notice, null);
                default:
                    return new VisualDrawing(null, null, $"{spec.GetType().Name} isn't a visual this studio can draw");
            }
        }
        catch (Exception ex)
        {
            return new VisualDrawing(null, null, ex.Message);
        }
    }
}
