namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

/// <summary>
/// GIF export of a recorded visualizer: the frames are drawn by the desktop's visualizer renderer, so it exists only in
/// the desktop studio. <c>recorder.SaveGif(...)</c> reads the same as before it moved out of <see cref="VisualizerRecorder"/>.
/// </summary>
public static class VisualizerRecorderGifExtensions
{
    /// <summary>Exports the recorded visualizer steps into an animated GIF file.</summary>
    public static Task SaveGifAsync(this VisualizerRecorder recorder, string path, VisualizerGifExportOptions? exportOptions = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        return VisualizerGifExportService.ExportToFileAsync(recorder.Options, path, exportOptions, progress, cancellationToken);
    }

    /// <summary>Exports the recorded visualizer steps into an animated GIF file synchronously.</summary>
    public static void SaveGif(this VisualizerRecorder recorder, string path, VisualizerGifExportOptions? exportOptions = null) =>
        recorder.SaveGifAsync(path, exportOptions).GetAwaiter().GetResult();

    /// <summary>Exports the recorded visualizer steps into animated GIF bytes.</summary>
    public static Task<byte[]> ToGifBytesAsync(this VisualizerRecorder recorder, VisualizerGifExportOptions? exportOptions = null, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        return VisualizerGifExportService.ExportToGifBytesAsync(recorder.Options, exportOptions, progress, cancellationToken);
    }
}
