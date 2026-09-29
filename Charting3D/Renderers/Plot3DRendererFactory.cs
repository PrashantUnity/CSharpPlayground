using System;
using System.Collections.Concurrent;
using PdfEditorApp.Plugins.CSharpEditor.Charting3D.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Charting3D.Renderers;

public static class Plot3DRendererFactory
{
    private static readonly ConcurrentDictionary<Plot3DType, IPlot3DRenderer> Renderers = new();

    static Plot3DRendererFactory()
    {
        Renderers[Plot3DType.Scatter] = new Scatter3DRenderer();
        Renderers[Plot3DType.Surface] = new Surface3DRenderer();
        Renderers[Plot3DType.Wireframe] = new Surface3DRenderer();
        Renderers[Plot3DType.Trajectory] = new Trajectory3DRenderer();
        Renderers[Plot3DType.Graph3D] = new Graph3DRenderer();
        Renderers[Plot3DType.VoxelBar] = new VoxelBar3DRenderer();
    }

    public static IPlot3DRenderer GetRenderer(Plot3DType type)
    {
        if (Renderers.TryGetValue(type, out var r)) return r;
        return Renderers[Plot3DType.Scatter];
    }
}
