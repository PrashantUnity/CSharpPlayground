using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PdfEditorApp.Plugins.CSharpEditor.Controls.Visuals;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Display;

/// <summary>
/// The desktop studio's <c>Display</c>: everything <see cref="Scripting.Display"/> shows in every host (images, HTML,
/// tables, charts, visualizers…), plus what only the desktop can show: live Avalonia controls (<see cref="Control"/>,
/// <see cref="Animate"/>), Avalonia bitmaps and GIFs drawn with a <see cref="DrawingContext"/>. A desktop script calls
/// both through this one name.
/// </summary>
public sealed partial class Display : Scripting.Display
{
    private Display()
    {
    }

    public static void Image(Bitmap bitmap)
    {
        Image(EncodePng(bitmap), "PNG");
    }

    internal static byte[] EncodePng(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
#pragma warning disable CS0618
        bitmap.Save(ms);
#pragma warning restore CS0618
        return ms.ToArray();
    }

    /// <summary>
    /// Shows a live control. An Avalonia control belongs to the thread that made it, and the UI can only show controls
    /// made on the UI thread, while a script runs on another one: so make the control with
    /// <see cref="Control{T}(Func{T})"/> (or <see cref="Animate"/>), which builds it on the UI thread. A control already
    /// made on the script's thread is refused with a message that says so, rather than showing nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">The control was made on the script's thread, not the UI thread.</exception>
    public static void Control(Control control) => InteractiveDisplayContext.Emit(ControlOutput(control));

    // The output that shows a control, refusing one the UI can't own.
    internal static RichCellOutput ControlOutput(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!OnUiThread() && control.CheckAccess())
        {
            throw new InvalidOperationException(
                "This control was made on the script's thread, and only controls made on the UI thread can be shown. " +
                "Make it where the UI can own it: Display.Control(() => new MyControl { ... }), or use Display.Animate(...).");
        }

        return new RichCellOutput
        {
            Kind = CellOutputKind.Control,
            InteractiveControl = control
        };
    }

    /// <summary>
    /// Builds a control on the UI thread (where its owner must be) and shows it:
    /// <c>Display.Control(() => new Button { Content = "Hi" });</c>. Everything that sets the control up goes inside
    /// <paramref name="build"/>. Returns the control (to read or dispose it; touch it from other threads only through
    /// <c>Avalonia.Threading.Dispatcher.UIThread</c>).
    /// </summary>
    public static T Control<T>(Func<T> build) where T : Control
    {
        ArgumentNullException.ThrowIfNull(build);
        var control = RunOnUiThread(build);
        Control(control);
        return control;
    }

    // True when the caller is on the UI thread, or the studio has none (a test, a tool): then there is nowhere else to be.
    private static bool OnUiThread() => Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess();

    /// <summary>Runs <paramref name="work"/> on the UI thread and waits for what it returns.</summary>
    internal static T RunOnUiThread<T>(Func<T> work) =>
        OnUiThread() ? work() : Avalonia.Threading.Dispatcher.UIThread.Invoke(work);

    /// <summary>
    /// Primary way to display a live, self-animating visual: hides the DispatcherTimer + repaint
    /// boilerplate behind a single call. <paramref name="onFrame"/> is invoked every frame with the
    /// control's DrawingContext and elapsed time — draw directly into it (immediate-mode, like Avalonia
    /// controls already draw themselves), no per-frame allocation needed. The returned control keeps
    /// animating live because it's the same object reference bound into the cell's output area, driven
    /// by Avalonia's own compositor — it is not re-created or re-serialized on every frame.
    /// Prefer this over a `for` loop calling Display.Image(...) repeatedly: it doesn't hold any
    /// execution lock while animating (the cell's script returns almost immediately after this call),
    /// so it never blocks other tabs, and it keeps animating for as long as you like without needing
    /// cooperative-cancellation checks. See Display.ThrowIfCancellationRequested() for the frame-loop
    /// alternative when you specifically want a bounded, finite sequence of frames instead.
    /// The callback runs on the UI thread. Draw with <c>Brushes.X</c>, <c>ImmutableSolidColorBrush</c> and
    /// <c>ImmutablePen</c>, or make brushes and pens inside the callback (or in the setup step of the other
    /// overload): a brush or pen made in the script belongs to the script's thread and can't be drawn with.
    /// </summary>
    public static AnimatedRenderControl Animate(
        Action<DrawingContext, TimeSpan> onFrame,
        TimeSpan? interval = null,
        double width = 400,
        double height = 300)
    {
        // Built on the UI thread: the control must belong to the thread that shows it.
        var control = RunOnUiThread(() => new AnimatedRenderControl(onFrame, interval, width, height));
        Control(control);
        return control;
    }

    /// <summary>
    /// <see cref="Animate(Action{DrawingContext, TimeSpan}, TimeSpan?, double, double)"/> with a setup step: <paramref name="setup"/>
    /// runs once on the UI thread and what it returns (brushes, pens, geometry, a list of particles…) is handed to every frame.
    /// A brush, pen or geometry made in the script belongs to the script's thread and can't be drawn with on the UI thread,
    /// so make them here, or use <c>Brushes.Red</c>, <c>new ImmutableSolidColorBrush(color)</c> and <c>new ImmutablePen(brush, width)</c>.
    /// </summary>
    public static AnimatedRenderControl Animate<TState>(
        Func<TState> setup,
        Action<DrawingContext, TimeSpan, TState> onFrame,
        TimeSpan? interval = null,
        double width = 400,
        double height = 300)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(onFrame);
        var control = RunOnUiThread(() =>
        {
            var state = setup();
            return new AnimatedRenderControl((ctx, elapsed) => onFrame(ctx, elapsed, state), interval, width, height);
        });
        Control(control);
        return control;
    }
}

/// <summary>What only the desktop can show from a script: Avalonia controls and bitmaps (see <see cref="IScriptHostDisplay"/>).</summary>
internal sealed class DesktopScriptHostDisplay : IScriptHostDisplay
{
    public RichCellOutput? TryCreateElementOutput(object value) =>
        value is Control control ? Display.ControlOutput(control) : null;

    public byte[]? TryEncodeImage(object value) => value is Bitmap bitmap ? Display.EncodePng(bitmap) : null;

    public HostValueDescription? Describe(object value) => value switch
    {
        Control control => new HostValueDescription($"<Avalonia.{control.GetType().Name}>", "UI Control"),
        Bitmap bitmap => new HostValueDescription($"<Bitmap {bitmap.Size.Width}x{bitmap.Size.Height}>", "Image"),
        _ => null
    };

    public string? DescribeUnshareable(Type type)
    {
        if (typeof(Avalonia.Visual).IsAssignableFrom(type)) return "a control";
        if (typeof(IImage).IsAssignableFrom(type)) return "an image";
        return null;
    }
}
