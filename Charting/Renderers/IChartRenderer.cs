using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting.Renderers;

public interface IChartRenderer
{
    void Render(DrawingContext context, Rect bounds, ChartOptions options);
    ChartHitTestResult? HitTest(Point pointerPosition, Rect bounds, ChartOptions options);
}
