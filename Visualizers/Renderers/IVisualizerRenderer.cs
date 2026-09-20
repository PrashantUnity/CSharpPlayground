using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public interface IVisualizerRenderer
{
    void Render(DrawingContext context, Rect bounds, VisualizerOptions options);
    VisualizerHitTestResult? HitTest(Point pointerPosition, Rect bounds, VisualizerOptions options);
}
