using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrySharp.Sdk;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette;

namespace PdfEditorApp.Plugins.CSharpEditor.ViewModels.Settings;

/// <summary>One section of the palette being designed: its colour, whether it is locked, and how readable it is.</summary>
public sealed partial class PaletteSectionItemViewModel : ObservableObject
{
    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    /// <summary>The colour itself (<see cref="Hex"/> is what the chosen vision simulation shows).</summary>
    public string RealHex { get; set; } = string.Empty;

    [ObservableProperty]
    private string _hex = "#000000";

    /// <summary>Text drawn on the swatch: white or near-black, whichever reads better.</summary>
    [ObservableProperty]
    private string _onHex = "#FFFFFF";

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private string _contrastLabel = string.Empty;

    [ObservableProperty]
    private bool _passesContrast = true;

    /// <summary>The hex typed in the section's box (committed with Enter).</summary>
    [ObservableProperty]
    private string _editHex = string.Empty;
}

/// <summary>A colour engine as the selector shows it.</summary>
public sealed class ColorEngineOption
{
    public ColorEngineKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// The palette studio: the palette is a <see cref="PaletteSpec"/> (engine, harmony, sections, locks, seed) that the
/// wheel and sliders edit and Generate, lock, regenerate-one and set-by-hand change. Each change is previewed at once
/// (a palette takes a few milliseconds), remembered in the preferences, and can be undone (50 steps); Apply makes it the
/// studio's theme.
/// </summary>
public partial class CSharpSettingsViewModel
{
    private const int PaletteUndoDepth = 50;

    private PaletteSpec _paletteSpec = new();
    private PaletteSpec? _appliedPaletteSpec;
    private GeneratedPalette? _currentPalette;
    private readonly LinkedList<PaletteSpec> _paletteUndo = new();
    private readonly Stack<PaletteSpec> _paletteRedo = new();
    private bool _syncingPaletteControls;
    private (float Hue, ColorHarmonyMode Mode, float Shift, ColorEngineKind Engine)? _baseHueSource;

    [ObservableProperty]
    private ColorEngineKind _selectedColorEngine = ColorEngineKind.Oklch;

    [ObservableProperty]
    private ColorEngineOption? _selectedColorEngineOption;

    [ObservableProperty]
    private bool _canUndoPalette;

    [ObservableProperty]
    private bool _canRedoPalette;

    [ObservableProperty]
    private string _chartSafetyLabel = string.Empty;

    [ObservableProperty]
    private bool _chartsAreSafe = true;

    [ObservableProperty]
    private string _editorPreviewBgHex = "#0D1117";

    [ObservableProperty]
    private string _editorPreviewFgHex = "#E6EDF3";

    [ObservableProperty]
    private string _harmonyAnchorHint = string.Empty;

    public IReadOnlyList<ColorEngineOption> AvailableColorEngines { get; } =
    [
        new() { Kind = ColorEngineKind.Oklch, Title = "OKLCH", Description = "Perceptually even lightness and hue (CSS Color 4, Tailwind 4)" },
        new() { Kind = ColorEngineKind.Hct, Title = "Material HCT", Description = "Tone predicts contrast (Material Design 3)" },
    ];

    public ObservableCollection<PaletteSectionItemViewModel> CoreSections { get; } = [];

    public ObservableCollection<PaletteSectionItemViewModel> SyntaxSections { get; } = [];

    public ObservableCollection<PaletteSectionItemViewModel> ChartSections { get; } = [];

    /// <summary>The palette being designed.</summary>
    public PaletteSpec PaletteSpec => _paletteSpec;

    /// <summary>The palette worked out from <see cref="PaletteSpec"/> for the previews.</summary>
    public GeneratedPalette CurrentPalette => _currentPalette ??= PaletteGenerator.Generate(_paletteSpec);

    partial void OnSelectedColorEngineOptionChanged(ColorEngineOption? value)
    {
        if (value != null && SelectedColorEngine != value.Kind) SelectedColorEngine = value.Kind;
    }

    partial void OnSelectedColorEngineChanged(ColorEngineKind value)
    {
        if (SelectedColorEngineOption?.Kind != value) SelectedColorEngineOption = AvailableColorEngines.First(o => o.Kind == value);
        if (!_syncingPaletteControls && !_initializing)
        {
            // The palette as it was before the switch (the last one worked out), so undo goes back to the other engine.
            PushPaletteUndo(_paletteSpec);
            UpdatePaletteHistoryState();
        }

        UpdateHarmonyChords();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    /// <summary>A new palette: every unlocked section changes, locked ones stay (Space).</summary>
    [RelayCommand]
    public void GeneratePalette() => ChangePalette(PaletteGenerator.Regenerate(CurrentSpec()));

    [RelayCommand]
    public void ToggleSectionLock(PaletteSectionItemViewModel? item)
    {
        if (item == null) return;
        var spec = CurrentSpec();
        ChangePalette(PaletteGenerator.SetLocked(spec, item.Id, !spec.IsLocked(item.Id), PaletteGenerator.Generate(spec)));
    }

    /// <summary>A new colour for this section only.</summary>
    [RelayCommand]
    public void RegenerateSection(PaletteSectionItemViewModel? item)
    {
        if (item == null) return;
        ChangePalette(PaletteGenerator.RegenerateSection(CurrentSpec(), item.Id));
    }

    /// <summary>Sets the section to the hex typed in its box, and locks it.</summary>
    [RelayCommand]
    public void SetSectionColor(PaletteSectionItemViewModel? item)
    {
        if (item == null) return;
        if (!ColorRgb.TryParseHex(item.EditHex, out var color))
        {
            ShowNotification($"\"{item.EditHex}\" isn't a colour (use #RRGGBB).", isError: true);
            item.EditHex = item.RealHex;
            return;
        }

        item.EditHex = color.ToHex();
        ChangePalette(PaletteGenerator.SetSection(CurrentSpec(), item.Id, color));
    }

    [RelayCommand]
    public void UndoPalette()
    {
        if (_paletteUndo.Count == 0) return;
        _paletteRedo.Push(CurrentSpec());
        var previous = _paletteUndo.Last!.Value;
        _paletteUndo.RemoveLast();
        LoadPaletteSpec(previous);
        UpdatePaletteHistoryState();
    }

    [RelayCommand]
    public void RedoPalette()
    {
        if (_paletteRedo.Count == 0) return;
        PushPaletteUndo(clearRedo: false);
        LoadPaletteSpec(_paletteRedo.Pop());
        UpdatePaletteHistoryState();
    }

    // ── Spec ⇄ controls ──────────────────────────────────────────────────────

    /// <summary>The spec with the wheel and sliders as they are now.</summary>
    private PaletteSpec CurrentSpec()
    {
        if (_syncingPaletteControls) return _paletteSpec;
        var source = (SelectedHueDegrees, SelectedHarmonyMode, AccentShift, SelectedColorEngine);
        var baseHue = _paletteSpec.BaseHue;
        if (_baseHueSource != source)
        {
            // The wheel moved (or the engine changed): the primary goes where the wheel points, in the engine's hue.
            var config = GetCurrentHarmonicConfiguration();
            baseHue = HarmonicColorGenerator.EngineHue(ColorEngines.Get(SelectedColorEngine), HarmonicColorGenerator.AccentHue(config));
            _baseHueSource = source;
        }

        return _paletteSpec with
        {
            Engine = SelectedColorEngine,
            Harmony = SelectedHarmonyMode,
            BaseHue = baseHue,
            IsDark = IsThemeDarkMode,
            ChromaBoost = Math.Clamp(SaturationBoost, 0.2f, 2.0f),
            NeutralTint = Math.Clamp(NeutralTint, 0f, 35f),
            SemanticPull = Math.Clamp(SemanticPull, 0f, 100f),
            ContrastTarget = Math.Clamp(ContrastTarget, 3f, 21f),
        };
    }

    private void ChangePalette(PaletteSpec next)
    {
        PushPaletteUndo();
        LoadPaletteSpec(next);
        UpdatePaletteHistoryState();
    }

    /// <summary>Makes <paramref name="spec"/> the palette being designed and moves the wheel and sliders to match it.</summary>
    private void LoadPaletteSpec(PaletteSpec spec)
    {
        _paletteSpec = spec;
        _currentPalette = null;
        _syncingPaletteControls = true;
        try
        {
            SelectedColorEngine = spec.Engine;
            SelectedHarmonyMode = spec.Harmony;
            IsThemeDarkMode = spec.IsDark;
            SaturationBoost = (float)spec.ChromaBoost;
            NeutralTint = (float)spec.NeutralTint;
            SemanticPull = (float)spec.SemanticPull;
            ContrastTarget = (float)spec.ContrastTarget;
            SelectedHueDegrees = WheelBaseHue(spec);
            _baseHueSource = (SelectedHueDegrees, SelectedHarmonyMode, AccentShift, SelectedColorEngine);
        }
        finally
        {
            _syncingPaletteControls = false;
        }

        RememberPalette();
        UpdateHarmonyChords();
    }

    // Where the wheel points for a spec: the colour-wheel hue of its primary, less the harmony's accent offset.
    private float WheelBaseHue(PaletteSpec spec)
    {
        var engine = ColorEngines.Get(spec.Engine);
        var primaryHue = PaletteGenerator.PrimaryHarmonyHue(spec, engine);
        var accentWheelHue = engine.Compose(primaryHue, engine.VividChroma, engine.ToneFromLstar(60)).ToHsl().H;
        var withMode = HarmonicColorGenerator.AccentHue(new HarmonicConfiguration { BaseHue = 0, Mode = spec.Harmony, AccentShift = AccentShift });
        var hue = (accentWheelHue - withMode) % 360f;
        return (float)Math.Round(hue < 0 ? hue + 360f : hue, 1);
    }

    private void PushPaletteUndo(PaletteSpec? spec = null, bool clearRedo = true)
    {
        _paletteUndo.AddLast(spec ?? CurrentSpec());
        while (_paletteUndo.Count > PaletteUndoDepth) _paletteUndo.RemoveFirst();
        if (clearRedo) _paletteRedo.Clear();
    }

    private void UpdatePaletteHistoryState()
    {
        CanUndoPalette = _paletteUndo.Count > 0;
        CanRedoPalette = _paletteRedo.Count > 0;
    }

    private void RememberPalette()
    {
        if (_initializing) return;
        var json = _paletteSpec.ToJson();
        _settingsStore.Update(s => s.Appearance.Palette = json);
    }

    private void RestorePaletteSpec()
    {
        if (PaletteSpec.FromJson(_settingsStore.GetSettings().Appearance.Palette) is { } saved)
        {
            _paletteSpec = saved;
            _currentPalette = null;
            _syncingPaletteControls = true;
            try
            {
                SelectedColorEngine = saved.Engine;
            }
            finally
            {
                _syncingPaletteControls = false;
            }

            // The wheel was restored with the other controls: keep the saved base hue rather than re-deriving it.
            _baseHueSource = (SelectedHueDegrees, SelectedHarmonyMode, AccentShift, SelectedColorEngine);
        }

        SelectedColorEngineOption = AvailableColorEngines.First(o => o.Kind == SelectedColorEngine);

        // The rows exist (and are coloured) before the page first draws; later changes recolour them in place.
        RefreshPalettePreview();
    }

    // ── Theme ────────────────────────────────────────────────────────────────

    /// <summary>The theme the palette being designed makes, for <paramref name="isDark"/> (the designed scheme by default).</summary>
    public ThemeDefinition BuildPaletteTheme(bool? isDark = null)
    {
        var spec = CurrentSpec();
        if (isDark is bool dark) spec = spec with { IsDark = dark };
        return HarmonicColorGenerator.ThemeFromPalette(PaletteGenerator.Generate(spec), HarmonicColorGenerator.AccentHue(GetCurrentHarmonicConfiguration()));
    }

    // ── Previews ─────────────────────────────────────────────────────────────

    /// <summary>Works out the palette and recolours the section rows in place.</summary>
    private (GeneratedPalette Palette, IReadOnlyDictionary<string, string> Tokens) RefreshPalettePreview()
    {
        _paletteSpec = CurrentSpec();
        var palette = _currentPalette = PaletteGenerator.Generate(_paletteSpec);
        var tokens = ThemeTokenMapper.Map(palette);
        RememberPalette();

        var target = (float)_paletteSpec.ContrastTarget;
        SyncSections(CoreSections, PaletteSections.Core, id => id switch
        {
            PaletteSections.NeutralVariant => "Muted grey",
            _ => id,
        }, (id, color) => id is PaletteSections.Neutral or PaletteSections.NeutralVariant
            ? ("Greys & surfaces", true)
            : Contrast(color, palette.Surface, target));
        SyncSections(SyntaxSections, PaletteSections.Syntax, id => id[PaletteSections.SyntaxPrefix.Length..] switch
        {
            "Preprocessor" => "Directive",
            var role => role,
        },
            (id, color) => Contrast(color, palette.EditorBackground, id.EndsWith("Comment", StringComparison.Ordinal) ? 3f : 4.5f));
        SyncSections(ChartSections, PaletteSections.Charts, id => "Series " + id[PaletteSections.ChartPrefix.Length..],
            (_, color) => Contrast(color, palette.Surface, 3f));

        var distance = PaletteGenerator.MinimumChartDistance(palette);
        ChartsAreSafe = distance >= 0.035;
        ChartSafetyLabel = ChartsAreSafe
            ? $"Distinct with colour-vision deficiencies (ΔE ≥ {distance:0.000})"
            : $"Two series look alike with a colour-vision deficiency (ΔE {distance:0.000})";
        EditorPreviewBgHex = palette.EditorBackground.ToHex();
        EditorPreviewFgHex = tokens["EditorFgBrush"];

        var anchor = PaletteSections.Chromatic.FirstOrDefault(id => _paletteSpec.Section(id) is { KeyColor: not null } s && (s.Locked || s.Source == SectionSource.Manual));
        HarmonyAnchorHint = anchor == null ? string.Empty : $"{anchor} is locked: the harmony is built around it.";
        return (palette, tokens);

        void SyncSections(ObservableCollection<PaletteSectionItemViewModel> rows, IReadOnlyList<string> ids, Func<string, string> label, Func<string, ColorRgb, (string Label, bool Passes)> contrast)
        {
            if (rows.Count != ids.Count)
            {
                rows.Clear();
                foreach (var id in ids) rows.Add(new PaletteSectionItemViewModel { Id = id, Label = label(id) });
            }

            foreach (var row in rows)
            {
                var color = palette.Keys[row.Id];
                var hex = color.ToHex();
                // A hex being typed in the row's box is left alone.
                var editing = row.RealHex.Length > 0 && !string.Equals(row.EditHex, row.RealHex, StringComparison.OrdinalIgnoreCase);
                row.RealHex = hex;
                row.Hex = SimulateHex(hex);
                if (!editing) row.EditHex = hex;
                row.OnHex = ColorRgb.White.ContrastRatio(color) >= ColorRgb.FromHex("#111111").ContrastRatio(color) ? "#FFFFFF" : "#111111";
                row.IsLocked = _paletteSpec.IsLocked(row.Id);
                (row.ContrastLabel, row.PassesContrast) = contrast(row.Id, color);
            }
        }

        static (string, bool) Contrast(ColorRgb color, ColorRgb against, float minimum)
        {
            var ratio = color.ContrastRatio(against);
            return ratio >= minimum ? ($"{ratio:0.0}:1", true) : ($"{ratio:0.0}:1 · adjusted", false);
        }
    }
}
