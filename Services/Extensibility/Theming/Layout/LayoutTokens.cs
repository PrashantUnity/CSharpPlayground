using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;

/// <summary>
/// The current layout tokens for controls built in code (rendered Markdown and HTML, completion lists, tables), which
/// can't use <c>{DynamicResource}</c>. Values are worked out once per layout; such controls rebuild on
/// <see cref="DynamicThemeEngine.LayoutChanged"/>.
/// </summary>
public static class LayoutTokens
{
    private static Snapshot? _snapshot;

    private sealed record Snapshot(LayoutSpec Spec, bool IsDark, IReadOnlyDictionary<string, object> Tokens);

    public static T Get<T>(string key, T fallback)
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        var spec = engine.Layout;
        var isDark = engine.GetTheme(engine.ActiveThemeId)?.IsDark ?? true;
        var snapshot = _snapshot;
        if (snapshot == null || !ReferenceEquals(snapshot.Spec, spec) || snapshot.IsDark != isDark)
        {
            snapshot = new Snapshot(spec, isDark, LayoutTokenMapper.Map(spec, isDark));
            _snapshot = snapshot;
        }

        return snapshot.Tokens.TryGetValue(key, out var value) && value is T typed ? typed : fallback;
    }

    /// <summary>The value of a layout token for the current layout; false for a key the mapper doesn't make.</summary>
    public static bool TryGet(string key, out object value)
    {
        Get<object?>(key, null);
        var tokens = _snapshot!.Tokens;
        if (tokens.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>A step of the type ramp (<c>"300"</c> is body text).</summary>
    public static double FontSize(string step) => Get("DsFontSize" + step, 12.0);

    /// <summary>A weight role: Body, Label, Emphasis or Strong.</summary>
    public static FontWeight Weight(string role) => Get("DsWeight" + role, FontWeight.Normal);

    public static FontFamily CodeFont => Get("DsCodeFontFamily", new FontFamily(LayoutTokenMapper.DefaultCodeFont));

    /// <summary>The width of borders and dividers.</summary>
    public static double BorderWidth => Get("DsBorderThin", new Thickness(1)).Left;

    /// <summary>The width of accent bars (quotes, selected items).</summary>
    public static double AccentWidth => Get("DsBorderAccentLeft", new Thickness(2, 0, 0, 0)).Left;

    /// <summary>A radius step (XS, SM, MD, LG, XL, 2XL, 3XL, 4XL) or a component (Card, Button, …).</summary>
    public static double Radius(string step) => Get("DsRadius" + step, new CornerRadius(6)).TopLeft;
}
