using Avalonia;
using Avalonia.Media;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

public interface IPlot3DRenderer
{
    void Render(DrawingContext context, Rect bounds, Plot3DOptions options);
    Plot3DHitTestResult? HitTest(Point pos, Rect bounds, Plot3DOptions options);
}
