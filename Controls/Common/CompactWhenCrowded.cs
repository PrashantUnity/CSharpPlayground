using Avalonia;
using Avalonia.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls;

/// <summary>
/// Adds the <c>compact</c> class to a Border while its content wouldn't fit side by side, so styles can give that content
/// a tighter layout: a toolbar hides its button labels (<c>Border.compact TextBlock.toolbar-label</c>), a header moves its
/// buttons onto a second row. Set <c>controls:CompactWhenCrowded.IsEnabled="True"</c> on the Border; its child must be a
/// Panel whose children sit next to each other at their natural (Auto) widths when there's room.
/// </summary>
public static class CompactWhenCrowded
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Border, bool>("IsEnabled", typeof(CompactWhenCrowded));

    // The width the content needs laid out in full, remembered while it is.
    private static readonly AttachedProperty<double> FullWidthProperty =
        AvaloniaProperty.RegisterAttached<Border, double>("FullWidth", typeof(CompactWhenCrowded));

    static CompactWhenCrowded()
    {
        IsEnabledProperty.Changed.AddClassHandler<Border>((border, e) =>
        {
            if (e.NewValue is true)
            {
                border.SizeChanged += OnSizeChanged;
            }
            else
            {
                border.SizeChanged -= OnSizeChanged;
            }
        });
    }

    public static bool GetIsEnabled(Border border) => border.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(Border border, bool value) => border.SetValue(IsEnabledProperty, value);

    private static void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Border border) Update(border);
    }

    private static void Update(Border border)
    {
        if (border.Child is not Panel content || border.Bounds.Width <= 0) return;

        bool compact = border.Classes.Contains("compact");
        if (!compact)
        {
            // Auto-sized children are measured at their natural width.
            double needed = 0;
            foreach (var child in content.Children)
            {
                if (child.IsVisible) needed += child.DesiredSize.Width;
            }
            border.SetValue(FullWidthProperty, needed);
        }

        double available = border.Bounds.Width - border.Padding.Left - border.Padding.Right;
        bool shouldCompact = border.GetValue(FullWidthProperty) > available;
        if (shouldCompact != compact) border.Classes.Set("compact", shouldCompact);
    }
}
