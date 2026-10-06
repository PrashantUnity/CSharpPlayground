using Avalonia.Controls;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Settings;

/// <summary>
/// Reusable control evaluating the design system under dual WCAG 2.1 &amp; APCA (WCAG 3.0) contrast
/// and CIELAB Delta E distinction across color vision deficiencies.
/// </summary>
public partial class ThemeAccessibilityMatrixControl : UserControl
{
    public ThemeAccessibilityMatrixControl()
    {
        InitializeComponent();
    }
}
