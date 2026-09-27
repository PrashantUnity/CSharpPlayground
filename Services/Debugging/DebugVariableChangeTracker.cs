using System;
using System.Collections.Generic;
using PdfEditorApp.Plugins.CSharpEditor.Models;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging;

public sealed class DebugVariableChangeTracker
{
    private readonly Dictionary<string, string> _previousValues = new(StringComparer.Ordinal);

    public void TrackAndMarkChanges(IEnumerable<DebugVariableItem> items)
    {
        if (items == null) return;

        foreach (var item in items)
        {
            TrackItem(item);
        }
    }

    private void TrackItem(DebugVariableItem item)
    {
        string key = !string.IsNullOrWhiteSpace(item.PathExpression) ? item.PathExpression : item.Name;
        if (string.IsNullOrWhiteSpace(key)) return;

        if (_previousValues.TryGetValue(key, out var prevVal))
        {
            if (!string.Equals(prevVal, item.ValueDisplay, StringComparison.Ordinal))
            {
                item.HasValueChanged = true;
                item.PreviousValue = prevVal;
            }
            else
            {
                item.HasValueChanged = false;
                item.PreviousValue = null;
            }
        }
        else
        {
            item.HasValueChanged = false;
            item.PreviousValue = null;
        }

        _previousValues[key] = item.ValueDisplay;

        foreach (var child in item.Children)
        {
            TrackItem(child);
        }
    }

    public void Reset()
    {
        _previousValues.Clear();
    }
}
