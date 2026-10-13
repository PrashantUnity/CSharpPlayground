using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

public enum CellOutputKind
{
    Text,
    Image,
    Html,
    Control,
    Error,
    Table,
    ObjectInspector,
    Chart,
    Visualizer,
    Plot3D
}

public class RichCellOutput
{
    /// <summary>An image output, its pixel size read from the header (no decode).</summary>
    public static RichCellOutput ForImage(byte[] bytes, string format = "PNG")
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var known = ImageHeader.TryReadDimensions(bytes.AsSpan(0, Math.Min(bytes.Length, 64 * 1024)), out int width, out int height);
        return new RichCellOutput
        {
            Kind = CellOutputKind.Image,
            ImageBytes = bytes,
            ImageFormat = format,
            ImageWidth = known ? width : null,
            ImageHeight = known ? height : null
        };
    }

    public CellOutputKind Kind { get; set; } = CellOutputKind.Text;
    public string Text { get; set; } = string.Empty;
    public byte[]? ImageBytes { get; set; }
    public string? ImageFormat { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? HtmlContent { get; set; }

    /// <summary>For a Markdown display (<see cref="MarkdownOutput"/>), the Markdown <see cref="HtmlContent"/> was rendered
    /// from: a host with a Markdown renderer shows this instead.</summary>
    public string? MarkdownSource { get; set; }

    /// <summary>
    /// A live element of the host's UI (on the desktop, an Avalonia control from Display.Control or Display.Animate). Core
    /// never looks inside it; a host that has none never sets it.
    /// </summary>
    public object? InteractiveControl { get; set; }
    public DumpTableResult? TableResult { get; set; }
    public ObjectInspectorNode? InspectorNode { get; set; }

    /// <summary>A chart, 3D plot or visualizer: its spec, which each view draws for itself on the UI thread.</summary>
    public VisualOutput? Visual { get; set; }

    // Convenience getters for XAML DataTemplates (e.g. the Code Studio scratchpad's RichOutputs
    // ItemsControl) — this is a plain POCO set once at emission time and never mutated afterward, so
    // no INotifyPropertyChanged is needed.
    public bool IsImageKind => Kind == CellOutputKind.Image;
    public bool IsHtmlKind => Kind == CellOutputKind.Html;

    /// <summary>A live control from Display.Control or Display.Animate (charts and visualizers are visuals, not controls).</summary>
    public bool IsControlKind => Kind == CellOutputKind.Control;
    public bool IsInspectorKind => Kind == CellOutputKind.ObjectInspector;
    public bool IsTableKind => Kind == CellOutputKind.Table && TableResult != null;
    public bool IsVisualKind => Visual != null;
    public bool IsChartKind => Kind == CellOutputKind.Chart;
    public bool IsVisualizerKind => Kind == CellOutputKind.Visualizer;
    public bool IsPlot3DKind => Kind == CellOutputKind.Plot3D;
}
