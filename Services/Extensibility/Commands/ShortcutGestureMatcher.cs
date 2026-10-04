using System;
using Avalonia.Input;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Commands;

/// <summary>
/// Evaluates whether an incoming KeyEventArgs matches a human-readable shortcut string (e.g. "Ctrl+Shift+R").
/// </summary>
public static class ShortcutGestureMatcher
{
    public static bool Matches(KeyEventArgs e, string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture)) return false;

        var parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        bool requireCtrl = false;
        bool requireShift = false;
        bool requireAlt = false;
        string keyPart = string.Empty;

        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (string.Equals(p, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, "Cmd", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, "Control", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, "Command", StringComparison.OrdinalIgnoreCase))
            {
                requireCtrl = true;
            }
            else if (string.Equals(p, "Shift", StringComparison.OrdinalIgnoreCase))
            {
                requireShift = true;
            }
            else if (string.Equals(p, "Alt", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(p, "Option", StringComparison.OrdinalIgnoreCase))
            {
                requireAlt = true;
            }
            else
            {
                keyPart = p;
            }
        }

        bool hasCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool hasAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (requireCtrl != hasCtrl || requireShift != hasShift || requireAlt != hasAlt)
        {
            return false;
        }

        if (string.IsNullOrEmpty(keyPart)) return false;

        if (Enum.TryParse<Key>(keyPart, ignoreCase: true, out var parsedKey))
        {
            return e.Key == parsedKey;
        }

        return false;
    }
}
