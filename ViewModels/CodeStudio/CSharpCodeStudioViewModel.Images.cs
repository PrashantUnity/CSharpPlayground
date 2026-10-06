using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;
using PdfEditorApp.Plugins.CSharpEditor.Services.Activities;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.CodeStudio;

public partial class CSharpCodeStudioViewModel
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp", ".svg", ".tiff", ".tif"
    };

    public static bool IsImageExtension(string? extension) =>
        !string.IsNullOrEmpty(extension) && ImageExtensions.Contains(extension);

    public static bool IsImagePath(string? path) =>
        !string.IsNullOrEmpty(path) && IsImageExtension(Path.GetExtension(path));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCodeEditor))]
    [NotifyPropertyChangedFor(nameof(RuntimeLabel))]
    private bool _isActiveDocumentImage;

    public bool ShowCodeEditor => !IsActiveDocumentImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageDisplayWidth))]
    [NotifyPropertyChangedFor(nameof(ImageDisplayHeight))]
    private Avalonia.Media.Imaging.Bitmap? _activeImageBitmap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeLabel))]
    private string _imageDimensionsText = string.Empty;

    [ObservableProperty]
    private string _imageFileSizeText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuntimeLabel))]
    private string _imageFormatText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageZoomPercentageText))]
    [NotifyPropertyChangedFor(nameof(ImageDisplayWidth))]
    [NotifyPropertyChangedFor(nameof(ImageDisplayHeight))]
    private double _imageZoomFactor = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageZoomPercentageText))]
    [NotifyPropertyChangedFor(nameof(ImageDisplayWidth))]
    [NotifyPropertyChangedFor(nameof(ImageDisplayHeight))]
    private bool _imageFitToWindow = true;

    [ObservableProperty]
    private bool _showImageCodeDrawer;

    [ObservableProperty]
    private string? _imageErrorText;

    public string ImageZoomPercentageText => ImageFitToWindow ? "Fit" : $"{Math.Round(ImageZoomFactor * 100)}%";

    public double ImageDisplayWidth
    {
        get
        {
            if (ImageFitToWindow || ActiveImageBitmap == null) return double.NaN;
            return ActiveImageBitmap.PixelSize.Width * ImageZoomFactor;
        }
    }

    public double ImageDisplayHeight
    {
        get
        {
            if (ImageFitToWindow || ActiveImageBitmap == null) return double.NaN;
            return ActiveImageBitmap.PixelSize.Height * ImageZoomFactor;
        }
    }

    [RelayCommand]
    public void ImageZoomIn()
    {
        ImageFitToWindow = false;
        ImageZoomFactor = Math.Min(Math.Round(ImageZoomFactor * 1.25, 2), 10.0);
    }

    [RelayCommand]
    public void ImageZoomOut()
    {
        ImageFitToWindow = false;
        ImageZoomFactor = Math.Max(Math.Round(ImageZoomFactor / 1.25, 2), 0.1);
    }

    [RelayCommand]
    public void ImageResetZoom()
    {
        ImageFitToWindow = false;
        ImageZoomFactor = 1.0;
    }

    [RelayCommand]
    public void ImageToggleFit()
    {
        ImageFitToWindow = !ImageFitToWindow;
        if (!ImageFitToWindow) ImageZoomFactor = 1.0;
    }

    [RelayCommand]
    public void ToggleImageCodeDrawer()
    {
        ShowImageCodeDrawer = !ShowImageCodeDrawer;
    }

    [RelayCommand]
    public async Task CopyActiveImagePathAsync()
    {
        if (Script?.SourceFilePath is { } path)
        {
            await CopyTextToClipboardAsync(path);
        }
    }

    [RelayCommand]
    public async Task CopyActiveImageRelativePathAsync()
    {
        if (Script?.SourceFilePath is { } path)
        {
            var rel = Path.GetRelativePath(_storageService.ActiveWorkspaceRootPath, path).Replace(Path.DirectorySeparatorChar, '/');
            await CopyTextToClipboardAsync(rel);
        }
    }

    [RelayCommand]
    public async Task CopyImageCodeSnippetAsync(string? kind)
    {
        if (Script?.SourceFilePath is not { } path) return;
        var rel = Path.GetRelativePath(_storageService.ActiveWorkspaceRootPath, path).Replace(Path.DirectorySeparatorChar, '/');

        string snippet = kind?.ToLowerInvariant() switch
        {
            "csharp-skia" or "skia" => $$"""
                using SkiaSharp;
                using var stream = System.IO.File.OpenRead("{{rel}}");
                using var bitmap = SKBitmap.Decode(stream);
                """,
            "csharp-bytes" or "bytes" => $$"""
                byte[] bytes = System.IO.File.ReadAllBytes("{{rel}}");
                """,
            "csharp-bitmap" or "avalonia" => $$"""
                using var stream = System.IO.File.OpenRead("{{rel}}");
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                """,
            "python-pil" or "pil" or "python" => $$"""
                from PIL import Image
                img = Image.open("{{rel}}")
                """,
            _ => $$"""
                // Image Asset: {{Path.GetFileName(path)}}
                // Relative Path: {{rel}}
                """
        };

        await CopyTextToClipboardAsync(snippet);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F2} MB";
    }

    private void UpdateImageStateForDocument(ScriptDocumentItem document)
    {
        var path = document.SourceFilePath;
        var ext = Path.GetExtension(path ?? document.Title);

        if (!IsImageExtension(ext))
        {
            IsActiveDocumentImage = false;
            ActiveImageBitmap = null;
            ImageDimensionsText = string.Empty;
            ImageFileSizeText = string.Empty;
            ImageFormatText = string.Empty;
            ImageErrorText = null;
            ShowImageCodeDrawer = false;
            return;
        }

        IsActiveDocumentImage = true;
        ImageErrorText = null;
        ImageFormatText = ext.TrimStart('.').ToUpperInvariant() + " Image";

        // Check if current tab already cached the bitmap
        var activeTab = OpenTabs.FirstOrDefault(t => t.Id == document.Id);
        if (activeTab?.ImageBitmap != null)
        {
            ActiveImageBitmap = activeTab.ImageBitmap;
            ImageDimensionsText = activeTab.ImageDimensionsText;
            ImageFileSizeText = activeTab.ImageFileSizeText;
            ImageFormatText = activeTab.ImageFormatText;
            ImageZoomFactor = activeTab.ImageZoomFactor;
            ImageFitToWindow = activeTab.ImageFitToWindow;
            ShowImageCodeDrawer = activeTab.ShowImageCodeDrawer;
            return;
        }

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            ImageErrorText = "Image file not found on disk.";
            ActiveImageBitmap = null;
            return;
        }

        try
        {
            var info = new FileInfo(path);
            ImageFileSizeText = FormatFileSize(info.Length);
            ImageFitToWindow = true;
            ImageZoomFactor = 1.0;
            ActiveImageBitmap = null;

            // The size comes from the header at once; the pixels are decoded off the UI thread (a camera photo took
            // ~6 s here before), at most ImageDecoder.MaxDisplayWidth across.
            if (ImageDecoder.TryReadDimensions(path, out var w, out var h))
            {
                ImageDimensionsText = $"{w} × {h} px";
            }

            if (activeTab != null)
            {
                activeTab.IsImage = true;
                activeTab.ImageDimensionsText = ImageDimensionsText;
                activeTab.ImageFileSizeText = ImageFileSizeText;
                activeTab.ImageFormatText = ImageFormatText;
                activeTab.ImageZoomFactor = ImageZoomFactor;
                activeTab.ImageFitToWindow = ImageFitToWindow;
                activeTab.ShowImageCodeDrawer = ShowImageCodeDrawer;
            }

            PendingImageDecode = DecodeActiveImageAsync(document.Id, path, _imageDecode.Begin());
            PendingImageDecode.FireAndForget(_activities, $"Opening {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpCodeStudioViewModel] Failed to inspect image '{path}': {ex.Message}");
            ImageErrorText = $"Unable to inspect image: {ex.Message}";
            ActiveImageBitmap = null;
        }
    }

    // Opening another image (or another tab) while one decodes: the older decode is dropped, never shown.
    private readonly LatestOperation _imageDecode = new();

    /// <summary>The image being decoded in the background, if any (tests await it).</summary>
    public Task PendingImageDecode { get; private set; } = Task.CompletedTask;

    private async Task DecodeActiveImageAsync(string documentId, string path, CancellationToken token)
    {
        using var activity = _activities.Start(new ActivityOptions($"Opening {Path.GetFileName(path)}", ActivityLocation.Editor), token);
        Avalonia.Media.Imaging.Bitmap bitmap;
        try
        {
            bitmap = await ImageDecoder.DecodeFileAsync(path, cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpCodeStudioViewModel] Failed to decode bitmap '{path}': {ex.Message}");
            if (!token.IsCancellationRequested && string.Equals(Script?.Id, documentId, StringComparison.Ordinal))
            {
                ImageErrorText = $"Unable to decode image: {ex.Message}";
            }

            return;
        }

        var tab = OpenTabs.FirstOrDefault(t => t.Id == documentId);
        if (token.IsCancellationRequested || tab == null)
        {
            bitmap.Dispose();
            return;
        }

        // The tab keeps it, so switching back shows it without decoding again.
        tab.ImageBitmap = bitmap;
        if (string.Equals(Script?.Id, documentId, StringComparison.Ordinal))
        {
            ActiveImageBitmap = bitmap;
            if (string.IsNullOrEmpty(ImageDimensionsText)) ImageDimensionsText = $"{bitmap.PixelSize.Width} × {bitmap.PixelSize.Height} px";
            tab.ImageDimensionsText = ImageDimensionsText;
        }
    }

    public static bool TryReadImageHeaderDimensions(string path, out int width, out int height) =>
        ImageDecoder.TryReadDimensions(path, out width, out height);
}
