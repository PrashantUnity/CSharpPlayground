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
/// (see InteractiveControlLifecycle). The elapsed time handed to the callback counts only the time the animation is
/// running, so it carries on where it left off when its page comes back instead of jumping ahead. A frame callback
/// that throws stops the animation and says why on the canvas (it used to leave an empty canvas and a debug line).
/// Must be created on the UI thread: Display.Animate does that for a script.
/// </summary>
public class AnimatedRenderControl : Control, IDisposable
{
    private readonly Action<DrawingContext, TimeSpan> _onFrame;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private bool _disposed;
    private bool _stopped;
    private string? _failure;

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
        _clock.Stop();
        _timer.Stop();
    }

    private void UpdateTimer()
    {
        if (_disposed || _stopped || _failure != null) return;
        if (IsEffectivelyVisible)
        {
            _clock.Start();
            _timer.Start();
        }
        else
        {
            _clock.Stop();
            _timer.Stop();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_disposed) return;

        if (_failure != null)
        {
            DrawFailure(context);
            return;
        }

        if (!RunFrame(context)) DrawFailure(context);
    }

    // Runs the callback for one frame. A callback that throws would only throw again next frame, so it ends the animation
    // and the canvas says what went wrong where the animation was.
    internal bool RunFrame(DrawingContext context)
    {
        try
        {
            _onFrame(context, _clock.Elapsed);
            return true;
        }
        catch (Exception ex)
        {
            _failure = $"The animation's frame callback threw {ex.GetType().Name}: {ex.Message}{Hint(ex)}";
            Debug.WriteLine($"[Display.Animate] {_failure}\n{ex}");
            _clock.Stop();
            _timer.Stop();
            return false;
        }
    }

    // The usual cause of "a different thread owns it": a brush, pen or geometry made in the script and drawn with here.
    private static string Hint(Exception ex) => ex is InvalidOperationException && ex.Message.Contains("different thread", StringComparison.OrdinalIgnoreCase)
        ? "\n\nA brush, pen or geometry made in the script belongs to the script's thread. Make it inside the callback, " +
          "use Brushes.X, new ImmutableSolidColorBrush(color) and new ImmutablePen(brush, width), or make it in the setup step of Display.Animate(setup, frame)."
        : string.Empty;

    private void DrawFailure(DrawingContext context)
    {
        var area = new Rect(Bounds.Size);
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(255, 42, 20, 24)), new Pen(new SolidColorBrush(Color.FromArgb(255, 244, 63, 94)), 1), area.Deflate(1));
        var text = new FormattedText(_failure!, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), 12, new SolidColorBrush(Color.FromArgb(255, 252, 165, 165)))
        {
            MaxTextWidth = Math.Max(20, area.Width - 24),
            MaxTextHeight = Math.Max(20, area.Height - 24)
        };
        context.DrawText(text, new Point(12, 12));
    }

    /// <summary>
    /// Stops the animation and keeps its last frame on screen (drawn again whenever the canvas repaints, with the time it
    /// stopped at). Unlike <see cref="Dispose"/> it can be called from a script and leaves something to look at.
    /// </summary>
    public void Stop()
    {
        if (_disposed || _stopped) return;
        _stopped = true;
        _clock.Stop();
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            _timer.Stop();
            InvalidateVisual();
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                _timer.Stop();
                InvalidateVisual();
            });
        }
    }

    /// <summary>True once <see cref="Stop"/> has frozen the animation.</summary>
    public bool IsStopped => _stopped;

    /// <summary>The message of the exception the frame callback threw, when it did (the animation has stopped); otherwise null.</summary>
    public string? Failure => _failure;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // A script disposes from its own thread, and the timer belongs to the UI thread's dispatcher.
        if (Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess()) _timer.Stop();
        else Dispatcher.UIThread.Post(_timer.Stop);
        _clock.Stop();
    }
}
