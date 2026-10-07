using Avalonia.Controls;
using Avalonia.Input;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Settings;

/// <summary>The palette's sections (core roles, syntax, chart series): lock, regenerate one, or type a hex to set one.</summary>
public partial class ThemePaletteSectionsControl : UserControl
{
    public ThemePaletteSectionsControl()
    {
        InitializeComponent();
    }

    private void OnHexBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is CSharpSettingsViewModel settings && (sender as Control)?.DataContext is PaletteSectionItemViewModel section)
        {
            settings.SetSectionColor(section);
            e.Handled = true;
        }
    }
}
