using System;
using Avalonia;
using Avalonia.Controls;
using Material.Icons;
using Material.Icons.Avalonia;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Controls;

public partial class InteractiveVisualizerControl : UserControl
{
    public static readonly StyledProperty<VisualizerOptions?> OptionsProperty =
        AvaloniaProperty.Register<InteractiveVisualizerControl, VisualizerOptions?>(nameof(Options));

    public VisualizerOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    static InteractiveVisualizerControl()
    {
        OptionsProperty.Changed.AddClassHandler<InteractiveVisualizerControl>((control, _) => control.ApplyOptions());
    }

    public InteractiveVisualizerControl()
    {
        try
        {
            InitializeComponent();
            WireEvents();
        }
        catch
        {
            // Headless test runner
        }
    }

    public InteractiveVisualizerControl(VisualizerOptions options) : this()
    {
        Options = options;
        ApplyOptions();
    }

    private void WireEvents()
    {
        var zoomInBtn = this.FindControl<Button>("ZoomInBtn");
        var zoomOutBtn = this.FindControl<Button>("ZoomOutBtn");
        var resetBtn = this.FindControl<Button>("ResetViewBtn");
        var toggleValuesBtn = this.FindControl<Button>("ToggleValuesBtn");
        var toggleCoordsBtn = this.FindControl<Button>("ToggleCoordsBtn");
        var copyBtn = this.FindControl<Button>("CopyDataBtn");

        if (zoomInBtn != null) zoomInBtn.Click += (_, _) => Zoom(1.2);
        if (zoomOutBtn != null) zoomOutBtn.Click += (_, _) => Zoom(0.83);
        if (resetBtn != null) resetBtn.Click += (_, _) => ResetView();
        if (toggleValuesBtn != null) toggleValuesBtn.Click += (_, _) => ToggleValues();
        if (toggleCoordsBtn != null) toggleCoordsBtn.Click += (_, _) => ToggleCoords();
        if (copyBtn != null) copyBtn.Click += (_, _) => CopyData();
    }

    private void ToggleValues()
    {
        if (Options == null) return;
        Options.ShowValues = !Options.ShowValues;
        if (Options.MatrixData != null) Options.MatrixData.ShowValues = Options.ShowValues;
        this.FindControl<VisualizerCanvasControl>("CanvasControl")?.InvalidateVisual();
    }

    private void ToggleCoords()
    {
        if (Options == null) return;
        Options.ShowCoordinates = !Options.ShowCoordinates;
        if (Options.MatrixData != null) Options.MatrixData.ShowCoordinates = Options.ShowCoordinates;
        this.FindControl<VisualizerCanvasControl>("CanvasControl")?.InvalidateVisual();
    }

    private void CopyData()
    {
        if (Options?.MatrixData != null)
            VisualizerExportService.CopyToClipboard(VisualizerExportService.ToCsv(Options.MatrixData));
    }

    private void Zoom(double factor)
    {
        if (Options == null) return;
        Options.Zoom = Math.Clamp(Options.Zoom * factor, 0.25, 4.0);
        var canvas = this.FindControl<VisualizerCanvasControl>("CanvasControl");
        canvas?.InvalidateVisual();
    }

    private void ResetView()
    {
        if (Options == null) return;
        Options.Zoom = 1.0;
        Options.PanOffsetX = 0;
        Options.PanOffsetY = 0;
        var canvas = this.FindControl<VisualizerCanvasControl>("CanvasControl");
        canvas?.InvalidateVisual();
    }

    public void ApplyOptions()
    {
        if (Options == null) return;

        var titleBlock = this.FindControl<TextBlock>("VisualizerTitleText");
        var statsBlock = this.FindControl<TextBlock>("StatsSummaryText");
        var icon = this.FindControl<MaterialIcon>("HeaderVisualizerIcon");
        var canvas = this.FindControl<VisualizerCanvasControl>("CanvasControl");
        var playback = this.FindControl<VisualizerPlaybackControl>("PlaybackControl");

        if (titleBlock != null)
        {
            titleBlock.Text = string.IsNullOrEmpty(Options.Title) ? "Data Structure Visualizer" : Options.Title;
        }

        if (icon != null)
        {
            icon.Kind = Options.Kind switch
            {
                VisualizerKind.Matrix => MaterialIconKind.Grid,
                VisualizerKind.Islands => MaterialIconKind.Island,
                VisualizerKind.Tree => MaterialIconKind.FamilyTree,
                VisualizerKind.Graph => MaterialIconKind.Graph,
                VisualizerKind.LinkedList => MaterialIconKind.VectorLink,
                VisualizerKind.ArrayPointers => MaterialIconKind.FormatListNumbered,
                _ => MaterialIconKind.CodeBraces
            };
        }

        if (statsBlock != null)
        {
            statsBlock.Text = Options.GetSummaryText();
        }

        if (canvas != null)
        {
            canvas.Options = Options;
        }

        if (playback != null)
        {
            playback.Sequence = Options.Sequence;
            playback.IsVisible = Options.Sequence != null && Options.Sequence.HasSteps;
        }
    }
}
