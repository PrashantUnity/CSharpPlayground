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

    // Where the layer is shown (UI thread only): each root's resources, and the layer dictionary installed in it.
    private readonly List<IResourceDictionary> _roots = new();
    private readonly Dictionary<IResourceDictionary, ThemeLayerProvider> _installed = new();
    private int _commitQueued;

    /// <summary>How many times the layer was swapped into the screen (tests check a whole theme costs one).</summary>
    public int LayerCommits { get; private set; }

    public event Action<string>? ThemeChanged;

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

        // Auto-associate Brush and Color variants
        string brushKey = tokenName.EndsWith("Brush", StringComparison.OrdinalIgnoreCase) ? tokenName : tokenName + "Brush";
        string colorKey = tokenName.EndsWith("Brush", StringComparison.OrdinalIgnoreCase)
            ? tokenName.Substring(0, tokenName.Length - 5) + "Color"
            : tokenName;

        var brush = new ImmutableSolidColorBrush(color);
        lock (_layerGate)
        {
            _layer[brushKey] = brush;
            _layer[colorKey] = color;
            _layer[tokenName] = brush;
        }

        QueueCommit();
        ThemeChanged?.Invoke(_activeThemeId);
    }

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
                _layer["EditorFontFamily"] = family;
                if (size.HasValue) _layer["EditorFontSize"] = size.Value;
                break;
            case "ui":
            case "system":
            default:
                _layer["M3FontFamily"] = family;
                _layer["M3FontFamilyDisplay"] = family;
                if (size.HasValue) _layer["M3FontSizeBodyMedium"] = size.Value;
                break;
        }
    }

    public void SetDensity(LayoutDensity density)
    {
        var (tab, rail, small, medium) = density switch
        {
            LayoutDensity.Compact => (28.0, 42.0, 2.0, 6.0),
            LayoutDensity.Spacious => (42.0, 54.0, 6.0, 12.0),
            _ => (35.0, 48.0, 4.0, 8.0),
        };

        lock (_layerGate)
        {
            _layer["DensityTabHeight"] = tab;
            _layer["DensityRailWidth"] = rail;
            _layer["DensityPaddingSmall"] = small;
            _layer["DensityPaddingMedium"] = medium;
        }

        QueueCommit();
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
            foreach (var (key, hex) in def.Colors)
            {
                if (TryParseColor(hex, out var color))
                {
                    string brushKey = key.EndsWith("Brush", StringComparison.OrdinalIgnoreCase) ? key : key + "Brush";
                    string colorKey = key.EndsWith("Brush", StringComparison.OrdinalIgnoreCase)
                        ? key.Substring(0, key.Length - 5) + "Color"
                        : key;

                    var brush = new ImmutableSolidColorBrush(color);
                    _layer[brushKey] = brush;
                    _layer[colorKey] = color;
                    _layer[key] = brush;
                }
            }

            foreach (var (fontRole, fontName) in def.Fonts)
            {
                SetFontValues(fontRole, fontName, null);
            }
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
