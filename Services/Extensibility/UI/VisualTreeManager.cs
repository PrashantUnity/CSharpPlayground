using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.UI;

/// <summary>
/// Implements <see cref="IVisualTreeApi"/> providing visual tree search,
/// hierarchical tree dumping, and adorner attachment on Avalonia windows.
/// </summary>
public class VisualTreeManager : IVisualTreeApi
{
    private readonly Func<object?> _topLevelResolver;

    public VisualTreeManager(Func<object?> topLevelResolver)
    {
        _topLevelResolver = topLevelResolver ?? throw new ArgumentNullException(nameof(topLevelResolver));
    }

    public object? FindControl(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_topLevelResolver() is not Visual root) return null;

        if (root is Control ctrl && string.Equals(ctrl.Name, name, StringComparison.OrdinalIgnoreCase))
        {
            return ctrl;
        }

        foreach (var desc in root.GetVisualDescendants())
        {
            if (desc is Control c && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }

        return null;
    }

    public IReadOnlyList<VisualElementInfo> DumpVisualTree(int maxDepth = 6)
    {
        var list = new List<VisualElementInfo>();
        if (_topLevelResolver() is not Visual root) return list;

        TraverseVisual(root, 0, maxDepth, list);
        return list;
    }

    public string DumpVisualTreeSummary(int maxDepth = 6)
    {
        var elements = DumpVisualTree(maxDepth);
        if (elements.Count == 0) return "Visual tree unavailable (no active window/toplevel).";

        var sb = new StringBuilder();
        sb.AppendLine($"# Studio Visual Tree Hierarchy ({elements.Count} elements)");

        foreach (var el in elements)
        {
            var indent = new string(' ', el.Depth * 2);
            var nameInfo = string.IsNullOrWhiteSpace(el.Name) ? "" : $" #{el.Name}";
            var visInfo = el.IsVisible ? "" : " (Hidden)";
            sb.AppendLine($"{indent}- {el.Type}{nameInfo} [{el.Bounds}]{visInfo}");
        }

        return sb.ToString();
    }

    public IDisposable AttachAdorner(object target, object adorner)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(adorner);

        if (target is not Control targetControl)
        {
            throw new ArgumentException("Target must be an Avalonia Control.", nameof(target));
        }

        if (adorner is not Control adornerControl)
        {
            throw new ArgumentException("Adorner must be an Avalonia Control.", nameof(adorner));
        }

        var layer = AdornerLayer.GetAdornerLayer(targetControl);
        if (layer == null)
        {
            throw new InvalidOperationException("No AdornerLayer found for the specified target visual.");
        }

        AdornerLayer.SetAdornedElement(adornerControl, targetControl);
        layer.Children.Add(adornerControl);

        return new ActionDisposable(() =>
        {
            layer.Children.Remove(adornerControl);
        });
    }

    private static void TraverseVisual(Visual visual, int depth, int maxDepth, List<VisualElementInfo> list)
    {
        if (depth > maxDepth) return;

        var ctrl = visual as Control;
        var bounds = visual.Bounds;

        list.Add(new VisualElementInfo
        {
            Name = ctrl?.Name ?? string.Empty,
            Type = visual.GetType().Name,
            Bounds = $"{(int)bounds.Width}x{(int)bounds.Height}",
            IsVisible = ctrl?.IsVisible ?? true,
            ChildCount = visual.GetVisualChildren().Count(),
            Depth = depth
        });

        foreach (var child in visual.GetVisualChildren())
        {
            TraverseVisual(child, depth + 1, maxDepth, list);
        }
    }

    private sealed class ActionDisposable : IDisposable
    {
        private Action? _action;
        public ActionDisposable(Action action) => _action = action;
        public void Dispose()
        {
            var act = System.Threading.Interlocked.Exchange(ref _action, null);
            act?.Invoke();
        }
    }
}
