using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Notebooks;

/// <summary>
/// Attached behavior implementing CSS <c>position: sticky; top: 0</c> for notebook cell
/// gutter buttons (fold + play).
///
/// Usage (AXAML):
///   &lt;StackPanel nb:StickyGutterBehavior.IsEnabled="True" VerticalAlignment="Top" ... /&gt;
///
/// Algorithm — each gutter gets its OWN independent GutterState, zero shared mutable state:
///   1. On every scroll tick, TranslatePoint from this gutter to the ancestor
///      NotebookCanvasScrollViewer gives the gutter's live viewport Y (includes current tx.Y).
///   2. Subtract tx.Y to get the NATURAL viewport Y (where the gutter sits with no transform).
///   3. If naturalY &lt; 0 the gutter has scrolled above the viewport — push it down by −naturalY
///      to pin at viewport top (Y=0). Clamp to [0, cellHeight − gutterHeight].
/// </summary>
public static class StickyGutterBehavior
{
    // ── Attached Property ─────────────────────────────────────────────────────

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>(
            "IsEnabled",
            typeof(StickyGutterBehavior));

    public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);

    // ── Bootstrap ─────────────────────────────────────────────────────────────

    static StickyGutterBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>((gutter, e) =>
        {
            if (e.NewValue is true)
                Attach(gutter);
            else
                Detach(gutter);
        });
    }

    // ── Instance management ───────────────────────────────────────────────────

    // Keyed by Control instance — each cell's gutter is a separate Control, no sharing.
    private static readonly Dictionary<Control, GutterState> _states = new();

    private static void Attach(Control gutter)
    {
        if (_states.ContainsKey(gutter)) return;

        var tx = new TranslateTransform(0, 0);
        gutter.RenderTransform = tx;
        gutter.RenderTransformOrigin = RelativePoint.TopLeft;

        var state = new GutterState(gutter, tx);
        _states[gutter] = state;

        gutter.AttachedToVisualTree  += state.OnAttachedToVisualTree;
        gutter.DetachedFromVisualTree += state.OnDetachedFromVisualTree;

        if (gutter.IsAttachedToVisualTree())
            state.OnAttachedToVisualTree(gutter, default!);
    }

    private static void Detach(Control gutter)
    {
        if (!_states.TryGetValue(gutter, out var state)) return;
        _states.Remove(gutter);

        gutter.AttachedToVisualTree  -= state.OnAttachedToVisualTree;
        gutter.DetachedFromVisualTree -= state.OnDetachedFromVisualTree;
        state.Unsubscribe();

        if (gutter.RenderTransform is TranslateTransform tx)
            tx.Y = 0;
    }

    // ── Per-gutter state — one private instance per gutter control ────────────

    private sealed class GutterState(Control gutter, TranslateTransform tx)
    {
        private NotebookCanvasScrollViewer? _scrollViewer;

        // ── Visual-tree events ────────────────────────────────────────────────

        public void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            Unsubscribe();
            _scrollViewer = gutter.FindAncestorOfType<NotebookCanvasScrollViewer>();
            if (_scrollViewer is null) return;

            _scrollViewer.OffsetChanged += OnScrollChanged;
            Update();
        }

        public void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
            => Unsubscribe();

        public void Unsubscribe()
        {
            if (_scrollViewer is null) return;
            _scrollViewer.OffsetChanged -= OnScrollChanged;
            _scrollViewer = null;
        }

        // ── Scroll handler ────────────────────────────────────────────────────

        private void OnScrollChanged(object? sender, EventArgs e) => Update();

        // ── Sticky math ───────────────────────────────────────────────────────

        // Small breathing room from the viewport top when the gutter is pinned.
        private const double TopPadding = 6.0;

        private void Update()
        {
            if (_scrollViewer is null) return;

            // Visual tree: gutter → cellGrid (Grid "56,*") → cellRow (per-cell StackPanel)
            if (gutter.Parent is not Panel cellGrid) return;
            if (cellGrid.Parent is not Control cellRow) return;

            double cellHeight = cellRow.Bounds.Height;
            double gutterH    = gutter.Bounds.Height;
            if (cellHeight < 1 || gutterH < 1) return; // not measured yet

            // TranslatePoint gives the gutter's LIVE viewport-Y (already includes current tx.Y).
            var pos = gutter.TranslatePoint(new Point(0, 0), _scrollViewer);
            if (pos is null) return;

            // Remove the current transform to recover the NATURAL viewport Y —
            // where the gutter would appear with absolutely no translation applied.
            //   naturalY > TopPadding  →  below sticky threshold (no movement)
            //   naturalY = TopPadding  →  exactly at threshold (starts sticking)
            //   naturalY < TopPadding  →  above threshold → stick with TopPadding margin
            double naturalY = pos.Value.Y - tx.Y;

            // Pin at TopPadding from the viewport top (not flush against Y=0).
            // When naturalY > TopPadding → needed < 0 → clamped to 0 (no slide).
            // When naturalY < TopPadding → needed > 0 → gutter slides to sit at TopPadding.
            double needed = TopPadding - naturalY;

            // Clamp: 0 = no slide (natural position), maxSlide = gutter at cell bottom.
            double maxSlide = Math.Max(0, cellHeight - gutterH);
            tx.Y = Math.Clamp(needed, 0, maxSlide);
        }
    }
}
