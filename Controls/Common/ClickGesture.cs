using Avalonia;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// A left press and release in (nearly) the same place: a click, as against a drag that pans, orbits or scrubs. Canvases
/// that drag keep their drag; a click is what is left when the pointer didn't move.
/// </summary>
internal sealed class ClickGesture
{
    // How far the pointer may move between press and release and still click (a hand is never perfectly still).
    private const double Slop = 4;

    private Point? _pressedAt;

    public void Pressed(PointerPressedEventArgs e, Visual relativeTo)
    {
        _pressedAt = e.GetCurrentPoint(relativeTo).Properties.IsLeftButtonPressed ? e.GetPosition(relativeTo) : null;
    }

    /// <summary>Where the click was, when this release ends one.</summary>
    public Point? Released(PointerReleasedEventArgs e, Visual relativeTo)
    {
        var pressedAt = _pressedAt;
        _pressedAt = null;
        if (pressedAt is not { } from || e.InitialPressMouseButton != MouseButton.Left) return null;
        var at = e.GetPosition(relativeTo);
        return Math.Abs(at.X - from.X) <= Slop && Math.Abs(at.Y - from.Y) <= Slop ? at : null;
    }

    /// <summary>The keys held, by the names every language's events use.</summary>
    public static IReadOnlyList<string> Names(KeyModifiers modifiers)
    {
        var names = new List<string>(4);
        if (modifiers.HasFlag(KeyModifiers.Control)) names.Add("ctrl");
        if (modifiers.HasFlag(KeyModifiers.Shift)) names.Add("shift");
        if (modifiers.HasFlag(KeyModifiers.Alt)) names.Add("alt");
        if (modifiers.HasFlag(KeyModifiers.Meta)) names.Add("meta");
        return names;
    }

    /// <summary>A click that adds to or takes from a selection rather than being a click of its own.</summary>
    public static bool Selects(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta) || modifiers.HasFlag(KeyModifiers.Shift);
}

/// <summary>An element of a visual was clicked: what was under the pointer, and the keys held.</summary>
public sealed class ElementClickedEventArgs<THit>(THit hit, KeyModifiers modifiers) : EventArgs
{
    public THit Hit { get; } = hit;
    public KeyModifiers Modifiers { get; } = modifiers;
}
