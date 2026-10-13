using System;
using System.Reflection;

namespace PdfEditorApp.Plugins.CSharpEditor.Visualizers.Services;

public static class VisualizerReflectionHelper
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;

    public static object? GetMemberValue(object? obj, params string[] candidateNames)
    {
        if (obj == null) return null;
        var type = obj.GetType();

        foreach (var name in candidateNames)
        {
            // 1. Check Properties
            var prop = type.GetProperty(name, Flags);
            if (prop != null)
            {
                try { return prop.GetValue(obj); }
                catch { }
            }

            // 2. Check Fields (crucial for TreeNode.val, left, right, ListNode.next)
            var field = type.GetField(name, Flags);
            if (field != null)
            {
                try { return field.GetValue(obj); }
                catch { }
            }
        }

        return null;
    }

    public static bool HasMember(object obj, params string[] candidateNames)
    {
        var type = obj.GetType();
        foreach (var name in candidateNames)
        {
            if (type.GetProperty(name, Flags) != null || type.GetField(name, Flags) != null)
            {
                return true;
            }
        }
        return false;
    }

    public static string ExtractDisplayValue(object? obj, params string[] candidateNames)
    {
        if (obj == null) return string.Empty;

        var val = GetMemberValue(obj, candidateNames);
        if (val != null) return val.ToString() ?? string.Empty;

        // Fallback: check standard numeric / primitive values directly
        if (obj is int or long or short or byte or double or float or decimal or string or char or bool)
        {
            return obj.ToString() ?? string.Empty;
        }

        return obj.ToString() ?? string.Empty;
    }
}
