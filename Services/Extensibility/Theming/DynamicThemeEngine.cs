using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using PdfEditorApp.Plugins.CSharpEditor.Services.Common;
using FrySharp.Sdk;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;

/// <summary>
/// Dynamic runtime theme engine. Modifies Avalonia Application.Current.Resources tokens
/// in real time on the UI thread without requiring app restart or view reconstruction.
/// </summary>
public class DynamicThemeEngine : IThemeApi
{
    private readonly ConcurrentDictionary<string, ThemeDefinition> _themes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private string _activeThemeId = "dark-plus";

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

        UiDispatchHelper.RunOnUi(() =>
        {
            if (Application.Current?.Resources is { } res)
            {
                var brush = new SolidColorBrush(color);
                res[brushKey] = brush;
                res[colorKey] = color;
                res[tokenName] = brush;
            }
        });

        ThemeChanged?.Invoke(_activeThemeId);
    }

    public string? GetColor(string tokenName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);

        if (_overrides.TryGetValue(tokenName, out var overrideHex))
        {
            return overrideHex;
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

        UiDispatchHelper.RunOnUi(() =>
        {
            if (Application.Current?.Resources is not { } res) return;

            var family = new FontFamily(fontFamily);

            switch (role.ToLowerInvariant())
            {
                case "editor":
                case "mono":
                case "code":
                    res["M3FontFamilyMono"] = family;
                    res["EditorFontFamily"] = family;
                    if (size.HasValue) res["EditorFontSize"] = size.Value;
                    break;
                case "ui":
                case "system":
                default:
                    res["M3FontFamily"] = family;
                    res["M3FontFamilyDisplay"] = family;
                    if (size.HasValue) res["M3FontSizeBodyMedium"] = size.Value;
                    break;
            }
        });
    }

    public void SetDensity(LayoutDensity density)
    {
        UiDispatchHelper.RunOnUi(() =>
        {
            if (Application.Current?.Resources is not { } res) return;

            switch (density)
            {
                case LayoutDensity.Compact:
                    res["DensityTabHeight"] = 28.0;
                    res["DensityRailWidth"] = 42.0;
                    res["DensityPaddingSmall"] = 2.0;
                    res["DensityPaddingMedium"] = 6.0;
                    break;
                case LayoutDensity.Comfortable:
                    res["DensityTabHeight"] = 35.0;
                    res["DensityRailWidth"] = 48.0;
                    res["DensityPaddingSmall"] = 4.0;
                    res["DensityPaddingMedium"] = 8.0;
                    break;
                case LayoutDensity.Spacious:
                    res["DensityTabHeight"] = 42.0;
                    res["DensityRailWidth"] = 54.0;
                    res["DensityPaddingSmall"] = 6.0;
                    res["DensityPaddingMedium"] = 12.0;
                    break;
            }
        });
    }

    public void SetSpacing(string tokenName, double value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);
        _overrides[tokenName] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        UiDispatchHelper.RunOnUi(() =>
        {
            if (Application.Current?.Resources is { } res)
            {
                res[tokenName] = value;
            }
        });

        ThemeChanged?.Invoke(_activeThemeId);
    }

    public void SetDimension(string tokenName, double value) => SetSpacing(tokenName, value);

    public void SetResource(string tokenName, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);
        ArgumentNullException.ThrowIfNull(value);
        _overrides[tokenName] = value.ToString() ?? string.Empty;

        UiDispatchHelper.RunOnUi(() =>
        {
            if (Application.Current?.Resources is { } res)
            {
                res[tokenName] = value;
            }
        });

        ThemeChanged?.Invoke(_activeThemeId);
    }

    public object? GetResource(string tokenName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenName);

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

        UiDispatchHelper.RunOnUi(() =>
        {
            if (Application.Current?.Resources is not { } res) return;

            // Switch Avalonia requested theme variant
            if (Application.Current is { } app)
            {
                app.RequestedThemeVariant = def.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
            }

            foreach (var (key, hex) in def.Colors)
            {
                if (TryParseColor(hex, out var color))
                {
                    string brushKey = key.EndsWith("Brush", StringComparison.OrdinalIgnoreCase) ? key : key + "Brush";
                    string colorKey = key.EndsWith("Brush", StringComparison.OrdinalIgnoreCase)
                        ? key.Substring(0, key.Length - 5) + "Color"
                        : key;

                    var brush = new SolidColorBrush(color);
                    res[brushKey] = brush;
                    res[colorKey] = color;
                    res[key] = brush;
                }
            }

            foreach (var (fontRole, fontName) in def.Fonts)
            {
                SetFont(fontRole, fontName);
            }
        });

        ThemeChanged?.Invoke(_activeThemeId);
        return true;
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
