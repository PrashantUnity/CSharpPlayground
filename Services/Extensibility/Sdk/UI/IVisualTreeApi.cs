using System;
using System.Collections.Generic;

namespace FrySharp.Sdk;

/// <summary>
/// Lightweight information about a node in the Avalonia visual tree.
/// </summary>
public class VisualElementInfo
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Bounds { get; init; } = string.Empty;
    public bool IsVisible { get; init; } = true;
    public int ChildCount { get; init; }
    public int Depth { get; init; }
}

/// <summary>
/// Public API for visual tree inspection, querying controls by name or type,
/// and attaching dynamic adorners and overlays to existing UI elements.
/// </summary>
public interface IVisualTreeApi
{
    /// <summary>Searches the application window visual tree for a control with the given Name or automation ID.</summary>
    object? FindControl(string name);

    /// <summary>Extracts a flattened hierarchy of visible controls in the application window up to maxDepth.</summary>
    IReadOnlyList<VisualElementInfo> DumpVisualTree(int maxDepth = 6);

    /// <summary>Generates a formatted text summary of the visible UI hierarchy for AI inspection.</summary>
    string DumpVisualTreeSummary(int maxDepth = 6);

    /// <summary>Attaches a floating adorner or highlight border to an existing visual element.</summary>
    IDisposable AttachAdorner(object target, object adorner);
}
