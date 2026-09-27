using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Visualizers;

/// <summary>
/// Registry of pluggable type visualizers used by debug sessions to render variables and their children.
/// </summary>
public sealed class DebugTypeVisualizerRegistry
{
    private static readonly Lazy<DebugTypeVisualizerRegistry> _default =
        new(() => CreateDefaultRegistry());

    public static DebugTypeVisualizerRegistry Default => _default.Value;

    private readonly List<IDebugTypeVisualizer> _visualizers = new();
    private readonly object _lock = new();

    public void Register(IDebugTypeVisualizer visualizer)
    {
        if (visualizer == null) throw new ArgumentNullException(nameof(visualizer));
        lock (_lock)
        {
            _visualizers.RemoveAll(v => v.GetType() == visualizer.GetType());
            _visualizers.Add(visualizer);
            _visualizers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public IDebugTypeVisualizer? FindVisualizer(object? value, Type type)
    {
        lock (_lock)
        {
            for (int i = 0; i < _visualizers.Count; i++)
            {
                if (_visualizers[i].CanVisualize(value, type))
                    return _visualizers[i];
            }
            return null;
        }
    }

    public static DebugTypeVisualizerRegistry CreateDefaultRegistry()
    {
        var reg = new DebugTypeVisualizerRegistry();
        reg.Register(new HttpTypeVisualizer());
        reg.Register(new CollectionTypeVisualizer());
        reg.Register(new DefaultReflectionVisualizer());
        return reg;
    }
}
