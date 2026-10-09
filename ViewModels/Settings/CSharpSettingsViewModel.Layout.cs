using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

/// <summary>A layout in the gallery: a built-in preset or one of the user's.</summary>
public sealed partial class LayoutPresetItemViewModel : ObservableObject
{
    public string Id { get; init; } = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    public string Description { get; init; } = string.Empty;

    public bool IsUserLayout { get; init; }

    public LayoutSpec Spec { get; set; } = LayoutSpec.Default;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private bool _isConfirmingDelete;

    // What the card shows of the layout: its corner, its body text size, its spacing.
    public double SampleRadius => Math.Round(6 * Math.Clamp(Spec.RadiusScale, 0, 3) * 2) / 2;
    public double SampleFontSize => Math.Round(Spec.BaseFontSize, 1);
    public string Summary => $"{Spec.BaseFontSize:0.#} px · ◜×{Spec.RadiusScale:0.##}" + (Spec.Density == LayoutDensity.Comfortable ? string.Empty : $" · {Spec.Density}");
}

/// <summary>A per-component override (a corner radius or a size): Auto follows the global lever.</summary>
public sealed partial class LayoutOverrideRowViewModel : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public double Minimum { get; init; }
    public double Maximum { get; init; } = 40;

    [ObservableProperty]
    private bool _isOverridden;

    [ObservableProperty]
    private double _value;

    /// <summary>The value the global levers give it (shown while Auto).</summary>
    [ObservableProperty]
    private double _autoValue;

    public Action? Changed { get; set; }

    partial void OnIsOverriddenChanged(bool value) => Changed?.Invoke();
    partial void OnValueChanged(double value)
    {
        if (IsOverridden) Changed?.Invoke();
    }
}

/// <summary>A font in a picker.</summary>
public sealed class LayoutFontChoice
{
    public string Name { get; init; } = string.Empty;

    /// <summary>The family list to use; <c>null</c> for the studio's default.</summary>
    public string? Family { get; init; }

    public bool IsInstalled { get; init; } = true;

    public FontFamily Preview => Family == null ? FontFamily.Default : new FontFamily(Family);

    public string Note => IsInstalled ? string.Empty : "not installed";

    public override string ToString() => Name;
}

/// <summary>
/// Settings → Layout &amp; Typography: levers for the interface and code fonts, the type ramp, weights, corner radii,
/// borders, spacing and density, and shadows. A lever moves the preview at once; the whole studio follows when the
/// lever is let go (or 300 ms after a key or click), in one layout change. Every change is remembered in the
/// preferences, can be undone (50 steps), and can be saved as a named layout.
/// </summary>
public partial class CSharpSettingsViewModel
{
    private const int LayoutUndoDepth = 50;
    private static readonly TimeSpan LayoutCommitDelay = TimeSpan.FromMilliseconds(300);

    private LayoutSpec _layoutSpec = LayoutSpec.Default;
    private bool _syncingLayout;
    private DispatcherTimer? _layoutCommitTimer;
    private readonly LinkedList<LayoutSpec> _layoutUndo = new();
    private readonly Stack<LayoutSpec> _layoutRedo = new();

    public static IReadOnlyList<string> LayoutWeightOptions { get; } = ["Light", "Normal", "Medium", "SemiBold", "Bold", "ExtraBold"];

    public static IReadOnlyList<LayoutDensity> LayoutDensityOptions { get; } = [LayoutDensity.Compact, LayoutDensity.Comfortable, LayoutDensity.Spacious];

    public ObservableCollection<LayoutFontChoice> UiFonts { get; } = [];
    public ObservableCollection<LayoutFontChoice> CodeFonts { get; } = [];
    public ObservableCollection<LayoutPresetItemViewModel> LayoutPresetItems { get; } = [];
    public ObservableCollection<LayoutPresetItemViewModel> UserLayouts { get; } = [];
    public ObservableCollection<LayoutOverrideRowViewModel> RadiusOverrideRows { get; } = [];
    public ObservableCollection<LayoutOverrideRowViewModel> SizeOverrideRows { get; } = [];

    /// <summary>The type ramp as it would look (token, size), for the specimen.</summary>
    public ObservableCollection<LayoutRampRow> TypeRampRows { get; } = [];

    [ObservableProperty] private LayoutFontChoice? _selectedUiFont;
    [ObservableProperty] private LayoutFontChoice? _selectedCodeFont;
    [ObservableProperty] private double _layoutBaseFontSize = LayoutSpec.DefaultBaseFontSize;
    [ObservableProperty] private double _layoutHierarchy = 1;
    [ObservableProperty] private string _layoutBodyWeight = "Normal";
    [ObservableProperty] private string _layoutLabelWeight = "Medium";
    [ObservableProperty] private string _layoutEmphasisWeight = "SemiBold";
    [ObservableProperty] private string _layoutStrongWeight = "Bold";
    [ObservableProperty] private double _layoutCapsSpacing = 1;
    [ObservableProperty] private double _layoutRadiusScale = 1;
    [ObservableProperty] private double _layoutBorderWidth = 1;
    [ObservableProperty] private bool _layoutCardBorders = true;
    [ObservableProperty] private bool _layoutDividers = true;
    [ObservableProperty] private double _layoutAccentWidth = 2;
    [ObservableProperty] private LayoutDensity _layoutDensity = LayoutDensity.Comfortable;
    [ObservableProperty] private double _layoutSpacingScale = 1;
    [ObservableProperty] private double _layoutShadowStrength = 1;
    [ObservableProperty] private double _layoutShadowSoftness = 1;
    [ObservableProperty] private int _layoutCardElevation;
    [ObservableProperty] private bool _canUndoLayout;
    [ObservableProperty] private bool _canRedoLayout;
    [ObservableProperty] private string _activeLayoutName = "Studio";
    [ObservableProperty] private string _newLayoutName = string.Empty;

    /// <summary>Saving a theme also saves the current layout with it.</summary>
    [ObservableProperty] private bool _includeLayoutInTheme;

    /// <summary>The layout the levers describe (the preview shows it; the studio gets it on release).</summary>
    public LayoutSpec LayoutSpec => _layoutSpec;

    /// <summary>The preview's tokens changed (the specimen swaps them into its own resources).</summary>
    public event Action<IReadOnlyDictionary<string, object>>? LayoutPreviewChanged;

    /// <summary>The preview's tokens for the current levers.</summary>
    public IReadOnlyDictionary<string, object> LayoutPreviewTokens => LayoutTokenMapper.Map(_layoutSpec, IsStudioDark());

    private LayoutLibraryStore Layouts => _languageServices.LayoutLibrary;

    private void InitializeLayoutSettings()
    {
        foreach (var component in LayoutComponents.All)
        {
            RadiusOverrideRows.Add(new LayoutOverrideRowViewModel { Key = component, Label = component, Maximum = 32, Changed = OnLayoutLeverChanged });
        }

        foreach (var (key, label, min, max) in new (string, string, double, double)[]
                 {
                     ("ControlHeight", "Control height", 20, 48), ("ControlHeightSmall", "Small control height", 16, 40),
                     ("RowHeight", "List row height", 18, 48), ("TabHeight", "Tab height", 22, 56),
                     ("CardPaddingX", "Card padding (sides)", 4, 40), ("CardPaddingY", "Card padding (top, bottom)", 4, 40),
                 })
        {
            SizeOverrideRows.Add(new LayoutOverrideRowViewModel { Key = key, Label = label, Minimum = min, Maximum = max, Changed = OnLayoutLeverChanged });
        }

        PopulateFontChoices();
        foreach (var preset in LayoutPresets.All)
        {
            LayoutPresetItems.Add(new LayoutPresetItemViewModel { Id = preset.Id, Name = preset.Name, Description = preset.Description, Spec = preset.Spec });
        }

        LoadUserLayouts();

        var engine = StudioAppContext.Instance.ThemeEngine;
        var saved = LayoutSpec.FromJson(_settingsStore.GetSettings().Appearance.Layout);
        LoadLayoutIntoLevers(saved ?? engine.Layout);
        UpdateActiveLayout(_settingsStore.GetSettings().Appearance.ActiveLayoutId ?? _layoutSpec.PresetId);

        // Density changed from elsewhere (the Customization page, a script): the levers follow.
        engine.LayoutChanged += layout =>
        {
            void Sync()
            {
                if (layout.SameLayoutAs(_layoutSpec)) return;
                LoadLayoutIntoLevers(layout);
                RememberLayout(layout);
            }

            if (!PdfEditorApp.Plugins.CSharpEditor.Services.Common.UiDispatchHelper.HasLiveUiLifetime || Dispatcher.UIThread.CheckAccess()) Sync();
            else Dispatcher.UIThread.Post(Sync);
        };
    }

    // ── Levers → spec ────────────────────────────────────────────────────────

    partial void OnSelectedUiFontChanged(LayoutFontChoice? value) => OnLayoutLeverChanged();
    partial void OnSelectedCodeFontChanged(LayoutFontChoice? value) => OnLayoutLeverChanged();
    partial void OnLayoutBaseFontSizeChanged(double value) => OnLayoutLeverChanged();
    partial void OnLayoutHierarchyChanged(double value) => OnLayoutLeverChanged();
    partial void OnLayoutBodyWeightChanged(string value) => OnLayoutLeverChanged();
    partial void OnLayoutLabelWeightChanged(string value) => OnLayoutLeverChanged();
    partial void OnLayoutEmphasisWeightChanged(string value) => OnLayoutLeverChanged();
    partial void OnLayoutStrongWeightChanged(string value) => OnLayoutLeverChanged();
    partial void OnLayoutCapsSpacingChanged(double value) => OnLayoutLeverChanged();
    partial void OnLayoutRadiusScaleChanged(double value) => OnLayoutLeverChanged();
    partial void OnLayoutBorderWidthChanged(double value) => OnLayoutLeverChanged();
    partial void OnLayoutCardBordersChanged(bool value) => OnLayoutLeverChanged();
    partial void OnLayoutDividersChanged(bool value) => OnLayoutLeverChanged();
    partial void OnLayoutAccentWidthChanged(double value) => OnLayoutLeverChanged();
    partial void OnLayoutDensityChanged(LayoutDensity value) => OnLayoutLeverChanged();
    partial void OnLayoutSpacingScaleChanged(double value) => OnLayoutLeverChanged();
    partial void OnLayoutShadowStrengthChanged(double value) => OnLayoutLeverChanged();
    partial void OnLayoutShadowSoftnessChanged(double value) => OnLayoutLeverChanged();
    partial void OnLayoutCardElevationChanged(int value) => OnLayoutLeverChanged();

    private void OnLayoutLeverChanged()
    {
        if (_syncingLayout) return;
        _layoutSpec = SpecFromLevers();
        RefreshLayoutPreview();
        ScheduleLayoutCommit();
    }

    private LayoutSpec SpecFromLevers()
    {
        var radius = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var row in RadiusOverrideRows.Where(r => r.IsOverridden)) radius[row.Key] = Math.Round(row.Value, 1);
        var sizes = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var row in SizeOverrideRows.Where(r => r.IsOverridden)) sizes[row.Key] = Math.Round(row.Value, 1);
        return new LayoutSpec
        {
            UiFont = SelectedUiFont?.Family,
            CodeFont = SelectedCodeFont?.Family,
            BaseFontSize = Math.Round(LayoutBaseFontSize * 2) / 2,
            Hierarchy = Math.Round(LayoutHierarchy, 2),
            BodyWeight = LayoutBodyWeight,
            LabelWeight = LayoutLabelWeight,
            EmphasisWeight = LayoutEmphasisWeight,
            StrongWeight = LayoutStrongWeight,
            CapsLetterSpacing = Math.Round(LayoutCapsSpacing, 2),
            RadiusScale = Math.Round(LayoutRadiusScale, 2),
            RadiusOverrides = radius,
            BorderWidth = Math.Round(LayoutBorderWidth * 2) / 2,
            CardBorders = LayoutCardBorders,
            Dividers = LayoutDividers,
            AccentBarWidth = Math.Round(LayoutAccentWidth * 2) / 2,
            Density = LayoutDensity,
            SpacingScale = Math.Round(LayoutSpacingScale, 2),
            SizeOverrides = sizes,
            ShadowStrength = Math.Round(LayoutShadowStrength, 2),
            ShadowSoftness = Math.Round(LayoutShadowSoftness, 2),
            CardElevation = Math.Clamp(LayoutCardElevation, 0, 4),
        };
    }

    /// <summary>Moves every lever to <paramref name="spec"/> (without committing it).</summary>
    private void LoadLayoutIntoLevers(LayoutSpec spec)
    {
        _syncingLayout = true;
        try
        {
            SelectedUiFont = FontChoice(UiFonts, spec.UiFont);
            SelectedCodeFont = FontChoice(CodeFonts, spec.CodeFont);
            LayoutBaseFontSize = spec.BaseFontSize;
            LayoutHierarchy = spec.Hierarchy;
            LayoutBodyWeight = spec.BodyWeight;
            LayoutLabelWeight = spec.LabelWeight;
            LayoutEmphasisWeight = spec.EmphasisWeight;
            LayoutStrongWeight = spec.StrongWeight;
            LayoutCapsSpacing = spec.CapsLetterSpacing;
            LayoutRadiusScale = spec.RadiusScale;
            LayoutBorderWidth = spec.BorderWidth;
            LayoutCardBorders = spec.CardBorders;
            LayoutDividers = spec.Dividers;
            LayoutAccentWidth = spec.AccentBarWidth;
            LayoutDensity = spec.Density;
            LayoutSpacingScale = spec.SpacingScale;
            LayoutShadowStrength = spec.ShadowStrength;
            LayoutShadowSoftness = spec.ShadowSoftness;
            LayoutCardElevation = spec.CardElevation;
            foreach (var row in RadiusOverrideRows)
            {
                row.IsOverridden = spec.RadiusOverrides.TryGetValue(row.Key, out var r);
                row.Value = row.IsOverridden ? r : row.Value;
            }

            foreach (var row in SizeOverrideRows)
            {
                row.IsOverridden = spec.SizeOverrides.TryGetValue(row.Key, out var v);
                row.Value = row.IsOverridden ? v : row.Value;
            }
        }
        finally
        {
            _syncingLayout = false;
        }

        _layoutSpec = spec;
        RefreshLayoutPreview();
    }

    private static LayoutFontChoice? FontChoice(ObservableCollection<LayoutFontChoice> choices, string? family)
    {
        if (family == null) return choices.FirstOrDefault(c => c.Family == null);
        var match = choices.FirstOrDefault(c => string.Equals(c.Family, family, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;
        var custom = new LayoutFontChoice { Name = family.Split(',')[0].Trim(), Family = family };
        choices.Add(custom);
        return custom;
    }

    // ── Preview & commit ─────────────────────────────────────────────────────

    private void RefreshLayoutPreview()
    {
        var tokens = LayoutTokenMapper.Map(_layoutSpec, IsStudioDark());
        foreach (var row in RadiusOverrideRows) row.AutoValue = ((Avalonia.CornerRadius)tokens["DsRadius" + row.Key]).TopLeft;
        foreach (var row in SizeOverrideRows)
        {
            row.AutoValue = row.Key switch
            {
                "CardPaddingX" => ((Avalonia.Thickness)tokens["DsCardPadding"]).Left,
                "CardPaddingY" => ((Avalonia.Thickness)tokens["DsCardPadding"]).Top,
                var key => (double)tokens["Ds" + key],
            };
            if (!row.IsOverridden) row.Value = row.AutoValue;
        }

        var ramp = LayoutTokenMapper.TypeRamp.Select(step => new LayoutRampRow("DsFontSize" + step.Step, (double)tokens["DsFontSize" + step.Step])).ToList();
        if (TypeRampRows.Count == ramp.Count)
        {
            for (var i = 0; i < ramp.Count; i++) TypeRampRows[i].Size = ramp[i].Size;
        }
        else
        {
            TypeRampRows.Clear();
            foreach (var row in ramp) TypeRampRows.Add(row);
        }

        OnPropertyChanged(nameof(LayoutSpec));
        LayoutPreviewChanged?.Invoke(tokens);
    }

    private void ScheduleLayoutCommit()
    {
        if (!PdfEditorApp.Plugins.CSharpEditor.Services.Common.UiDispatchHelper.HasLiveUiLifetime)
        {
            CommitLayout();
            return;
        }

        if (_layoutCommitTimer == null)
        {
            _layoutCommitTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = LayoutCommitDelay };
            _layoutCommitTimer.Tick += (_, _) =>
            {
                _layoutCommitTimer!.Stop();
                CommitLayout();
            };
        }

        _layoutCommitTimer.Stop();
        _layoutCommitTimer.Start();
    }

    /// <summary>The slider was let go: the studio takes the layout now rather than after the pause.</summary>
    public void CommitLayoutNow()
    {
        _layoutCommitTimer?.Stop();
        CommitLayout();
    }

    /// <summary>The studio takes the levers' layout (one layout change), remembered and undoable.</summary>
    public void CommitLayout()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        if (_layoutSpec.SameLayoutAs(engine.Layout)) return;
        PushLayoutUndo(engine.Layout);
        var spec = _layoutSpec with { PresetId = null };
        _layoutSpec = spec;
        engine.ApplyLayout(spec);
        RememberLayout(spec);
        UpdateActiveLayout(null);
    }

    private void PushLayoutUndo(LayoutSpec previous, bool clearRedo = true)
    {
        _layoutUndo.AddLast(previous);
        while (_layoutUndo.Count > LayoutUndoDepth) _layoutUndo.RemoveFirst();
        if (clearRedo) _layoutRedo.Clear();
        CanUndoLayout = _layoutUndo.Count > 0;
        CanRedoLayout = _layoutRedo.Count > 0;
    }

    private void ApplyLayoutNow(LayoutSpec spec, string? activeId)
    {
        LoadLayoutIntoLevers(spec);
        StudioAppContext.Instance.ThemeEngine.ApplyLayout(spec);
        RememberLayout(spec, activeId);
        UpdateActiveLayout(activeId);
    }

    private void RememberLayout(LayoutSpec spec, string? activeId = null)
    {
        if (_initializing) return;
        var json = spec.ToJson();
        _settingsStore.Update(s =>
        {
            s.Appearance.Layout = json;
            s.Appearance.ActiveLayoutId = activeId;
            s.Appearance.Density = spec.Density.ToString();
        });
    }

    private void UpdateActiveLayout(string? id)
    {
        foreach (var item in LayoutPresetItems.Concat(UserLayouts))
        {
            item.IsActive = id != null ? string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase) : item.Spec.SameLayoutAs(_layoutSpec);
        }

        ActiveLayoutName = LayoutPresetItems.Concat(UserLayouts).FirstOrDefault(i => i.IsActive)?.Name ?? "Custom";
    }

    private static bool IsStudioDark()
    {
        var engine = StudioAppContext.Instance.ThemeEngine;
        return engine.GetTheme(engine.ActiveThemeId)?.IsDark ?? true;
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    public void ApplyLayoutPreset(LayoutPresetItemViewModel? item)
    {
        if (item == null) return;
        PushLayoutUndo(StudioAppContext.Instance.ThemeEngine.Layout);
        ApplyLayoutNow(item.Spec with { PresetId = item.Id }, item.Id);
        ShowNotification($"Layout: {item.Name}.", isError: false);
    }

    /// <summary>Compact, Comfortable or Spacious (the segmented buttons).</summary>
    [RelayCommand]
    public void SetLayoutDensity(string? density)
    {
        if (Enum.TryParse<LayoutDensity>(density, ignoreCase: true, out var value)) LayoutDensity = value;
    }

    /// <summary>A radius preset: None (0), Small (0.5), Medium (1), Large (1.6), Extra (2.2).</summary>
    [RelayCommand]
    public void SetLayoutRadiusPreset(string? scale)
    {
        if (double.TryParse(scale, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)) LayoutRadiusScale = value;
    }

    /// <summary>Cards flat (0) … floating (4).</summary>
    [RelayCommand]
    public void SetLayoutCardElevation(string? level)
    {
        if (int.TryParse(level, out var value)) LayoutCardElevation = Math.Clamp(value, 0, 4);
    }

    [RelayCommand]
    public void ResetLayout()
    {
        PushLayoutUndo(StudioAppContext.Instance.ThemeEngine.Layout);
        ApplyLayoutNow(LayoutPresets.All[0].Spec, LayoutPresets.StudioId);
    }

    [RelayCommand]
    public void UndoLayout()
    {
        if (_layoutUndo.Count == 0) return;
        _layoutRedo.Push(StudioAppContext.Instance.ThemeEngine.Layout);
        var previous = _layoutUndo.Last!.Value;
        _layoutUndo.RemoveLast();
        ApplyLayoutNow(previous, previous.PresetId);
        CanUndoLayout = _layoutUndo.Count > 0;
        CanRedoLayout = _layoutRedo.Count > 0;
    }

    [RelayCommand]
    public void RedoLayout()
    {
        if (_layoutRedo.Count == 0) return;
        PushLayoutUndo(StudioAppContext.Instance.ThemeEngine.Layout, clearRedo: false);
        var next = _layoutRedo.Pop();
        ApplyLayoutNow(next, next.PresetId);
        CanUndoLayout = _layoutUndo.Count > 0;
        CanRedoLayout = _layoutRedo.Count > 0;
    }

    [RelayCommand]
    public void SaveLayoutAs()
    {
        CommitLayoutNow();
        var name = string.IsNullOrWhiteSpace(NewLayoutName) ? $"Layout {DateTime.Now:d MMM HH:mm}" : NewLayoutName.Trim();
        var saved = Layouts.Save(name, _layoutSpec);
        var item = LayoutItemFor(saved);
        UserLayouts.Insert(0, item);
        _layoutSpec = item.Spec;
        RememberLayout(item.Spec, saved.Id);
        UpdateActiveLayout(saved.Id);
        NewLayoutName = string.Empty;
        ShowNotification($"Saved \"{saved.Name}\" to your layouts.", isError: false);
    }

    [RelayCommand]
    public void BeginRenameLayout(LayoutPresetItemViewModel? item)
    {
        if (item is not { IsUserLayout: true }) return;
        item.EditName = item.Name;
        item.IsConfirmingDelete = false;
        item.IsRenaming = true;
    }

    [RelayCommand]
    public void CancelRenameLayout(LayoutPresetItemViewModel? item)
    {
        if (item != null) item.IsRenaming = false;
    }

    [RelayCommand]
    public void RenameLayout(LayoutPresetItemViewModel? item)
    {
        if (item is not { IsUserLayout: true }) return;
        var newName = string.IsNullOrWhiteSpace(item.EditName) ? item.Name : item.EditName.Trim();
        item.IsRenaming = false;
        if (newName == item.Name) return;
        if (Layouts.Rename(item.Id, newName) is { } renamed)
        {
            item.Name = renamed.Name;
            if (item.IsActive) ActiveLayoutName = renamed.Name;
        }
    }

    [RelayCommand]
    public void DuplicateLayout(LayoutPresetItemViewModel? item)
    {
        if (item == null) return;
        var copy = Layouts.Duplicate(item.Spec, $"{item.Name} copy");
        UserLayouts.Insert(0, LayoutItemFor(copy));
        ShowNotification($"Saved a copy as \"{copy.Name}\".", isError: false);
    }

    [RelayCommand]
    public void AskDeleteLayout(LayoutPresetItemViewModel? item)
    {
        if (item is not { IsUserLayout: true }) return;
        item.IsRenaming = false;
        item.IsConfirmingDelete = !item.IsConfirmingDelete;
    }

    [RelayCommand]
    public void DeleteLayout(LayoutPresetItemViewModel? item)
    {
        if (item is not { IsUserLayout: true }) return;
        item.IsConfirmingDelete = false;
        if (!Layouts.Delete(item.Id)) return;
        UserLayouts.Remove(item);
        if (item.IsActive) UpdateActiveLayout(null);
        ShowNotification($"Deleted \"{item.Name}\".", isError: false);
    }

    private void LoadUserLayouts()
    {
        void Show(IReadOnlyList<SavedLayout> layouts)
        {
            foreach (var saved in layouts)
            {
                if (UserLayouts.All(i => !string.Equals(i.Id, saved.Id, StringComparison.OrdinalIgnoreCase))) UserLayouts.Add(LayoutItemFor(saved));
            }

            UpdateActiveLayout(_settingsStore.GetSettings().Appearance.ActiveLayoutId);
        }

        var library = Layouts;
        if (!PdfEditorApp.Plugins.CSharpEditor.Services.Common.UiDispatchHelper.HasLiveUiLifetime)
        {
            Show(library.List());
            return;
        }

        Task.Run(library.List).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully) Dispatcher.UIThread.Post(() => Show(t.Result));
        }, TaskScheduler.Default);
    }

    private static LayoutPresetItemViewModel LayoutItemFor(SavedLayout saved) => new()
    {
        Id = saved.Id,
        Name = saved.Name,
        Description = $"Saved {saved.ModifiedUtc.ToLocalTime():d MMM yyyy, HH:mm}",
        IsUserLayout = true,
        Spec = saved.Layout,
    };

    // ── Fonts ────────────────────────────────────────────────────────────────

    private static readonly (string Name, string Family)[] CuratedUiFonts =
    [
        ("Inter", "Inter"), ("Segoe UI Variable", "Segoe UI Variable, Segoe UI"), ("SF Pro Text", "SF Pro Text, -apple-system"),
        ("Helvetica Neue", "Helvetica Neue"), ("Roboto", "Roboto"), ("IBM Plex Sans", "IBM Plex Sans"), ("Source Sans 3", "Source Sans 3"),
        ("Noto Sans", "Noto Sans"), ("Ubuntu", "Ubuntu"), ("Open Sans", "Open Sans"),
    ];

    private static readonly (string Name, string Family)[] CuratedCodeFonts =
    [
        ("Cascadia Code", "Cascadia Code"), ("JetBrains Mono", "JetBrains Mono"), ("Fira Code", "Fira Code"), ("SF Mono", "SF Mono"),
        ("Menlo", "Menlo"), ("Consolas", "Consolas"), ("Source Code Pro", "Source Code Pro"), ("IBM Plex Mono", "IBM Plex Mono"),
        ("Ubuntu Mono", "Ubuntu Mono"), ("Courier New", "Courier New"),
    ];

    private void PopulateFontChoices()
    {
        var installed = InstalledFontNames();
        bool IsInstalled(string family) => installed == null || family.Split(',').Any(f => installed.Contains(f.Trim()));

        UiFonts.Add(new LayoutFontChoice { Name = "System default", Family = null });
        foreach (var (name, family) in CuratedUiFonts) UiFonts.Add(new LayoutFontChoice { Name = name, Family = family, IsInstalled = IsInstalled(family) });
        CodeFonts.Add(new LayoutFontChoice { Name = "Studio default (Cascadia Code, JetBrains Mono, …)", Family = null });
        foreach (var (name, family) in CuratedCodeFonts) CodeFonts.Add(new LayoutFontChoice { Name = name, Family = family, IsInstalled = IsInstalled(family) });

        if (installed == null) return;
        foreach (var name in installed.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (UiFonts.All(f => !string.Equals(f.Family, name, StringComparison.OrdinalIgnoreCase))) UiFonts.Add(new LayoutFontChoice { Name = name, Family = name });
        }
    }

    // The fonts this machine has (null when there is no platform to ask, as in tests).
    private static HashSet<string>? InstalledFontNames()
    {
        if (Avalonia.Application.Current == null) return null;
        try
        {
            return FontManager.Current.SystemFonts.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    // ── Export / import (W3C design tokens, CSS) ─────────────────────────────

    /// <summary>The layout as W3C DTCG design tokens (dimension, fontFamily, fontWeight, shadow), with the layout itself kept for import.</summary>
    public string ExportLayoutDtcg()
    {
        var tokens = LayoutTokenMapper.Map(_layoutSpec, IsStudioDark());
        var root = new JsonObject();
        foreach (var (key, value) in tokens.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var (type, json) = value switch
            {
                double d => ("dimension", (JsonNode)JsonValue.Create($"{d:0.##}px")!),
                Avalonia.CornerRadius r => ("dimension", JsonValue.Create($"{r.TopLeft:0.##}px")!),
                Avalonia.Thickness t => ("dimension", JsonValue.Create($"{t.Top:0.##}px {t.Right:0.##}px {t.Bottom:0.##}px {t.Left:0.##}px")!),
                FontWeight w => ("fontWeight", JsonValue.Create((int)w)!),
                FontFamily f => ("fontFamily", JsonValue.Create(f.Name)!),
                BoxShadows s => ("shadow", JsonValue.Create(Services.Extensibility.Theming.Layout.LayoutTokenXaml.Format(s))!),
                _ => ("string", JsonValue.Create(value.ToString())!),
            };
            root[key] = new JsonObject { ["$type"] = type, ["$value"] = json };
        }

        root["$extensions"] = new JsonObject { ["com.frypdf.layout"] = JsonNode.Parse(_layoutSpec.ToJson().GetRawText()) };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>The layout as CSS custom properties.</summary>
    public string ExportLayoutCss()
    {
        var tokens = LayoutTokenMapper.Map(_layoutSpec, IsStudioDark());
        var lines = new List<string> { ":root {" };
        foreach (var (key, value) in tokens.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var css = value switch
            {
                double d => $"{d:0.##}px",
                Avalonia.CornerRadius r => $"{r.TopLeft:0.##}px",
                Avalonia.Thickness t => $"{t.Top:0.##}px {t.Right:0.##}px {t.Bottom:0.##}px {t.Left:0.##}px",
                FontWeight w => ((int)w).ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontFamily f => f.Name,
                BoxShadows s => string.Join(", ", Enumerable.Range(0, s.Count).Select(i => $"{s[i].OffsetX:0.#}px {s[i].OffsetY:0.#}px {s[i].Blur:0.#}px {s[i].Spread:0.#}px #{s[i].Color.R:X2}{s[i].Color.G:X2}{s[i].Color.B:X2}{s[i].Color.A:X2}")),
                _ => value.ToString(),
            };
            lines.Add($"  --{Kebab(key)}: {css};");
        }

        lines.Add("}");
        return string.Join("\n", lines);

        // DsRadiusMDTop -> ds-radius-md-top: a capital starts a word after a lower-case letter, or ends an acronym.
        static string Kebab(string key)
        {
            var text = new System.Text.StringBuilder();
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                var startsWord = i > 0 && char.IsUpper(c) &&
                                 (char.IsLower(key[i - 1]) || (char.IsUpper(key[i - 1]) && i + 1 < key.Length && char.IsLower(key[i + 1])));
                if (startsWord) text.Append('-');
                text.Append(char.ToLowerInvariant(c));
            }

            return text.ToString();
        }
    }

    /// <summary>A layout exported by the studio (DTCG JSON): applied and saved to My layouts. Returns it, or null.</summary>
    public SavedLayout? ImportLayoutDtcg(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            var embedded = node?["$extensions"]?["com.frypdf.layout"];
            var spec = embedded == null ? null : LayoutSpec.FromJson(JsonSerializer.Deserialize<JsonElement>(embedded.ToJsonString()));
            if (spec == null)
            {
                ShowNotification("That JSON isn't a layout exported by the studio (no com.frypdf.layout extension).", isError: true);
                return null;
            }

            var name = string.IsNullOrWhiteSpace(NewLayoutName) ? $"Imported layout {DateTime.Now:d MMM HH:mm}" : NewLayoutName.Trim();
            var saved = Layouts.Save(name, spec);
            UserLayouts.Insert(0, LayoutItemFor(saved));
            PushLayoutUndo(StudioAppContext.Instance.ThemeEngine.Layout);
            ApplyLayoutNow(saved.Layout, saved.Id);
            NewLayoutName = string.Empty;
            ShowNotification($"Imported and saved \"{saved.Name}\".", isError: false);
            return saved;
        }
        catch (JsonException ex)
        {
            ShowNotification($"Couldn't read the layout JSON: {ex.Message}", isError: true);
            return null;
        }
    }

    [RelayCommand]
    public async Task CopyLayoutDtcgAsync() => await CopyExportTextAsync(ExportLayoutDtcg(), "Copied the layout as design tokens (DTCG JSON).");

    [RelayCommand]
    public async Task CopyLayoutCssAsync() => await CopyExportTextAsync(ExportLayoutCss(), "Copied the layout as CSS variables.");

    [RelayCommand]
    public async Task ImportLayoutFromClipboardAsync()
    {
        string? json = null;
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.Clipboard != null)
        {
            json = await desktop.MainWindow.Clipboard.TryGetTextAsync();
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            ShowNotification("The clipboard has no text.", isError: true);
            return;
        }

        ImportLayoutDtcg(json);
    }
}

/// <summary>A step of the type ramp in the preview.</summary>
public sealed partial class LayoutRampRow(string token, double size) : ObservableObject
{
    public string Token { get; } = token;

    [ObservableProperty]
    private double _size = size;
}
