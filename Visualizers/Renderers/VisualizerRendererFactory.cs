using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Visualizers.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Renderers;

public static class VisualizerRendererFactory
{
    private static readonly Dictionary<VisualizerKind, IVisualizerRenderer> Renderers = new()
    {
        [VisualizerKind.Matrix] = new GridMatrixRenderer(),
        [VisualizerKind.Islands] = new GridMatrixRenderer(),
        [VisualizerKind.Tree] = new TreeRenderer(),
        [VisualizerKind.Graph] = new GraphRenderer(),
        [VisualizerKind.LinkedList] = new LinkedListRenderer(),
        [VisualizerKind.ArrayPointers] = new ArrayPointerRenderer(),
        [VisualizerKind.Bars] = new BarVisualizerRenderer(),
        [VisualizerKind.Board] = new BoardVisualizerRenderer(),
        [VisualizerKind.Canvas] = new CanvasSceneRenderer()
    };

    public static IVisualizerRenderer GetRenderer(VisualizerKind kind)
    {
        if (Renderers.TryGetValue(kind, out var renderer))
        {
            return renderer;
        }
        return Renderers[VisualizerKind.Matrix];
    }
}
