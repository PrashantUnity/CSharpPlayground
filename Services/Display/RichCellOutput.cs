using Avalonia.Controls;
using Avalonia.Media.Imaging;
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
    public CellOutputKind Kind { get; set; } = CellOutputKind.Text;
    public string Text { get; set; } = string.Empty;
    public byte[]? ImageBytes { get; set; }
    public string? ImageFormat { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? HtmlContent { get; set; }
    public Control? InteractiveControl { get; set; }
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

    private Bitmap? _decodedImage;
    private bool _decodeAttempted;

    /// <summary>Lazily decodes ImageBytes into a Bitmap the first time it's asked for, caching the
    /// result (or the fact that decoding failed) — a DataTemplate binding may evaluate this repeatedly.</summary>
    public Bitmap? DecodedImage
    {
        get
        {
            if (_decodeAttempted) return _decodedImage;
            _decodeAttempted = true;
            if (ImageBytes == null || ImageBytes.Length == 0) return null;
            try
            {
                // At most ImageDecoder.MaxDisplayWidth across: a huge image is not decoded in full to be shown small.
                _decodedImage = ImageDecoder.Decode(ImageBytes);
            }
            catch
            {
                _decodedImage = null;
            }
            return _decodedImage;
        }
    }
}
