using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.CSharpEditor.Models;

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

            if (TryReadImageHeaderDimensions(path, out var w, out var h))
            {
                ImageDimensionsText = $"{w} × {h} px";
            }

            try
            {
                using var stream = File.OpenRead(path);
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                ActiveImageBitmap = bitmap;
                ImageDimensionsText = $"{bitmap.PixelSize.Width} × {bitmap.PixelSize.Height} px";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CSharpCodeStudioViewModel] Failed to decode bitmap '{path}': {ex.Message}");
                if (ActiveImageBitmap == null && string.IsNullOrEmpty(ImageDimensionsText))
                {
                    ImageErrorText = $"Unable to decode image: {ex.Message}";
                }
            }

            if (activeTab != null)
            {
                activeTab.IsImage = true;
                activeTab.ImageBitmap = ActiveImageBitmap;
                activeTab.ImageDimensionsText = ImageDimensionsText;
                activeTab.ImageFileSizeText = ImageFileSizeText;
                activeTab.ImageFormatText = ImageFormatText;
                activeTab.ImageZoomFactor = ImageZoomFactor;
                activeTab.ImageFitToWindow = ImageFitToWindow;
                activeTab.ShowImageCodeDrawer = ShowImageCodeDrawer;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[CSharpCodeStudioViewModel] Failed to inspect image '{path}': {ex.Message}");
            ImageErrorText = $"Unable to inspect image: {ex.Message}";
            ActiveImageBitmap = null;
        }
    }

    public static bool TryReadImageHeaderDimensions(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var header = new byte[Math.Min(4096, (int)fs.Length)];
            var read = fs.Read(header, 0, header.Length);
            if (read < 10) return false;

            // 1. PNG: 89 50 4E 47 0D 0A 1A 0A
            if (read >= 24 &&
                header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            {
                width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return width > 0 && height > 0;
            }

            // 2. GIF: GIF87a or GIF89a
            if (read >= 10 && header[0] == 'G' && header[1] == 'I' && header[2] == 'F')
            {
                width = header[6] | (header[7] << 8);
                height = header[8] | (header[9] << 8);
                return width > 0 && height > 0;
            }

            // 3. BMP: BM
            if (read >= 26 && header[0] == 'B' && header[1] == 'M')
            {
                width = header[18] | (header[19] << 8) | (header[20] << 16) | (header[21] << 24);
                height = Math.Abs(header[22] | (header[23] << 8) | (header[24] << 16) | (header[25] << 24));
                return width > 0 && height > 0;
            }

            // 4. JPEG: FF D8 ...
            if (read >= 4 && header[0] == 0xFF && header[1] == 0xD8)
            {
                int pos = 2;
                while (pos + 4 < read)
                {
                    if (header[pos] != 0xFF) { pos++; continue; }
                    while (pos < read && header[pos] == 0xFF) pos++;
                    if (pos >= read) break;

                    byte marker = header[pos++];
                    if (marker == 0xD9 || marker == 0xDA) break; // EOI or SOS

                    if (pos + 2 > read) break;
                    int chunkLen = (header[pos] << 8) | header[pos + 1];
                    if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB)
                    {
                        if (pos + 7 <= read)
                        {
                            height = (header[pos + 3] << 8) | header[pos + 4];
                            width = (header[pos + 5] << 8) | header[pos + 6];
                            return width > 0 && height > 0;
                        }
                    }
                    pos += chunkLen;
                }
            }
        }
        catch
        {
            // Ignore header parsing errors; fall back to bitmap decoder
        }
        return false;
    }
}
