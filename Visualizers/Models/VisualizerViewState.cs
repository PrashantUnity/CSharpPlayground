using System;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

/// <summary>
/// Per-view interaction state for an algorithm/data-structure visualizer. Decouples inline and fullscreen
/// views so they do not mutate the underlying <see cref="VisualizerOptions"/> or fight over zoom, pan, or visibility flags.
/// </summary>
public sealed class VisualizerViewState
{
    public double Zoom { get; set; } = 1.0;
    public double PanOffsetX { get; set; } = 0.0;
    public double PanOffsetY { get; set; } = 0.0;
    public bool? OverrideShowValues { get; set; }
    public bool? OverrideShowCoordinates { get; set; }
    public bool StayFitted { get; set; }

    public VisualizerViewState() { }

    public VisualizerViewState(VisualizerViewState source)
    {
        Zoom = source.Zoom;
        PanOffsetX = source.PanOffsetX;
        PanOffsetY = source.PanOffsetY;
        OverrideShowValues = source.OverrideShowValues;
        OverrideShowCoordinates = source.OverrideShowCoordinates;
        StayFitted = source.StayFitted;
    }

    public VisualizerViewState(VisualizerOptions options)
    {
        Zoom = options.Zoom;
        PanOffsetX = options.PanOffsetX;
        PanOffsetY = options.PanOffsetY;
        OverrideShowValues = options.ShowValues;
        OverrideShowCoordinates = options.ShowCoordinates;
    }

    public bool EffectiveShowValues(VisualizerOptions options) => OverrideShowValues ?? options.ShowValues;

    public bool EffectiveShowCoordinates(VisualizerOptions options) => OverrideShowCoordinates ?? options.ShowCoordinates;

    public void Reset()
    {
        Zoom = 1.0;
        PanOffsetX = 0.0;
        PanOffsetY = 0.0;
        StayFitted = false;
        OverrideShowValues = null;
        OverrideShowCoordinates = null;
    }
}
