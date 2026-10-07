using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

namespace PdfEditorApp.Plugins.CSharpEditor.Controls.Settings;

/// <summary>Settings → Layout &amp; Typography: presets, the levers and the preview.</summary>
public partial class LayoutSettingsSectionControl : UserControl
{
    public LayoutSettingsSectionControl()
    {
        InitializeComponent();
        // Letting go of a slider gives the studio the layout at once (keys and clicks wait for a short pause).
        AddHandler(Thumb.DragCompletedEvent, (_, _) => Settings?.CommitLayoutNow(), RoutingStrategies.Bubble);
    }

    private CSharpSettingsViewModel? Settings => DataContext as CSharpSettingsViewModel;

    private void OnSaveAsBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Settings is not { } settings) return;
        settings.SaveLayoutAs();
        e.Handled = true;
    }

    private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (Settings is not { } settings || (sender as Control)?.DataContext is not LayoutPresetItemViewModel item) return;
        if (e.Key == Key.Enter) settings.RenameLayout(item);
        else if (e.Key == Key.Escape) settings.CancelRenameLayout(item);
        else return;
        e.Handled = true;
    }
}

/// <summary>Converters of the Layout page.</summary>
public static class LayoutConverters
{
    /// <summary>A number of pixels as a uniform corner radius.</summary>
    public static readonly IValueConverter Radius = new FuncValueConverter<double, CornerRadius>(r => new CornerRadius(Math.Max(0, r)));
}
