using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Common;

/// <summary>
/// Whether the mouse wheel zooms a chart, 3D plot or visualizer. By default it always does. Inside a page that is itself
/// scrolled (the documentation, where a chart sits every few hundred pixels) a control sets <see cref="RequireCtrlProperty"/>:
/// the wheel then scrolls the page, and Ctrl (Cmd on a Mac) with the wheel zooms.
/// </summary>
public static class WheelZoomGate
{
    public static readonly AttachedProperty<bool> RequireCtrlProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, bool>("RequireCtrl", defaultValue: false, inherits: true);

    public static bool GetRequireCtrl(Control control) => control.GetValue(RequireCtrlProperty);

    public static void SetRequireCtrl(Control control, bool value) => control.SetValue(RequireCtrlProperty, value);

    /// <summary>True when a wheel turn with these keys held should zoom <paramref name="control"/>; false leaves the event to scroll the page.</summary>
    public static bool ShouldZoom(Control control, KeyModifiers modifiers) =>
        !GetRequireCtrl(control) || (modifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
}
