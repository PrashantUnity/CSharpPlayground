using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Threading;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;

/// <summary>
/// Layout design tokens on controls, by key: <c>lt:Tokens.FontSize="DsFontSize200"</c> on an element, or
/// <c>&lt;Setter Property="lt:Tokens.CornerRadius" Value="DsRadiusMD" /&gt;</c> in a style. The control gets the token's
/// value for the current layout (see <see cref="LayoutTokenMapper"/>), at the same priority the key was set with, and gets
/// the new value whenever the layout changes.
/// <para>
/// Why not <c>{DynamicResource}</c>: a dynamic resource in a shared style's setter makes Avalonia build that style for
/// every control it matches; across the studio's ~260 style setters that held ~140 MB (measured), and every resource
/// binding is re-resolved on each colour-theme switch. A key is a plain string: shared styles stay shared, a colour
/// switch doesn't touch layout values, and a layout change sets each value once from a weak registry (~35 ms for 20,000
/// controls) instead of re-resolving or restyling the tree.
/// </para>
/// <para>
/// A key starting with <c>=</c> is a fixed value (<c>Value="=0,2,1,0"</c>): in shared styles every setter of these
/// properties goes through a key, so that which style wins is decided by Avalonia on the key (a value set from code
/// can't take part in style ordering) and only the winner's value is applied.
/// </para>
/// </summary>
public sealed class Tokens
{
    public static readonly AttachedProperty<string?> FontSizeProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("FontSize");
    public static readonly AttachedProperty<string?> FontWeightProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("FontWeight");
    public static readonly AttachedProperty<string?> FontFamilyProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("FontFamily");
    public static readonly AttachedProperty<string?> LetterSpacingProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("LetterSpacing");
    public static readonly AttachedProperty<string?> CornerRadiusProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("CornerRadius");
    public static readonly AttachedProperty<string?> BorderThicknessProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("BorderThickness");
    public static readonly AttachedProperty<string?> PaddingProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("Padding");
    public static readonly AttachedProperty<string?> HeightProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("Height");
    public static readonly AttachedProperty<string?> MinHeightProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("MinHeight");
    public static readonly AttachedProperty<string?> SpacingProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("Spacing");
    public static readonly AttachedProperty<string?> BoxShadowProperty =
        AvaloniaProperty.RegisterAttached<Tokens, AvaloniaObject, string?>("BoxShadow");

    // The controls carrying keys, and what was set on each (so a layout change can set it again, and a key that goes
    // away takes its value with it).
    private static readonly ConditionalWeakTable<AvaloniaObject, Applied> Registry = new();
    private static readonly Dictionary<(Type, string), AvaloniaProperty?> Targets = new();
    private static int _subscribed;

    private sealed class Applied
    {
        public readonly Dictionary<AvaloniaProperty, (string Key, BindingPriority Priority, AvaloniaProperty Target, IDisposable? Handle)> Values = new();
    }

    static Tokens()
    {
        FontSizeProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        FontWeightProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        FontFamilyProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        LetterSpacingProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        CornerRadiusProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        BorderThicknessProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        PaddingProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        HeightProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        MinHeightProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        SpacingProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
        BoxShadowProperty.Changed.AddClassHandler<AvaloniaObject>(OnKeyChanged);
    }

    private Tokens()
    {
    }

    private static void OnKeyChanged(AvaloniaObject target, AvaloniaPropertyChangedEventArgs e) =>
        OnKeyChanged(target, (AttachedProperty<string?>)e.Property, e.NewValue as string, e.Priority);

    public static string? GetFontSize(AvaloniaObject o) => o.GetValue(FontSizeProperty);
    public static void SetFontSize(AvaloniaObject o, string? key) => o.SetValue(FontSizeProperty, key);
    public static string? GetFontWeight(AvaloniaObject o) => o.GetValue(FontWeightProperty);
    public static void SetFontWeight(AvaloniaObject o, string? key) => o.SetValue(FontWeightProperty, key);
    public static string? GetFontFamily(AvaloniaObject o) => o.GetValue(FontFamilyProperty);
    public static void SetFontFamily(AvaloniaObject o, string? key) => o.SetValue(FontFamilyProperty, key);
    public static string? GetLetterSpacing(AvaloniaObject o) => o.GetValue(LetterSpacingProperty);
    public static void SetLetterSpacing(AvaloniaObject o, string? key) => o.SetValue(LetterSpacingProperty, key);
    public static string? GetCornerRadius(AvaloniaObject o) => o.GetValue(CornerRadiusProperty);
    public static void SetCornerRadius(AvaloniaObject o, string? key) => o.SetValue(CornerRadiusProperty, key);
    public static string? GetBorderThickness(AvaloniaObject o) => o.GetValue(BorderThicknessProperty);
    public static void SetBorderThickness(AvaloniaObject o, string? key) => o.SetValue(BorderThicknessProperty, key);
    public static string? GetPadding(AvaloniaObject o) => o.GetValue(PaddingProperty);
    public static void SetPadding(AvaloniaObject o, string? key) => o.SetValue(PaddingProperty, key);
    public static string? GetHeight(AvaloniaObject o) => o.GetValue(HeightProperty);
    public static void SetHeight(AvaloniaObject o, string? key) => o.SetValue(HeightProperty, key);
    public static string? GetMinHeight(AvaloniaObject o) => o.GetValue(MinHeightProperty);
    public static void SetMinHeight(AvaloniaObject o, string? key) => o.SetValue(MinHeightProperty, key);
    public static string? GetSpacing(AvaloniaObject o) => o.GetValue(SpacingProperty);
    public static void SetSpacing(AvaloniaObject o, string? key) => o.SetValue(SpacingProperty, key);
    public static string? GetBoxShadow(AvaloniaObject o) => o.GetValue(BoxShadowProperty);
    public static void SetBoxShadow(AvaloniaObject o, string? key) => o.SetValue(BoxShadowProperty, key);

    /// <summary>How many controls carry token keys now (tests and perf tools).</summary>
    public static int RegisteredCount
    {
        get
        {
            var count = 0;
            foreach (var _ in Registry) count++;
            return count;
        }
    }

    /// <summary>Sets every keyed value again from the current layout (the engine's layout changed).</summary>
    public static void Refresh() => Refresh(null);

    // Only some keys (the shadows, which depend on light or dark), or all of them.
    private static void Refresh(AvaloniaProperty? only)
    {
        foreach (var (target, applied) in Registry)
        {
            // Only objects of this thread (the UI thread, in the studio); another thread's controls can't be touched.
            if (!target.CheckAccess()) continue;
            foreach (var (keyProperty, entry) in new List<KeyValuePair<AvaloniaProperty, (string Key, BindingPriority Priority, AvaloniaProperty Target, IDisposable? Handle)>>(applied.Values))
            {
                if (only == null || keyProperty == only) Apply(target, applied, keyProperty, entry.Key, entry.Priority, entry.Target);
            }
        }
    }

    private static void OnKeyChanged(AvaloniaObject target, AttachedProperty<string?> keyProperty, string? key, BindingPriority priority)
    {
        EnsureSubscribed();
        var applied = Registry.GetValue(target, _ => new Applied());
        if (applied.Values.TryGetValue(keyProperty, out var previous))
        {
            Remove(target, previous.Priority, previous.Target, previous.Handle);
            applied.Values.Remove(keyProperty);
        }

        if (string.IsNullOrEmpty(key)) return;
        var property = TargetFor(target.GetType(), keyProperty.Name);
        if (property == null) return;
        Apply(target, applied, keyProperty, key, priority, property);
    }

    private static void Apply(AvaloniaObject target, Applied applied, AvaloniaProperty keyProperty, string key, BindingPriority priority, AvaloniaProperty property)
    {
        if (!TryResolve(key, property.PropertyType, out var value) || !Fit(ref value, property.PropertyType))
        {
            System.Diagnostics.Debug.WriteLine($"[Tokens] Layout token '{key}' doesn't fit {target.GetType().Name}.{property.Name}");
            return;
        }

        if (applied.Values.TryGetValue(keyProperty, out var previous) && previous.Priority != BindingPriority.LocalValue)
        {
            previous.Handle?.Dispose();
        }

        IDisposable? handle = null;
        if (priority == BindingPriority.LocalValue) target.SetValue(property, value);
        else handle = target.SetValue(property, value, priority);
        applied.Values[keyProperty] = (key, priority, property, handle);
    }

    // A number on a thickness or a corner radius means "the same on every side"; anything else that doesn't fit is
    // skipped (a wrong key must not take the window down).
    private static bool Fit(ref object value, Type type)
    {
        if (type.IsInstanceOfType(value)) return true;
        if (value is double d)
        {
            if (type == typeof(Thickness)) { value = new Thickness(d); return true; }
            if (type == typeof(CornerRadius)) { value = new CornerRadius(d); return true; }
        }

        if (value is Thickness t && type == typeof(double)) { value = t.Left; return true; }
        if (value is CornerRadius r && type == typeof(double)) { value = r.TopLeft; return true; }
        return false;
    }

    private static readonly Dictionary<(Type, string), object?> Literals = new();

    /// <summary>A token's value for the current layout, or a fixed <c>=value</c> parsed for the property's type.</summary>
    public static bool TryResolve(string key, Type type, out object value)
    {
        if (!key.StartsWith('='))
        {
            return LayoutTokens.TryGet(key, out value);
        }

        lock (Literals)
        {
            if (!Literals.TryGetValue((type, key), out var parsed))
            {
                parsed = ParseLiteral(key[1..], type);
                Literals[(type, key)] = parsed;
            }

            value = parsed!;
            return parsed != null;
        }
    }

    private static object? ParseLiteral(string text, Type type)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            if (type == typeof(double)) return double.Parse(text, invariant);
            if (type == typeof(Thickness)) return Thickness.Parse(text);
            if (type == typeof(CornerRadius)) return CornerRadius.Parse(text);
            if (type == typeof(Avalonia.Media.FontWeight)) return Enum.Parse<Avalonia.Media.FontWeight>(text, ignoreCase: true);
            if (type == typeof(Avalonia.Media.FontFamily)) return new Avalonia.Media.FontFamily(text);
            if (type == typeof(Avalonia.Media.BoxShadows)) return Avalonia.Media.BoxShadows.Parse(text);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            System.Diagnostics.Debug.WriteLine($"[Tokens] Can't read '{text}' as {type.Name}: {ex.Message}");
        }

        return null;
    }

    private static void Remove(AvaloniaObject target, BindingPriority priority, AvaloniaProperty property, IDisposable? handle)
    {
        if (priority == BindingPriority.LocalValue) target.ClearValue(property);
        else handle?.Dispose();
    }

    // The property a key sets on this kind of object (Border.CornerRadius and TemplatedControl.CornerRadius are two).
    private static AvaloniaProperty? TargetFor(Type type, string name)
    {
        lock (Targets)
        {
            if (Targets.TryGetValue((type, name), out var known)) return known;
            var property = AvaloniaPropertyRegistry.Instance.FindRegistered(type, name);
            if (property == null)
            {
                foreach (var attached in AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(type))
                {
                    if (attached.Name == name && attached.OwnerType.Name is "TextElement" or "TextBlock") property = attached;
                }
            }

            Targets[(type, name)] = property;
            return property;
        }
    }

    private static void EnsureSubscribed()
    {
        if (System.Threading.Interlocked.Exchange(ref _subscribed, 1) == 1) return;
        var engine = StudioAppContext.Instance.ThemeEngine;
        engine.LayoutChanged += _ => OnUi(() => Refresh(null));
        // Shadows are deeper on dark themes: a theme switch refreshes them (and nothing else).
        var wasDark = engine.GetTheme(engine.ActiveThemeId)?.IsDark ?? true;
        engine.ThemeChanged += id =>
        {
            var isDark = engine.GetTheme(id)?.IsDark ?? true;
            if (isDark == wasDark) return;
            wasDark = isDark;
            OnUi(() => Refresh(BoxShadowProperty));
        };

        static void OnUi(Action action)
        {
            if (!PdfEditorApp.Plugins.CSharpEditor.Services.Common.UiDispatchHelper.HasLiveUiLifetime || Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        }
    }
}
