using System;
using System.Collections.Generic;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Visualizers;

/// <summary>
/// Pluggable debugger type visualizer / renderer for inspecting language runtime objects.
/// Translates raw runtime structures into human-friendly summaries and child nodes.
/// </summary>
public interface IDebugTypeVisualizer
{
    /// <summary>Higher priority visualizers run before lower priority ones.</summary>
    int Priority { get; }

    /// <summary>Determines if this visualizer handles the given runtime value and type.</summary>
    bool CanVisualize(object? value, Type type);

    /// <summary>Formats a human-friendly single-line summary for the variable.</summary>
    string FormatSummary(object value, Type type);

    /// <summary>Yields child member nodes (properties, elements, pseudo-fields) for inspection.</summary>
    IEnumerable<(string Name, object? Value)> GetChildren(object value, Type type);
}
