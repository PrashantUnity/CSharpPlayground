using PdfEditorApp.Plugins.CSharpEditor.Services.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Editor;

/// <summary>
/// Centralized typography and font zoom controller for C# Code Studio, Notebook Studio, and Terminal decks.
/// Standardizes zoom stepping, bounds clamping (9px - 36px), percentage formatting, and debounced settings persistence.
/// </summary>
public static class EditorZoomController
{
    public const double DefaultFontSize = 13.0;
    public const double MinFontSize = 9.0;
    public const double MaxFontSize = 36.0;
    public const double StepSize = 1.0;

    /// <summary>
    /// Increases font size by <see cref="StepSize"/>, clamped to <see cref="MaxFontSize"/>.
    /// </summary>
    public static double ZoomIn(double current) =>
        Math.Min(MaxFontSize, Math.Round(current + StepSize, 1));

    /// <summary>
    /// Decreases font size by <see cref="StepSize"/>, clamped to <see cref="MinFontSize"/>.
    /// </summary>
    public static double ZoomOut(double current) =>
        Math.Max(MinFontSize, Math.Round(current - StepSize, 1));

    /// <summary>
    /// Resets font size to the studio default (<see cref="DefaultFontSize"/>).
    /// </summary>
    public static double Reset() => DefaultFontSize;

    /// <summary>
    /// Clamps an arbitrary font size within the permitted IDE bounds (<see cref="MinFontSize"/> to <see cref="MaxFontSize"/>).
    /// </summary>
    public static double Clamp(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return DefaultFontSize;
        }
        return Math.Clamp(Math.Round(value, 1), MinFontSize, MaxFontSize);
    }

    /// <summary>
    /// Formats font size as an IDE zoom percentage string (e.g. 13.0px -> "100%", 15.0px -> "115%").
    /// </summary>
    public static string FormatPercentage(double fontSize)
    {
        var clamped = Clamp(fontSize);
        var percent = Math.Round((clamped / DefaultFontSize) * 100.0);
        return $"{percent:0}%";
    }

    // ── Debounced Persistence Helper ──
    private static CancellationTokenSource? _saveCts;
    private static readonly object _saveLock = new();

    /// <summary>
    /// Persists the updated font size to <see cref="StudioSettingsStore"/> with a 400ms debounce
    /// so rapid mouse-wheel or keyboard zoom bursts do not hammer disk I/O.
    /// </summary>
    public static void ScheduleSave(StudioSettingsStore? store, double fontSize)
    {
        if (store == null) return;

        var clamped = Clamp(fontSize);

        lock (_saveLock)
        {
            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = new CancellationTokenSource();
            var token = _saveCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(400, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;

                    var settings = store.GetSettings();
                    if (Math.Abs(settings.FontSize - clamped) > 0.05)
                    {
                        settings.FontSize = clamped;
                        store.SaveSettings(settings);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Debounce cancelled by newer zoom action — normal behavior
                }
                catch
                {
                    // Ignore background persistence faults
                }
            }, token);
        }
    }
}
