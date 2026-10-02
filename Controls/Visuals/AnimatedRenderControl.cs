using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;

/// <summary>
/// Hides the DispatcherTimer + InvalidateVisual boilerplate for a self-repainting animation.
/// Returned by Display.Animate(...) in Services/InteractiveDisplayService.cs; can also be
/// instantiated directly by notebook/scratchpad code that wants more control.
///
/// Immediate-mode drawing (a callback receiving the DrawingContext each frame) rather than a
/// retained scene graph — no per-frame allocation, matching how Avalonia controls already draw
/// themselves. Disposing stops the timer for good; it also pauses by itself while detached from the
/// visual tree (e.g. the app closes the window it's hosted in) or hidden (its page is not the one on
/// screen) as a safety net, in addition to the explicit disposal wired into cell/tab teardown
/// (see InteractiveControlLifecycle).
/// </summary>
public class AnimatedRenderControl : Control, IDisposable
{
    private readonly Action<DrawingContext, TimeSpan> _onFrame;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _disposed;

    public AnimatedRenderControl(
        Action<DrawingContext, TimeSpan> onFrame,
        TimeSpan? interval = null,
        double width = 400,
        double height = 300)
    {
        _onFrame = onFrame ?? throw new ArgumentNullException(nameof(onFrame));
        Width = width;
        Height = height;
        ClipToBounds = true;

        // Background, not Render: a decorative animation must not outrank the host's own input handling.
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval ?? TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _timer.Tick += (_, _) => InvalidateVisual();
    }

    /// <summary>True while the animation is actually ticking (attached, visible and not disposed).</summary>
    public bool IsTicking => _timer.IsEnabled;

    // Ticks only while on screen. Pages are kept alive when hidden (they stay in the visual tree), so "attached"
    // alone no longer means "visible": the timer follows IsEffectivelyVisible as well.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateTimer();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // Avalonia keeps IsEffectivelyVisibleProperty internal, but it still reports the change by name.
        if (change.Property.Name == nameof(IsEffectivelyVisible)) UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    private void UpdateTimer()
    {
        if (_disposed) return;
        if (IsEffectivelyVisible) _timer.Start();
        else _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_disposed) return;

        try
        {
            _onFrame(context, _clock.Elapsed);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Display.Animate] frame callback threw: {ex}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
    }
}
