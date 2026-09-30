using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Material.Icons;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Json;
using PdfEditorApp.Plugins.CSharpEditor.Visuals.Output;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

public partial class VisualChromeControl : UserControl
{
    public event EventHandler? ResetFitRequested;
    public event EventHandler? FullScreenToggleRequested;
    public event EventHandler? FullscreenRequested { add => FullScreenToggleRequested += value; remove => FullScreenToggleRequested -= value; }

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
    }

    private void Bind(string name, Action act) { if (this.FindControl<Button>(name) is { } b) b.Click += (_, _) => act(); }
    private void BindAsync(string name, Func<Task> act) { if (this.FindControl<Button>(name) is { } b) b.Click += async (_, _) => await act(); }

    public void SetHeader(string title, string? subtitle, string? stats, string? notice, MaterialIconKind icon)
    {
        if (this.FindControl<TextBlock>("TitleText") is { } t) t.Text = string.IsNullOrWhiteSpace(title) ? "Visual" : title;
        if (this.FindControl<TextBlock>("SubtitleText") is { } s) { s.Text = subtitle; s.IsVisible = !string.IsNullOrWhiteSpace(subtitle); }
        if (this.FindControl<Border>("StatsPillBorder") is { } sp && this.FindControl<TextBlock>("StatsSummaryText") is { } st)
        {
            sp.IsVisible = !string.IsNullOrWhiteSpace(stats);
            if (!string.IsNullOrWhiteSpace(stats)) st.Text = stats;
        }
        if (this.FindControl<TextBlock>("NoticeText") is { } n) { n.Text = notice; n.IsVisible = !string.IsNullOrWhiteSpace(notice); }
        if (this.FindControl<Material.Icons.Avalonia.MaterialIcon>("HeaderIcon") is { } hi) hi.Kind = icon;
    }

    private static void Detach(Control? c)
    {
        if (c?.Parent is Panel p) p.Children.Remove(c);
        else if (c?.Parent is ContentControl cc) cc.Content = null;
        else if (c?.Parent is Border b) b.Child = null;
    }

    public void SetCanvasContent(Control? content) { Detach(content); if (this.FindControl<ContentControl>("CanvasHost") is { } host) host.Content = content; }
    public void SetKindTools(Control? tools) { Detach(tools); if (this.FindControl<ContentControl>("KindToolsHost") is { } host) host.Content = tools; }
    public void SetFooter(Control? footer) { Detach(footer); if (this.FindControl<ContentControl>("FooterHost") is { } host) { host.Content = footer; host.IsVisible = footer != null; } }

    public void SetFullScreenState(bool isFullScreen)
    {
        if (this.FindControl<Material.Icons.Avalonia.MaterialIcon>("FullscreenIcon") is { } icon)
            icon.Kind = isFullScreen ? MaterialIconKind.FullscreenExit : MaterialIconKind.Fullscreen;
        if (this.FindControl<Button>("FullscreenBtn") is { } btn)
            ToolTip.SetTip(btn, isFullScreen ? "Exit Fullscreen (Esc)" : "Fullscreen / Pop-Out");
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
