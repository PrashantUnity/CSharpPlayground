using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Debugging.Visualizers;

/// <summary>
/// General fallback visualizer that inspects public instance properties and fields via reflection.
/// Safely suppresses exceptions from getters and avoids blocking on incomplete tasks.
/// </summary>
public sealed class DefaultReflectionVisualizer : IDebugTypeVisualizer
{
    private const int MaxInspectMembers = 30;

    public int Priority => 0; // Fallback

    public bool CanVisualize(object? value, Type type) => true;

    public string FormatSummary(object value, Type type)
    {
        return value.ToString() ?? type.Name;
    }

    public IEnumerable<(string Name, object? Value)> GetChildren(object value, Type type)
    {
        int count = 0;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (count >= MaxInspectMembers) yield break;
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;

            // Reading Result of an unfinished task would block the paused script thread.
            if (value is Task { IsCompleted: false } && prop.Name == nameof(Task<object>.Result)) continue;

            object? val;
            try
            {
                val = prop.GetValue(value);
            }
            catch (Exception ex)
            {
                val = $"<threw {ex.GetType().Name}>";
            }

            yield return (prop.Name, val);
            count++;
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (count >= MaxInspectMembers) yield break;

            object? val;
            try
            {
                val = field.GetValue(value);
            }
            catch (Exception ex)
            {
                val = $"<threw {ex.GetType().Name}>";
            }

            yield return (field.Name, val);
            count++;
        }
    }
}
