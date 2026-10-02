using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Notebooks;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Notebooks;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;

public partial class VisualChromeControl : UserControl
{
    private Control? _canvasContent;
    private double _defaultCanvasHeight = 200;
    private double _minCanvasHeight = 80;
    private double _maxCanvasHeight = 1200;
    private double _currentCanvasHeight = 200;
    private bool _hasUserResized;

    public event EventHandler? ResetFitRequested;
    public event EventHandler? FullScreenToggleRequested;
    public event EventHandler? FullscreenRequested { add => FullScreenToggleRequested += value; remove => FullScreenToggleRequested -= value; }
    public event EventHandler<double>? CanvasResized;

    public double DefaultCanvasHeight
    {
        get => _defaultCanvasHeight;
        set
        {
            _defaultCanvasHeight = value;
            if (!_hasUserResized)
            {
                _currentCanvasHeight = value;
                if (Find<Border>("CanvasContainerBorder") is { } border)
                    border.Height = value;
                if (_canvasContent != null && !double.IsNaN(_canvasContent.Height))
                    _canvasContent.Height = value;
            }
        }
    }

    public double MinCanvasHeight { get => _minCanvasHeight; set => _minCanvasHeight = value; }
    public double MaxCanvasHeight { get => _maxCanvasHeight; set => _maxCanvasHeight = value; }

    public Func<VisualSpec?>? SpecGetter { get; set; }
    public Func<string?>? DataCsvGetter { get; set; }
    public Func<Task>? CustomPngSaver { get; set; }

    public VisualChromeControl()
    {
        try { InitializeComponent(); WireToolbarEvents(); }
        catch { /* Headless test runner */ }
    }

    private void WireToolbarEvents()
    {
        Bind("ResetFitBtn", () => ResetFitRequested?.Invoke(this, EventArgs.Empty));
        Bind("FullscreenBtn", () => FullScreenToggleRequested?.Invoke(this, EventArgs.Empty));
        BindAsync("CopySpecBtn", CopySpecToClipboardAsync);
        BindAsync("CopyDataBtn", CopyDataToClipboardAsync);
        BindAsync("SavePngBtn", SavePngAsync);
        WireResizeGrip();
    }

    private T? Find<T>(string name) where T : Control { try { return this.FindControl<T>(name); } catch { return null; } }

    private void WireResizeGrip()
    {
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true, RoutingStrategies.Bubble, handledEventsToo: true);

        var grip = Find<Border>("ResizeGrip");
        if (grip == null) return;

        grip.AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true, RoutingStrategies.Bubble, handledEventsToo: true);
        grip.Focusable = true;

        bool dragging = false;
        double startHeight = 0;
        double startY = 0;
        ScrollViewer? parentScroller = null;
        double savedScrollOffset = 0;

        double PointerY(PointerEventArgs e) =>
            TopLevel.GetTopLevel(this) is { } top ? e.GetPosition(top).Y : e.GetPosition(this).Y;

        grip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
            var border = Find<Border>("CanvasContainerBorder");
            if (border == null) return;

            // Shift focus onto grip so any off-screen text editor in earlier cells is blurred
            grip.Focus();

            // Notify parent notebook to activate this cell if hosted in a notebook cell
            try
            {
                if (this.FindAncestorOfType<NotebookCellOutputControl>()?.DataContext is NotebookCellViewModel cellVm)
                {
                    if (this.FindAncestorOfType<Views.CSharpNotebookStudioView>()?.DataContext is CSharpNotebookStudioViewModel studioVm)
                    {
                        studioVm.SelectCell(cellVm);
                    }
                    else
                    {
                        cellVm.IsSelected = true;
                    }
                }
            }
            catch { }

            // Lock scroll offset during drag so VirtualizingStackPanel cannot shift the viewport
            parentScroller = this.FindAncestorOfType<ScrollViewer>();
            savedScrollOffset = parentScroller?.Offset.Y ?? 0;

            dragging = true;
            startHeight = border.Bounds.Height > 0 ? border.Bounds.Height : (double.IsNaN(border.Height) ? _currentCanvasHeight : border.Height);
            startY = PointerY(e);
            e.Pointer.Capture(grip);
            e.Handled = true;
        };

        grip.PointerMoved += (_, e) =>
        {
            if (!dragging) return;
            var border = Find<Border>("CanvasContainerBorder");
            if (border == null) return;

            double newHeight = Math.Clamp(startHeight + PointerY(e) - startY, _minCanvasHeight, _maxCanvasHeight);
            _hasUserResized = true;
            _currentCanvasHeight = newHeight;
            border.Height = newHeight;
            if (_canvasContent != null && !double.IsNaN(_canvasContent.Height))
                _canvasContent.Height = newHeight;

            // Preserve scroll position throughout the drag interaction
            if (parentScroller != null && Math.Abs(parentScroller.Offset.Y - savedScrollOffset) > 0.5)
            {
                parentScroller.Offset = new Vector(parentScroller.Offset.X, savedScrollOffset);
            }

            CanvasResized?.Invoke(this, newHeight);
            e.Handled = true;
        };

        void EndDrag(PointerEventArgs? e)
        {
            if (!dragging) return;
            dragging = false;

            if (parentScroller != null && Math.Abs(parentScroller.Offset.Y - savedScrollOffset) > 0.5)
            {
                parentScroller.Offset = new Vector(parentScroller.Offset.X, savedScrollOffset);
            }
            parentScroller = null;

            if (e != null)
            {
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        grip.PointerReleased += (_, e) => EndDrag(e);
        grip.PointerCaptureLost += (_, _) => EndDrag(null);

        grip.DoubleTapped += (_, e) =>
        {
            var border = Find<Border>("CanvasContainerBorder");
            if (border == null) return;

            parentScroller = this.FindAncestorOfType<ScrollViewer>();
            savedScrollOffset = parentScroller?.Offset.Y ?? 0;

            _hasUserResized = false;
            _currentCanvasHeight = _defaultCanvasHeight;
            border.Height = _defaultCanvasHeight;
            if (_canvasContent != null && !double.IsNaN(_canvasContent.Height))
                _canvasContent.Height = _defaultCanvasHeight;

            if (parentScroller != null && Math.Abs(parentScroller.Offset.Y - savedScrollOffset) > 0.5)
            {
                parentScroller.Offset = new Vector(parentScroller.Offset.X, savedScrollOffset);
            }

            CanvasResized?.Invoke(this, _defaultCanvasHeight);
            e.Handled = true;
        };
    }

    private void Bind(string name, Action act) { if (Find<Button>(name) is { } b) b.Click += (_, _) => act(); }
    private void BindAsync(string name, Func<Task> act) { if (Find<Button>(name) is { } b) b.Click += async (_, _) => await act(); }

    public void SetHeader(string title, string? subtitle, string? stats, string? notice, MaterialIconKind icon)
    {
        if (Find<TextBlock>("TitleText") is { } t) t.Text = string.IsNullOrWhiteSpace(title) ? "Visual" : title;
        if (Find<TextBlock>("SubtitleText") is { } s) { s.Text = subtitle; s.IsVisible = !string.IsNullOrWhiteSpace(subtitle); }
        if (Find<Border>("StatsPillBorder") is { } sp && Find<TextBlock>("StatsSummaryText") is { } st)
        {
            sp.IsVisible = !string.IsNullOrWhiteSpace(stats);
            if (!string.IsNullOrWhiteSpace(stats)) st.Text = stats;
        }
        if (Find<TextBlock>("NoticeText") is { } n) { n.Text = notice; n.IsVisible = !string.IsNullOrWhiteSpace(notice); }
        if (Find<Material.Icons.Avalonia.MaterialIcon>("HeaderIcon") is { } hi) hi.Kind = icon;
    }

    private static void Detach(Control? c)
    {
        if (c?.Parent is Panel p) p.Children.Remove(c);
        else if (c?.Parent is ContentControl cc) cc.Content = null;
        else if (c?.Parent is Border b) b.Child = null;
    }

    public void SetCanvasContent(Control? content)
    {
        Detach(content);
        _canvasContent = content;
        if (content != null)
        {
            content.HorizontalAlignment = HorizontalAlignment.Stretch;
            content.VerticalAlignment = VerticalAlignment.Stretch;
            if (content.Height > 0 && !double.IsNaN(content.Height))
            {
                _currentCanvasHeight = content.Height;
                _defaultCanvasHeight = content.Height;
            }
        }
        if (Find<Border>("CanvasContainerBorder") is { } border)
        {
            border.Height = _currentCanvasHeight;
        }
        if (Find<ContentControl>("CanvasHost") is { } host) host.Content = content;
    }

    public void SetKindTools(Control? tools) { Detach(tools); if (Find<ContentControl>("KindToolsHost") is { } host) host.Content = tools; }
    public void SetFooter(Control? footer) { Detach(footer); if (Find<ContentControl>("FooterHost") is { } host) { host.Content = footer; host.IsVisible = footer != null; } }

    public void SetFullScreenState(bool isFullScreen)
    {
        if (Find<Material.Icons.Avalonia.MaterialIcon>("FullscreenIcon") is { } icon)
            icon.Kind = isFullScreen ? MaterialIconKind.FullscreenExit : MaterialIconKind.Fullscreen;
        if (Find<Button>("FullscreenBtn") is { } btn)
            ToolTip.SetTip(btn, isFullScreen ? "Exit Fullscreen (Esc)" : "Fullscreen / Pop-Out");
        if (Find<Border>("ResizeGrip") is { } grip)
            grip.IsVisible = !isFullScreen;

        VerticalAlignment = isFullScreen ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        if (Find<Border>("CanvasContainerBorder") is { } border)
        {
            border.Height = isFullScreen ? double.NaN : _currentCanvasHeight;
            border.VerticalAlignment = isFullScreen ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        }
        if (_canvasContent != null)
        {
            _canvasContent.Height = isFullScreen ? double.NaN : _currentCanvasHeight;
            _canvasContent.VerticalAlignment = isFullScreen ? VerticalAlignment.Stretch : VerticalAlignment.Top;
        }
    }

    public async Task CopySpecToClipboardAsync()
    {
        var spec = SpecGetter?.Invoke();
        if (spec == null) return;
        try
        {
            var mime = VisualMimeTypes.For(spec.Family);
            var specJson = VisualJson.Serialize(spec);
            using var doc = JsonDocument.Parse(specJson);
            var bundle = new Dictionary<string, object>
            {
                [mime] = doc.RootElement.Clone(),
                ["text/plain"] = string.IsNullOrWhiteSpace(spec.Title) ? spec.Family.ToString() : spec.Title
            };
            var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb) await cb.SetTextAsync(json);
        }
        catch { }
    }

    public async Task CopyDataToClipboardAsync()
    {
        var csv = DataCsvGetter?.Invoke();
        if (string.IsNullOrEmpty(csv)) return;
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } cb) await cb.SetTextAsync(csv);
        }
        catch { }
    }

    public async Task SavePngAsync()
    {
        if (CustomPngSaver != null) { await CustomPngSaver(); return; }
        var target = this.FindControl<Border>("CanvasContainerBorder") ?? (Control)this;
        var w = (int)Math.Max(120, target.Bounds.Width);
        var h = (int)Math.Max(120, target.Bounds.Height);
        try
        {
            using var rtb = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
            rtb.Render(target);
            var top = TopLevel.GetTopLevel(this);
            if (top?.StorageProvider is { CanSave: true } sp)
            {
                var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Visual Image",
                    DefaultExtension = "png",
                    SuggestedFileName = "visual.png",
                    FileTypeChoices = [new FilePickerFileType("PNG Image") { Patterns = ["*.png"] }]
                });
                if (file != null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    rtb.Save(stream, new PngBitmapEncoderOptions());
                }
            }
        }
        catch { }
    }
}
