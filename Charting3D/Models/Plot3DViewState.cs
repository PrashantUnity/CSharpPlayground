using System;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

/// <summary>
/// Per-view interaction state for a 3D visualization. Decouples inline and fullscreen views so they
/// do not mutate the underlying <see cref="Plot3DOptions"/> or fight over camera angles, wireframe, or color maps.
/// </summary>
public sealed class Plot3DViewState
{
    public Camera3D Camera { get; } = new();
    public bool? OverrideShowFloorGrid { get; set; }
    public bool? OverrideShowBoundingBox { get; set; }
    public bool? OverrideWireframe { get; set; }
    public ColorMapPreset? OverrideColorMap { get; set; }
    public bool AutoRotate { get; set; }

    public Plot3DViewState() { }

    public Plot3DViewState(Plot3DViewState source)
    {
        Camera.CopyFrom(source.Camera);
        OverrideShowFloorGrid = source.OverrideShowFloorGrid;
        OverrideShowBoundingBox = source.OverrideShowBoundingBox;
        OverrideWireframe = source.OverrideWireframe;
        OverrideColorMap = source.OverrideColorMap;
        AutoRotate = source.AutoRotate;
    }

    public Plot3DViewState(Plot3DOptions options)
    {
        Camera.CopyFrom(options.Camera);
        OverrideShowFloorGrid = options.ShowFloorGrid;
        OverrideShowBoundingBox = options.ShowBoundingBox;
        OverrideWireframe = options.Wireframe;
        OverrideColorMap = options.ColorMap;
        AutoRotate = options.AutoRotate;
    }

    public bool EffectiveShowFloorGrid(Plot3DOptions options) => OverrideShowFloorGrid ?? options.ShowFloorGrid;

    public bool EffectiveShowBoundingBox(Plot3DOptions options) => OverrideShowBoundingBox ?? options.ShowBoundingBox;

    public bool EffectiveWireframe(Plot3DOptions options) => OverrideWireframe ?? options.Wireframe;

    public ColorMapPreset EffectiveColorMap(Plot3DOptions options) => OverrideColorMap ?? options.ColorMap;

    public void Reset(Plot3DOptions? options = null)
    {
        Camera.Reset();
        if (options != null)
        {
            OverrideShowFloorGrid = options.ShowFloorGrid;
            OverrideShowBoundingBox = options.ShowBoundingBox;
            OverrideWireframe = options.Wireframe;
            OverrideColorMap = options.ColorMap;
            AutoRotate = options.AutoRotate;
        }
        else
        {
            OverrideShowFloorGrid = null;
            OverrideShowBoundingBox = null;
            OverrideWireframe = null;
            OverrideColorMap = null;
            AutoRotate = false;
        }
    }
}
