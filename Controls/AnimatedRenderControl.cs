using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

/// <summary>
/// Hides the DispatcherTimer + InvalidateVisual boilerplate for a self-repainting animation.
/// Returned by Display.Animate(...) in Services/InteractiveDisplayService.cs; can also be
/// instantiated directly by notebook/scratchpad code that wants more control.
///
/// Immediate-mode drawing (a callback receiving the DrawingContext each frame) rather than a
/// retained scene graph — no per-frame allocation, matching how Avalonia controls already draw
/// themselves. Disposing stops the timer; it also stops itself automatically when detached from the
/// visual tree (e.g. the app closes the window it's hosted in) as a safety net, in addition to the
/// explicit disposal wired into cell/tab teardown (see InteractiveControlLifecycle).
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

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = interval ?? TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _timer.Tick += (_, _) => InvalidateVisual();
        _timer.Start();

        DetachedFromVisualTree += (_, _) => Dispose();
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
