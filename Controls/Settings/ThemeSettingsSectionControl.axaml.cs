using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Settings;

/// <summary>
/// Dedicated Theme &amp; Colors settings section, hosting the interactive Harmony Wheel,
/// live IDE Chrome preview, and dynamic token inspector.
/// </summary>
public partial class ThemeSettingsSectionControl : UserControl
{
    public ThemeSettingsSectionControl()
    {
        InitializeComponent();
        // Space generates (Coolors-style) and Ctrl+Z / Ctrl+Shift+Z step through the palette history, unless a text box
        // or a focused button wants the key.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || DataContext is not CSharpSettingsViewModel settings || e.Source is TextBox) return;
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            settings.GeneratePalette();
            e.Handled = true;
        }
        else if (command && e.Key == Key.Z)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) settings.RedoPalette();
            else settings.UndoPalette();
            e.Handled = true;
        }
        else if (command && e.Key == Key.Y)
        {
            settings.RedoPalette();
            e.Handled = true;
        }
    }
}
