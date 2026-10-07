using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

/// <summary>
/// Dynamic runtime theme engine: recolours, refonts and resizes the studio at run time, without a restart.
/// <para>
/// Every value it sets lives in one <b>theme layer</b>, a resource dictionary placed on top of the palette in each
/// studio root (the studio host view, and windows of their own; the application when there is none). A change replaces
/// that whole dictionary at once: <b>one</b> resource-change notification for a whole theme. Writing the ~240 entries of
/// a theme one by one sent ~700 notifications, each re-resolving every dynamic resource of every page the studio keeps
/// alive, which froze the app for ~8 s per theme. And because the palette was also included closer to the controls than
/// the application, colours written to the application were shadowed and never showed.
/// </para>
/// </summary>
public class DynamicThemeEngine : IThemeApi
{
    private readonly ConcurrentDictionary<string, ThemeDefinition> _themes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private string _activeThemeId = "dark-plus";

    // The theme layer: every resource the engine has set, by key. Read and written under _layerGate.
    private readonly Dictionary<string, object> _layer = new(StringComparer.Ordinal);
    private readonly object _layerGate = new();
    private ThemeVariant? _variant;
    private LayoutSpec _layout = LayoutSpec.Default;

    // The colour keys the active theme and its overrides put in the layer: the next theme takes back the ones it doesn't
    // set, or they would keep the old theme's colour.
    private readonly HashSet<string> _colorKeys = new(StringComparer.Ordinal);

    // The value colours (object inspector, data tips, watch rows) follow a theme's syntax colours when it has them and
    // doesn't set its own; otherwise the palette's per-scheme defaults show. Null values are muted text.
    private static readonly (string Code, string Source)[] CodeColorSources =
    [
        ("CodeTypeBrush", "SyntaxTypeBrush"), ("CodeMemberBrush", "SyntaxVariableBrush"), ("CodeStringBrush", "SyntaxStringBrush"),
        ("CodeNumberBrush", "SyntaxNumberBrush"), ("CodeKeywordBrush", "SyntaxKeywordBrush"), ("CodeNullBrush", "DsMutedBrush"),
    ];

    // Where the layer is shown (UI thread only): each root's resources, and the layer dictionary installed in it.
    private readonly List<IResourceDictionary> _roots = new();
    private readonly Dictionary<IResourceDictionary, ThemeLayerProvider> _installed = new();
    private int _commitQueued;

    /// <summary>How many times the layer was swapped into the screen (tests check a whole theme costs one).</summary>
    public int LayerCommits { get; private set; }

    /// <summary>How long the last swap took on the UI thread: every dynamic resource below the roots re-resolving (perf tools read it).</summary>
    public TimeSpan LastCommitDuration { get; private set; }

    public event Action<string>? ThemeChanged;

    /// <summary>The layout changed (fonts, sizes, radii, borders, spacing, shadows), with the new layout.</summary>
    public event Action<LayoutSpec>? LayoutChanged;

    /// <summary>The layout in use; it is independent of the colour theme and stays when the theme changes.</summary>
    public LayoutSpec Layout => _layout;

    public string ActiveThemeId => _activeThemeId;
    public IReadOnlyDictionary<string, string> Overrides => _overrides;

    public DynamicThemeEngine()
    {
        foreach (var theme in BuiltInThemes.All)
        {
            _themes[theme.Id] = theme;
        }
    }

    public void SetColor(string tokenName, string hexOrRgb)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);
        ArgumentException.ThrowIfNullOrWhiteSpace(hexOrRgb);

        if (!TryParseColor(hexOrRgb, out var color))
        {
            throw new ArgumentException($"Invalid color specification: '{hexOrRgb}'. Expected hex (e.g. #FF5722 or #1A00FFCC) or rgb/rgba.", nameof(hexOrRgb));
        }

        _overrides[tokenName] = hexOrRgb;

        // The Brush and Color variants come too, and a syntax colour carries its value colour along.
        string brushKey = tokenName.EndsWith("Brush", StringComparison.OrdinalIgnoreCase) ? tokenName : tokenName + "Brush";
        lock (_layerGate)
        {
            WriteColor(tokenName, color);
            foreach (var (code, source) in CodeColorSources)
            {
                if (string.Equals(source, brushKey, StringComparison.Ordinal) && !_overrides.ContainsKey(code) && !ActiveThemeSets(code)) WriteColor(code, color);
            }
        }

        QueueCommit();
        ThemeChanged?.Invoke(_activeThemeId);
    }

    // Under _layerGate: a colour under its key, its Brush key and its Color key.
    private void WriteColor(string key, Color color)
    {
        string brushKey = key.EndsWith("Brush", StringComparison.OrdinalIgnoreCase) ? key : key + "Brush";
        string colorKey = key.EndsWith("Brush", StringComparison.OrdinalIgnoreCase)
            ? key.Substring(0, key.Length - 5) + "Color"
            : key;

        var brush = new ImmutableSolidColorBrush(color);
        _layer[brushKey] = brush;
        _layer[colorKey] = color;
        _layer[key] = brush;
        _colorKeys.Add(brushKey);
        _colorKeys.Add(colorKey);
        _colorKeys.Add(key);
    }

    private bool ActiveThemeSets(string key) => _themes.TryGetValue(_activeThemeId, out var active) && active.Colors.ContainsKey(key);

    public string? GetColor(string tokenName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);

        if (_overrides.TryGetValue(tokenName, out var overrideHex))
        {
            return overrideHex;
        }

        object? layered;
        lock (_layerGate) _layer.TryGetValue(tokenName, out layered);
        if (layered is ISolidColorBrush layeredBrush)
        {
            return $"#{layeredBrush.Color.A:X2}{layeredBrush.Color.R:X2}{layeredBrush.Color.G:X2}{layeredBrush.Color.B:X2}";
        }
        if (layered is Color layeredColor)
        {
            return $"#{layeredColor.A:X2}{layeredColor.R:X2}{layeredColor.G:X2}{layeredColor.B:X2}";
        }

        if (Application.Current?.Resources is { } res && res.TryGetResource(tokenName, null, out var val))
        {
            if (val is ISolidColorBrush scb)
            {
                return $"#{scb.Color.A:X2}{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}";
            }
            if (val is Color c)
            {
                return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            }
        }

        if (_themes.TryGetValue(_activeThemeId, out var activeDef) &&
            activeDef.Colors.TryGetValue(tokenName, out var themeColor))
        {
            return themeColor;
        }

        return null;
    }

    public void SetFont(string role, string fontFamily, double? size = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamily);

        lock (_layerGate) SetFontValues(role, fontFamily, size);
        QueueCommit();
    }

    private void SetFontValues(string role, string fontFamily, double? size)
    {
        var family = new FontFamily(fontFamily);
        switch (role.ToLowerInvariant())
        {
            case "editor":
            case "mono":
            case "code":
                _layer["M3FontFamilyMono"] = family;
                _layer["DsCodeFontFamily"] = family;
                _layer["JetBrainsMonoFontFamily"] = family;
                _layer["EditorFontFamily"] = family;
                if (size.HasValue) _layer["EditorFontSize"] = size.Value;
                break;
            case "ui":
            case "system":
            default:
                _layer["M3FontFamily"] = family;
                _layer["M3FontFamilyDisplay"] = family;
                _layer["DsUiFontFamily"] = family;
                if (size.HasValue) _layer["M3FontSizeBodyMedium"] = size.Value;
                break;
        }
    }

    /// <summary>Compact, Comfortable or Spacious: the layout's density (spacing of containers, controls, rows and tabs).</summary>
    public void SetDensity(LayoutDensity density) => ApplyLayout(_layout with { Density = density });

    /// <summary>
    /// Lays the studio out by <paramref name="layout"/>: every layout token (fonts, type ramp, weights, radii, borders,
    /// spacing, shadows) in one swap of the theme layer, so the whole change costs one resource notification.
    /// </summary>
    public void ApplyLayout(LayoutSpec layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
        lock (_layerGate) WriteLayoutValues(layout, GetTheme(_activeThemeId));
        QueueCommit();
        LayoutChanged?.Invoke(layout);
    }

    // Called under _layerGate.
    private void WriteLayoutValues(LayoutSpec layout, ThemeDefinition? theme)
    {
        var isDark = _variant is { } variant ? variant != ThemeVariant.Light : theme?.IsDark ?? true;
        foreach (var (key, value) in LayoutTokenMapper.Map(layout, isDark)) _layer[key] = value;

        // A theme's own fonts apply where the layout keeps the defaults.
        if (theme != null)
        {
            foreach (var (fontRole, fontName) in theme.Fonts)
            {
                var isCode = fontRole.Equals("code", StringComparison.OrdinalIgnoreCase) || fontRole.Equals("mono", StringComparison.OrdinalIgnoreCase) || fontRole.Equals("editor", StringComparison.OrdinalIgnoreCase);
                if (isCode ? layout.CodeFont == null : layout.UiFont == null) SetFontValues(fontRole, fontName, null);
            }
        }
    }

    public void SetSpacing(string tokenName, double value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);
        _overrides[tokenName] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        lock (_layerGate) _layer[tokenName] = value;
        QueueCommit();
        ThemeChanged?.Invoke(_activeThemeId);
    }

    public void SetDimension(string tokenName, double value) => SetSpacing(tokenName, value);

    public void SetResource(string tokenName, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);
        ArgumentNullException.ThrowIfNull(value);
        _overrides[tokenName] = value.ToString() ?? string.Empty;
        lock (_layerGate) _layer[tokenName] = value;
        QueueCommit();
        ThemeChanged?.Invoke(_activeThemeId);
    }

    public object? GetResource(string tokenName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);
        lock (_layerGate)
        {
            if (_layer.TryGetValue(tokenName, out var layered)) return layered;
        }

        if (Application.Current?.Resources is { } res && res.TryGetResource(tokenName, null, out var val))
        {
            return val;
        }

        return null;
    }

    /// <summary>Whether a theme with this id is registered (built in, saved, generated or imported).</summary>
    public bool HasTheme(string themeId) => !string.IsNullOrWhiteSpace(themeId) && _themes.ContainsKey(themeId);

    /// <summary>The registered theme with this id, or <c>null</c>.</summary>
    public ThemeDefinition? GetTheme(string themeId) =>
        !string.IsNullOrWhiteSpace(themeId) && _themes.TryGetValue(themeId, out var theme) ? theme : null;

    /// <summary>Whether this id is one of the themes that ship with the studio.</summary>
    public static bool IsBuiltInTheme(string? themeId) =>
        themeId != null && BuiltInThemes.All.Any(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase));

    public void RegisterTheme(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (string.IsNullOrWhiteSpace(theme.Id)) throw new ArgumentException("Theme ID cannot be empty.", nameof(theme));
        _themes[theme.Id] = theme;
    }

    public bool ApplyTheme(string themeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeId);

        if (!_themes.TryGetValue(themeId, out var def))
        {
            return false;
        }

        _activeThemeId = themeId;
        _overrides.Clear();

        // The whole theme is worked out first (off screen), then shown in one swap of the theme layer.
        lock (_layerGate)
        {
            _variant = def.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
            foreach (var key in _colorKeys) _layer.Remove(key);
            _colorKeys.Clear();
            foreach (var (key, hex) in def.Colors)
            {
                if (TryParseColor(hex, out var color)) WriteColor(key, color);
            }

            foreach (var (code, source) in CodeColorSources)
            {
                if (!def.Colors.ContainsKey(code) && def.Colors.TryGetValue(source, out var hex) && TryParseColor(hex, out var color)) WriteColor(code, color);
            }

            // The layout stays: its shadows are worked out again for the theme's scheme, and its fonts win over the
            // theme's (when it has chosen any).
            WriteLayoutValues(_layout, def);
        }

        QueueCommit();
        ThemeChanged?.Invoke(_activeThemeId);
        return true;
    }

    /// <summary>
    /// Shows the theme layer in <paramref name="element"/>'s resources while it is in a window: the studio host view,
    /// and windows of their own that include the palette. The layer sits on top of the palette there, so it wins.
    /// </summary>
    public void TrackResourceRoot(StyledElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element is Window window)
        {
            // A window is the root of its own tree: it is never "detached", it closes.
            AttachResourceRoot(window.Resources);
            window.Closed += (_, _) => DetachResourceRoot(window.Resources);
            return;
        }

        element.AttachedToLogicalTree += (_, _) => AttachResourceRoot(element.Resources);
        element.DetachedFromLogicalTree += (_, _) => DetachResourceRoot(element.Resources);
        if (((Avalonia.LogicalTree.ILogical)element).IsAttachedToLogicalTree) AttachResourceRoot(element.Resources);
    }

    /// <summary>Starts showing the theme layer in <paramref name="root"/> (UI thread).</summary>
    public void AttachResourceRoot(IResourceDictionary root)
    {
        if (_roots.Contains(root)) return;
        _roots.Add(root);

        // A theme applied before any studio existed went to the application; a studio shows it from now on.
        if (Application.Current?.Resources is { } app && !_roots.Contains(app) && _installed.Remove(app, out var appLayer))
        {
            app.MergedDictionaries.Remove(appLayer);
        }

        CommitNow();
    }

    /// <summary>Stops showing the theme layer in <paramref name="root"/> (UI thread).</summary>
    public void DetachResourceRoot(IResourceDictionary root)
    {
        _roots.Remove(root);
        if (_installed.Remove(root, out var layer)) root.MergedDictionaries.Remove(layer);
    }

    // Many changes in a row (a script setting fifty colours) are shown together, once, on the next UI turn.
    private void QueueCommit()
    {
        if (!UiDispatchHelper.HasLiveUiLifetime)
        {
            UiDispatchHelper.RunOnUi(CommitNow);
            return;
        }

        if (System.Threading.Interlocked.Exchange(ref _commitQueued, 1) == 1) return;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            System.Threading.Interlocked.Exchange(ref _commitQueued, 0);
            CommitNow();
        }, Avalonia.Threading.DispatcherPriority.Send);
    }

    private void CommitNow()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            CommitLayer();
        }
        finally
        {
            LastCommitDuration = System.Diagnostics.Stopwatch.GetElapsedTime(started);
        }
    }

    private void CommitLayer()
    {
        Dictionary<object, object?> values;
        ThemeVariant? variant;
        lock (_layerGate)
        {
            values = new Dictionary<object, object?>(_layer.Count);
            foreach (var (key, value) in _layer) values[key] = value;
            variant = _variant;
        }

        // The light/dark switch re-resolves every themed resource once; only when it really changes.
        if (variant != null && Application.Current is { } app && app.RequestedThemeVariant != variant)
        {
            app.RequestedThemeVariant = variant;
        }

        var targets = _roots.Count > 0 ? (IEnumerable<IResourceDictionary>)_roots : Application.Current?.Resources is { } appResources ? new[] { appResources } : Array.Empty<IResourceDictionary>();
        foreach (var root in targets)
        {
            // The layer stays installed; its content is replaced in one go: one change for everything below the root.
            if (_installed.TryGetValue(root, out var layer) && root.MergedDictionaries.Contains(layer))
            {
                layer.Replace(values);
            }
            else
            {
                layer = new ThemeLayerProvider();
                layer.Replace(values);
                root.MergedDictionaries.Add(layer);
                _installed[root] = layer;
            }
        }

        LayerCommits++;
    }

    public IReadOnlyList<ThemeDefinition> GetAvailableThemes()
    {
        return _themes.Values.OrderBy(t => t.Name).ToList();
    }

    public IDisposable AddStyle(IStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);

        UiDispatchHelper.RunOnUi(() =>
        {
            Application.Current?.Styles.Add(style);
        });

        return new ActionDisposable(() =>
        {
            UiDispatchHelper.RunOnUi(() =>
            {
                Application.Current?.Styles.Remove(style);
            });
        });
    }

    public IDisposable AddStyle(string xaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xaml);

        IStyle? loadedStyle = null;

        UiDispatchHelper.RunOnUi(() =>
        {
            try
            {
                string trimmed = xaml.Trim();
                if (trimmed.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                {
                    if (Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(new Uri(trimmed), null) is IStyle style)
                    {
                        loadedStyle = style;
                        Application.Current?.Styles.Add(style);
                    }
                }
                else
                {
                    // Attempt runtime loader reflection if present in Avalonia environment
                    var loaderType = Type.GetType("Avalonia.Markup.Xaml.Loader.AvaloniaRuntimeXamlLoader, Avalonia.Markup.Xaml.Loader")
                        ?? Type.GetType("Avalonia.Markup.Xaml.AvaloniaRuntimeXamlLoader, Avalonia.Markup.Xaml");
                    var loadMethod = loaderType?.GetMethod("Load", new[] { typeof(string) });
                    if (loadMethod?.Invoke(null, new object[] { trimmed }) is IStyle dynStyle)
                    {
                        loadedStyle = dynStyle;
                        Application.Current?.Styles.Add(dynStyle);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DynamicThemeEngine.AddStyle] Failed to load XAML style: {ex.Message}");
            }
        });

        return new ActionDisposable(() =>
        {
            if (loadedStyle != null)
            {
                UiDispatchHelper.RunOnUi(() =>
                {
                    Application.Current?.Styles.Remove(loadedStyle);
                });
            }
        });
    }

    public bool ApplyLayout(string layoutJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layoutJson);
        try
        {
            var element = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(layoutJson);
            var json = element.TryGetProperty("$extensions", out var ext) && ext.TryGetProperty("com.frypdf.layout", out var embedded) ? embedded : element;
            if (LayoutSpec.FromJson(json) is not { } layout) return false;
            ApplyLayout(layout);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public string GetLayoutJson() => _layout.ToJson().GetRawText();

    public object? GetLayoutToken(string tokenName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);
        return LayoutTokens.TryGet(tokenName, out var value) ? value : null;
    }

    public void ResetToDefaults()
    {
        ApplyTheme(_activeThemeId);
    }

    private static bool TryParseColor(string hexOrRgb, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hexOrRgb)) return false;

        string s = hexOrRgb.Trim();

        try
        {
            if (s.StartsWith("#"))
            {
                color = Color.Parse(s);
                return true;
            }

            if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var parts = s.Replace("rgba(", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("rgb(", "", StringComparison.OrdinalIgnoreCase)
                             .Replace(")", "")
                             .Split(',', StringSplitOptions.TrimEntries);

                if (parts.Length >= 3 &&
                    byte.TryParse(parts[0], out var r) &&
                    byte.TryParse(parts[1], out var g) &&
                    byte.TryParse(parts[2], out var b))
                {
                    byte a = 255;
                    if (parts.Length >= 4 && float.TryParse(parts[3], out var aVal))
                    {
                        a = (byte)Math.Clamp((int)(aVal <= 1.0f ? aVal * 255 : aVal), 0, 255);
                    }
                    color = Color.FromArgb(a, r, g, b);
                    return true;
                }
            }

            color = Color.Parse(s);
            return true;
        }
        catch
        {
            return false;
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
