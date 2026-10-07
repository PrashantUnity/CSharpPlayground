using System;
using System.Collections.Generic;
using System.Linq;
using PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.ColorMath;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Palette;

/// <summary>A palette worked out from a <see cref="PaletteSpec"/>: one key colour per section and a scale per core role.</summary>
public sealed class GeneratedPalette
{
    internal GeneratedPalette(
        PaletteSpec spec,
        IColorEngine engine,
        IReadOnlyDictionary<string, ColorRgb> keys,
        IReadOnlyDictionary<string, PerceptualColor> parts,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, ColorRgb>> scales,
        ColorRgb background,
        ColorRgb surface,
        ColorRgb editorBackground)
    {
        Spec = spec;
        Engine = engine;
        Keys = keys;
        Parts = parts;
        Scales = scales;
        Background = background;
        Surface = surface;
        EditorBackground = editorBackground;
    }

    public PaletteSpec Spec { get; }

    public IColorEngine Engine { get; }

    /// <summary>Every section's colour, by section id (see <see cref="PaletteSections"/>).</summary>
    public IReadOnlyDictionary<string, ColorRgb> Keys { get; }

    /// <summary>Each section's hue, chroma and tone in the engine (the chroma asked for, before sRGB lowered it).</summary>
    public IReadOnlyDictionary<string, PerceptualColor> Parts { get; }

    /// <summary>The 11-stop scale (50..950) of each core role.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, ColorRgb>> Scales { get; }

    /// <summary>The window background the palette was solved against.</summary>
    public ColorRgb Background { get; }

    /// <summary>The card and panel surface accents and chart series were solved against.</summary>
    public ColorRgb Surface { get; }

    /// <summary>The code editor's background the syntax colours were solved against.</summary>
    public ColorRgb EditorBackground { get; }

    public ColorRgb this[string sectionId] => Keys[sectionId];
}

/// <summary>
/// Builds palettes the way Coolors and Material Theme Builder do, on a perceptual engine (OKLCH or HCT):
/// <list type="bullet">
/// <item>Harmony places primary, secondary and tertiary on the engine's hue circle; a locked or hand-set chromatic
/// section becomes the anchor the others are placed around.</item>
/// <item>Neutrals are the primary hue at low chroma; success, warning, error and info keep their meaning (green, amber,
/// red, blue) and lean slightly towards the brand.</item>
/// <item>Syntax colours are read on the editor background (text at least 4.5:1, comments 3:1) and kept apart from each
/// other; chart series reach 3:1 on the surface and are ordered so that the first few stay distinct with colour-vision
/// deficiencies.</item>
/// </list>
/// Pure and deterministic: the same spec gives the same palette, in a few milliseconds.
/// </summary>
public static class PaletteGenerator
{
    // Lightness targets in CIE L* (converted to each engine's tone).
    private readonly record struct Scheme(double Background, double Surface, double Editor, double Accent, double Syntax, double Comment, double Punctuation, double Chart);

    private static readonly Scheme DarkScheme = new(Background: 6, Surface: 10, Editor: 8, Accent: 68, Syntax: 76, Comment: 52, Punctuation: 70, Chart: 66);
    private static readonly Scheme LightScheme = new(Background: 99, Surface: 97, Editor: 100, Accent: 45, Syntax: 42, Comment: 52, Punctuation: 36, Chart: 54);

    // The colours status roles mean, before leaning towards the brand.
    private static readonly IReadOnlyDictionary<string, (string Hex, double ChromaFactor)> StatusReference = new Dictionary<string, (string, double)>
    {
        [PaletteSections.Success] = ("#2DA44E", 0.95),
        [PaletteSections.Warning] = ("#D4A72C", 1.0),
        [PaletteSections.Error] = ("#E5534B", 1.0),
        [PaletteSections.Info] = ("#4493F8", 0.9),
    };

    private const double MaxSemanticShift = 12;
    private const double SyntaxMinDistance = 0.075;
    private static readonly VisionDeficiency[] ChartVisions = [VisionDeficiency.Normal, VisionDeficiency.Deuteranopia, VisionDeficiency.Protanopia];

    public static GeneratedPalette Generate(PaletteSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var engine = ColorEngines.Get(spec.Engine);
        var scheme = spec.IsDark ? DarkScheme : LightScheme;
        var vivid = engine.VividChroma * Math.Clamp(spec.ChromaBoost, 0, 2.5);
        var keys = new Dictionary<string, ColorRgb>(StringComparer.Ordinal);
        var parts = new Dictionary<string, PerceptualColor>(StringComparer.Ordinal);

        // Hues of the chromatic roles, around the anchor.
        var primaryHue = PrimaryHarmonyHue(spec, engine);
        var (secondaryOffset, tertiaryOffset) = Offsets(spec.Harmony);
        var mono = spec.Harmony == ColorHarmonyMode.Monochromatic;
        var harmonyHues = new Dictionary<string, double>
        {
            [PaletteSections.Primary] = primaryHue,
            [PaletteSections.Secondary] = engine.RotateHue(primaryHue, secondaryOffset),
            [PaletteSections.Tertiary] = engine.RotateHue(primaryHue, tertiaryOffset),
        };
        var chromaFactors = new Dictionary<string, double>
        {
            [PaletteSections.Primary] = 1.0,
            [PaletteSections.Secondary] = mono ? 0.45 : 0.75,
            [PaletteSections.Tertiary] = mono ? 0.25 : 0.85,
        };

        var neutralChroma = engine.VividChroma * Math.Clamp(spec.NeutralTint, 0, 35) / 100;
        var neutral = Resolve(PaletteSections.Neutral, primaryHue, neutralChroma, 50, jitterHue: 0);
        var neutralVariant = Resolve(PaletteSections.NeutralVariant, primaryHue, neutralChroma * 2, 50, jitterHue: 0);

        var background = engine.Compose(neutral.Hue, neutral.Chroma, engine.ToneFromLstar(scheme.Background));
        var surface = engine.Compose(neutral.Hue, neutral.Chroma, engine.ToneFromLstar(scheme.Surface));
        var editor = engine.Compose(neutral.Hue, neutral.Chroma, engine.ToneFromLstar(scheme.Editor));
        var accentTone = engine.ToneFromLstar(scheme.Accent);

        foreach (var role in PaletteSections.Chromatic)
        {
            ResolveAccent(role, harmonyHues[role], vivid * chromaFactors[role], jitterHue: 10);
        }

        var statusHues = new Dictionary<string, double>();
        foreach (var (role, (hex, chromaFactor)) in StatusReference)
        {
            var reference = engine.Decompose(ColorRgb.FromHex(hex)).Hue;
            var hue = engine.RotateHue(reference, Math.Clamp(SignedAngle(reference, primaryHue) * spec.SemanticPull / 100 * 0.25, -MaxSemanticShift, MaxSemanticShift));
            statusHues[role] = hue;
            ResolveAccent(role, hue, vivid * chromaFactor, jitterHue: 6);
        }

        var scales = new Dictionary<string, IReadOnlyDictionary<int, ColorRgb>>(StringComparer.Ordinal);
        foreach (var role in PaletteSections.Core) scales[role] = engine.TonalScale(parts[role].Hue, parts[role].Chroma);

        GenerateSyntax();
        GenerateCharts();

        return new GeneratedPalette(spec, engine, keys, parts, scales, background, surface, editor);

        // A core section: its fixed colour, or one made of the given hue and chroma (varied when its source is Random).
        PerceptualColor Resolve(string id, double hue, double chroma, double toneLstar, double jitterHue)
        {
            var section = spec.Section(id);
            if (TryParse(section.KeyColor, out var fixedColor))
            {
                keys[id] = fixedColor;
                return parts[id] = engine.Decompose(fixedColor);
            }

            if (section.Source == SectionSource.Random)
            {
                hue = engine.RotateHue(hue, (Unit(spec.Seed, id, 1) * 2 - 1) * jitterHue);
                chroma *= 1 + (Unit(spec.Seed, id, 2) * 2 - 1) * 0.15;
            }

            var tone = engine.ToneFromLstar(toneLstar);
            keys[id] = engine.Compose(hue, chroma, tone);
            return parts[id] = new PerceptualColor(hue, chroma, tone);
        }

        // An accent: generated ones are readable on the surface at the scheme's accent lightness.
        void ResolveAccent(string id, double hue, double chroma, double jitterHue)
        {
            var section = spec.Section(id);
            if (TryParse(section.KeyColor, out _))
            {
                Resolve(id, hue, chroma, scheme.Accent, jitterHue);
                return;
            }

            var request = Resolve(id, hue, chroma, scheme.Accent, jitterHue);
            var solved = ContrastSolver.SolveTone(engine, request.Hue, request.Chroma, accentTone, surface, (float)spec.ContrastTarget, 45f);
            keys[id] = solved.Color;
            parts[id] = request with { Tone = solved.Tone };
        }

        // Locked colours are fixed points the others keep away from. A colour that was set but not locked (one section
        // regenerated on its own) is placed as if it had been generated and then shown as set, so changing it never moves
        // the others.
        void GenerateSyntax()
        {
            var placed = new List<Oklab>();
            foreach (var role in PaletteSections.SyntaxRoles)
            {
                var id = PaletteSections.SyntaxSection(role);
                var section = spec.Section(id);
                if (section.Locked && TryParse(section.KeyColor, out var fixedColor))
                {
                    keys[id] = fixedColor;
                    parts[id] = engine.Decompose(fixedColor);
                    if (role is not ("Comment" or "Punctuation")) placed.Add(Oklab.FromRgb(fixedColor));
                }
            }

            foreach (var role in PaletteSections.SyntaxRoles)
            {
                var id = PaletteSections.SyntaxSection(role);
                if (keys.ContainsKey(id)) continue;
                var (hue, chromaFactor, toneLstar, minRatio, chromatic) = role switch
                {
                    "Keyword" => (harmonyHues[PaletteSections.Primary], 0.85, scheme.Syntax, 4.5, true),
                    "Type" => (harmonyHues[PaletteSections.Tertiary], 0.8, scheme.Syntax, 4.5, true),
                    "Function" => (harmonyHues[PaletteSections.Secondary], 0.8, scheme.Syntax, 4.5, true),
                    "String" => (statusHues[PaletteSections.Success], 0.8, scheme.Syntax, 4.5, true),
                    "Number" => (statusHues[PaletteSections.Warning], 0.85, scheme.Syntax, 4.5, true),
                    "Preprocessor" => (engine.RotateHue(harmonyHues[PaletteSections.Primary], -35), 0.55, scheme.Syntax, 4.5, true),
                    "Variable" => (engine.RotateHue(harmonyHues[PaletteSections.Secondary], 25), 0.4, scheme.Syntax + (spec.IsDark ? 6 : -4), 4.5, true),
                    "Comment" => (neutral.Hue, 0, scheme.Comment, 3.0, false),
                    _ => (neutral.Hue, 0, scheme.Punctuation, 4.5, false),
                };
                var chroma = chromatic ? vivid * chromaFactor : Math.Max(neutral.Chroma * 1.6, engine.VividChroma * 0.03);
                if (spec.Section(id).Source == SectionSource.Random) hue = engine.RotateHue(hue, (Unit(spec.Seed, id, 1) * 2 - 1) * 25);
                var tone = engine.ToneFromLstar(toneLstar);

                // Keep each colour apart from the ones already placed: turn the hue until it is (or use the most distinct).
                ColorRgb best = default;
                double bestHue = hue, bestTone = tone, bestDistance = -1;
                for (var attempt = 0; attempt < (chromatic ? 16 : 1); attempt++)
                {
                    var tryHue = engine.RotateHue(hue, attempt * 23);
                    var solved = ContrastSolver.SolveTone(engine, tryHue, chroma, tone, editor, (float)Math.Max(minRatio, chromatic ? Math.Min(spec.ContrastTarget, 7) : minRatio));
                    var lab = Oklab.FromRgb(solved.Color);
                    var distance = placed.Count == 0 ? double.MaxValue : placed.Min(p => p.DeltaE(lab));
                    if (distance > bestDistance)
                    {
                        (best, bestHue, bestTone, bestDistance) = (solved.Color, tryHue, solved.Tone, distance);
                    }

                    if (distance >= SyntaxMinDistance) break;
                }

                if (chromatic) placed.Add(Oklab.FromRgb(best));
                if (TryParse(spec.Section(id).KeyColor, out var setColor))
                {
                    keys[id] = setColor;
                    parts[id] = engine.Decompose(setColor);
                }
                else
                {
                    keys[id] = best;
                    parts[id] = new PerceptualColor(bestHue, chroma, bestTone);
                }
            }
        }

        void GenerateCharts()
        {
            var baseTone = scheme.Chart;
            var regenerated = PaletteSections.Charts.Any(id => spec.Section(id) is { Source: SectionSource.Random, KeyColor: null });
            var wheelShift = regenerated ? Unit(spec.Seed, "Chart", 1) * 15 : 0;
            var candidates = new List<(ColorRgb Color, double Hue, double Tone, Oklab[] Seen)>();
            foreach (var toneOffset in new[] { -7.0, 7.0 })
            {
                for (var step = 0; step < 24; step++)
                {
                    var hue = engine.RotateHue(primaryHue, step * 15 + wheelShift);
                    var solved = ContrastSolver.SolveTone(engine, hue, vivid * 1.05, engine.ToneFromLstar(baseTone + toneOffset), surface, 3f);
                    candidates.Add((solved.Color, hue, solved.Tone, Seen(solved.Color)));
                }
            }

            var chosen = new List<Oklab[]>();
            var open = new List<int>();
            for (var series = 1; series <= PaletteSections.ChartSeriesCount; series++)
            {
                var id = PaletteSections.Chart(series);
                if (spec.Section(id).Locked && TryParse(spec.Section(id).KeyColor, out var fixedColor))
                {
                    keys[id] = fixedColor;
                    parts[id] = engine.Decompose(fixedColor);
                    chosen.Add(Seen(fixedColor));
                }
                else
                {
                    open.Add(series);
                }
            }

            foreach (var series in open)
            {
                // The first series is the brand colour; each next one the candidate farthest from those already used.
                var pick = chosen.Count == 0
                    ? 0
                    : Enumerable.Range(0, candidates.Count).MaxBy(i => chosen.Min(c => Distance(c, candidates[i].Seen)));
                var candidate = candidates[pick];
                var id = PaletteSections.Chart(series);
                if (TryParse(spec.Section(id).KeyColor, out var setColor))
                {
                    keys[id] = setColor;
                    parts[id] = engine.Decompose(setColor);
                }
                else
                {
                    keys[id] = candidate.Color;
                    parts[id] = new PerceptualColor(candidate.Hue, vivid * 1.05, candidate.Tone);
                }

                chosen.Add(candidate.Seen);
                candidates.RemoveAt(pick);
            }
        }
    }

    /// <summary>A new palette: every unlocked section is regenerated (with some variety); locked ones never change.</summary>
    public static PaletteSpec Regenerate(PaletteSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var seed = NextSeed(spec.Seed);
        var sections = new Dictionary<string, SectionSpec>(StringComparer.Ordinal);
        foreach (var id in PaletteSections.All)
        {
            var section = spec.Section(id);
            sections[id] = section.Locked ? section : new SectionSpec { Source = SectionSource.Random };
        }

        var next = spec with { Seed = seed, Sections = sections };
        var anchored = PaletteSections.Chromatic.Any(id => IsAnchor(next.Section(id)));
        if (!anchored) next = next with { BaseHue = Unit(seed, "BaseHue", 0) * 360 };
        return Normalized(next);
    }

    /// <summary>A new colour for one section only (unlocked, so the next Generate may change it again).</summary>
    public static PaletteSpec RegenerateSection(PaletteSpec spec, string sectionId)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (!PaletteSections.IsKnown(sectionId)) throw new ArgumentException($"Unknown palette section '{sectionId}'.", nameof(sectionId));
        var engine = ColorEngines.Get(spec.Engine);
        var current = Generate(spec);
        var now = current.Parts[sectionId];
        var was = current.Keys[sectionId];
        var salt = (ulong)Fnv(was.ToHex());
        var against = sectionId.StartsWith(PaletteSections.SyntaxPrefix, StringComparison.Ordinal) ? current.EditorBackground : current.Surface;
        var minRatio = sectionId.StartsWith(PaletteSections.ChartPrefix, StringComparison.Ordinal) || sectionId == PaletteSections.SyntaxSection("Comment")
            ? 3f
            : sectionId is PaletteSections.Neutral or PaletteSections.NeutralVariant ? 1f : (float)spec.ContrastTarget;

        var color = was;
        for (var attempt = 0UL; attempt < 8; attempt++)
        {
            var r1 = Unit(spec.Seed, sectionId, salt + attempt * 2);
            var r2 = Unit(spec.Seed, sectionId, salt + attempt * 2 + 1);
            double hue, chroma = now.Chroma, tone = now.Tone;
            if (StatusReference.ContainsKey(sectionId))
            {
                // Still green, amber, red or blue: a different shade of it.
                hue = engine.RotateHue(now.Hue, (r1 < 0.5 ? -1 : 1) * (6 + r2 * 10));
                chroma = now.Chroma * (0.8 + r2 * 0.35);
            }
            else if (sectionId is PaletteSections.Neutral or PaletteSections.NeutralVariant)
            {
                hue = r1 * 360;
                chroma = engine.VividChroma * (0.02 + r2 * 0.18);
            }
            else
            {
                // Clearly different: somewhere in the other two thirds of the circle.
                hue = engine.RotateHue(now.Hue, 60 + r1 * 240);
                chroma = now.Chroma * (0.85 + r2 * 0.3);
            }

            color = ContrastSolver.SolveTone(engine, hue, chroma, tone, against, minRatio).Color;
            if (Oklab.FromRgb(color).DeltaE(Oklab.FromRgb(was)) >= Oklch.JustNoticeableDifference) break;
        }

        // Unlocked and keeping its source, so the other sections are generated exactly as before.
        var section = spec.Section(sectionId);
        return spec.WithSection(sectionId, new SectionSpec
        {
            KeyColor = color.ToHex(),
            Locked = false,
            Source = section.Source == SectionSource.Manual ? SectionSource.Random : section.Source,
        });
    }

    /// <summary>Sets one section to <paramref name="color"/> and locks it there.</summary>
    public static PaletteSpec SetSection(PaletteSpec spec, string sectionId, ColorRgb color)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return Normalized(spec.WithSection(sectionId, new SectionSpec { KeyColor = color.ToHex(), Locked = true, Source = SectionSource.Manual }));
    }

    /// <summary>Locks a section at the colour it has in <paramref name="palette"/>, or unlocks it (keeping its colour).</summary>
    public static PaletteSpec SetLocked(PaletteSpec spec, string sectionId, bool locked, GeneratedPalette palette)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(palette);
        var section = spec.Section(sectionId);
        var key = section.KeyColor ?? palette.Keys[sectionId].ToHex();
        return Normalized(spec.WithSection(sectionId, section with { KeyColor = key, Locked = locked }));
    }

    /// <summary>Turns the base hue (when nothing chromatic is locked) and lets harmony place the rest again.</summary>
    public static PaletteSpec WithBaseHue(PaletteSpec spec, double hue) => spec with { BaseHue = ColorEngines.SanitizeHue(hue) };

    // ── Harmony ────────────────────────────────────────────────────────────────

    private static (double Secondary, double Tertiary) Offsets(ColorHarmonyMode mode) => mode switch
    {
        ColorHarmonyMode.Analogous => (30, -30),
        ColorHarmonyMode.Complementary => (180, 30),
        ColorHarmonyMode.SplitComplementary => (150, 210),
        ColorHarmonyMode.Triadic => (120, 240),
        ColorHarmonyMode.Tetradic => (90, 180),
        _ => (0, 0),
    };

    // A chromatic section fixes the harmony when the user locked it or set it by hand.
    private static bool IsAnchor(SectionSpec section) =>
        section.KeyColor != null && (section.Locked || section.Source == SectionSource.Manual);

    /// <summary>The primary hue harmony is built around: from the first anchored chromatic section, else the base hue.</summary>
    public static double PrimaryHarmonyHue(PaletteSpec spec, IColorEngine engine)
    {
        var (secondaryOffset, tertiaryOffset) = Offsets(spec.Harmony);
        foreach (var id in PaletteSections.Chromatic)
        {
            var section = spec.Section(id);
            if (!IsAnchor(section) || !TryParse(section.KeyColor, out var color)) continue;
            var hue = engine.Decompose(color).Hue;
            var offset = id == PaletteSections.Secondary ? secondaryOffset : id == PaletteSections.Tertiary ? tertiaryOffset : 0;
            return engine.RotateHue(hue, -offset);
        }

        return ColorEngines.SanitizeHue(spec.BaseHue);
    }

    // The base hue follows the anchor, so unlocking a section later doesn't move the others.
    private static PaletteSpec Normalized(PaletteSpec spec) =>
        spec with { BaseHue = PrimaryHarmonyHue(spec, ColorEngines.Get(spec.Engine)) };

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static double SignedAngle(double from, double to)
    {
        var d = (to - from) % 360;
        if (d > 180) d -= 360;
        if (d < -180) d += 360;
        return d;
    }

    private static bool TryParse(string? hex, out ColorRgb color) => ColorRgb.TryParseHex(hex, out color);

    private static Oklab[] Seen(ColorRgb color)
    {
        var seen = new Oklab[ChartVisions.Length];
        for (var i = 0; i < ChartVisions.Length; i++) seen[i] = Oklab.FromRgb(ColorBlindnessEngine.Simulate(color, ChartVisions[i]));
        return seen;
    }

    // How far apart two colours look to the person who tells them apart least well.
    private static double Distance(Oklab[] a, Oklab[] b)
    {
        var min = double.MaxValue;
        for (var i = 0; i < a.Length; i++) min = Math.Min(min, a[i].DeltaE(b[i]));
        return min;
    }

    /// <summary>The smallest distance between any two of the chart series, as people with each vision see them.</summary>
    public static double MinimumChartDistance(GeneratedPalette palette)
    {
        var seen = PaletteSections.Charts.Select(id => Seen(palette.Keys[id])).ToList();
        var min = double.MaxValue;
        for (var i = 0; i < seen.Count; i++)
        for (var j = i + 1; j < seen.Count; j++)
        {
            min = Math.Min(min, Distance(seen[i], seen[j]));
        }

        return min;
    }

    private static int NextSeed(int seed) => (int)(SplitMix((ulong)(uint)seed + 0x9E3779B97F4A7C15UL) >> 33);

    // Deterministic per-section randomness in [0, 1): independent of other sections, the same in every process.
    private static double Unit(int seed, string id, ulong salt)
    {
        var mixed = SplitMix((ulong)(uint)seed ^ ((ulong)Fnv(id) << 32) ^ (salt * 0xD1B54A32D192ED03UL));
        return (mixed >> 11) * (1.0 / (1UL << 53));
    }

    private static ulong SplitMix(ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    private static uint Fnv(string text)
    {
        var hash = 2166136261u;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return hash;
    }
}
