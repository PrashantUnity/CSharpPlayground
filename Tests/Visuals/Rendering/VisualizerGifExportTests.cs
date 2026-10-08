using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Headless;
using CSharpEditorPlugin.Tests.TestSupport;
using PdfEditorApp.Plugins.CSharpEditor.Services.Display;
using PdfEditorApp.Plugins.CSharpEditor.Services.Media.Gif;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;
using Xunit;

namespace CSharpEditorPlugin.Tests.Visuals.Rendering;

public class VisualizerGifExportTests
{
    private static void EnsureAvaloniaInitialized() =>
        TestHeadlessApp.EnsureInitialized();

    [Fact]
    public async Task ExportToGifBytesAsync_ArrayVisualizer_ProducesValidMultiFrameGif()
    {
        var prevSync = System.Threading.SynchronizationContext.Current;
        EnsureAvaloniaInitialized();
        if (System.Threading.SynchronizationContext.Current is Avalonia.Threading.AvaloniaSynchronizationContext)
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(prevSync);
        }

        // Build a 3-step sequence
        var steps = new[]
        {
            new VisualizerStep { Description = "Initial array", StepIndex = 0, SourceLine = 12 },
            new VisualizerStep { Description = "Swap elements at 0 and 1", StepIndex = 1, SourceLine = 15 },
            new VisualizerStep { Description = "Sorted array", StepIndex = 2, SourceLine = 18 }
        };

        var options = new VisualizerOptions
        {
            Kind = VisualizerKind.ArrayPointers,
            Title = "Bubble Sort Step Demo",
            ArrayData = new ArrayPointerData
            {
                Items = [new ArrayItemData(0, "42"), new ArrayItemData(1, "99")]
            },
            Sequence = new VisualizerSequence(steps)
        };

        var exportOptions = new VisualizerGifExportOptions
        {
            Width = 320,
            Height = 180,
            StepDelay = TimeSpan.FromMilliseconds(200),
            IncludeBanner = true
        };

        byte[] gifBytes = await VisualizerGifExportService.ExportToGifBytesAsync(options, exportOptions);
        Assert.NotEmpty(gifBytes);
        string header = Encoding.ASCII.GetString(gifBytes, 0, 6);
        Assert.Equal("GIF89a", header);

        Assert.True(ImageDecoder.TryReadDimensions(gifBytes, out int width, out int height));
        Assert.Equal(320, width);
        Assert.Equal(180, height);

        string ascii = Encoding.ASCII.GetString(gifBytes);
        Assert.Contains("NETSCAPE2.0", ascii);
        Assert.Equal(0x3B, gifBytes[^1]);
    }
}


