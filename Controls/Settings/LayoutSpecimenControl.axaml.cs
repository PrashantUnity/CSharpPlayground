using System.Collections.Generic;
using Avalonia.Controls;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Settings;

/// <summary>
/// The Layout page's preview: the levers' layout shown before the studio takes it. Its tokens live in its own resources,
/// replaced in one go on every lever move, so only this small preview re-resolves while a slider is dragged.
/// </summary>
public partial class LayoutSpecimenControl : UserControl
{
    private readonly ThemeLayerProvider _preview = new();
    private CSharpSettingsViewModel? _settings;

    public LayoutSpecimenControl()
    {
        InitializeComponent();
        Resources.MergedDictionaries.Add(_preview);
        DataContextChanged += (_, _) => Hook(DataContext as CSharpSettingsViewModel);
    }

    private void Hook(CSharpSettingsViewModel? settings)
    {
        if (_settings != null) _settings.LayoutPreviewChanged -= Show;
        _settings = settings;
        if (settings == null) return;
        settings.LayoutPreviewChanged += Show;
        Show(settings.LayoutPreviewTokens);
    }

    private void Show(IReadOnlyDictionary<string, object> tokens)
    {
        var values = new Dictionary<object, object?>(tokens.Count);
        foreach (var (key, value) in tokens) values[key] = value;
        _preview.Replace(values);
    }
}
