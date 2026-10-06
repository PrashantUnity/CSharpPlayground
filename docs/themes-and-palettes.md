# Themes, palettes and saved preferences

How the studio's colours are made, how a theme reaches the screen, and where everything the user chooses is kept.

## Colour engines

Palettes are built in a perceptual colour model, never in HSL (equal HSL lightness looks very different from hue to hue,
so HSL palettes come out uneven and miss contrast targets). The user picks one of two engines in
Settings → Theme & Colors → Palette sections:

| Engine | Model | Tone | Good at |
| :--- | :--- | :--- | :--- |
| **OKLCH** (default) | OKLab in polar form (Björn Ottosson, 2020; CSS Color 4; Tailwind 4 and Radix palettes) | OKLab L × 100 | Even lightness and hue steps |
| **Material HCT** | CAM16 hue and chroma with CIE L* tone (Material Design 3) | L* | Tone alone predicts contrast: tones 40 apart give ≥ 3:1, 50 apart ≥ 4.5:1 |

Code: `Services/Extensibility/Theming/ColorMath/`
- `Oklab.cs`: `Oklab`, `Oklch` and CSS Color 4 gamut mapping (binary search on chroma, ΔEOK < 0.02), so an
  out-of-gamut request loses chroma, never hue or lightness.
- `MaterialHct.cs`: a port of Google's material-color-utilities (Apache-2.0, header kept): `Hct`, `TonalPalette`,
  CAM16 and the HCT solver. The solver's matrices are worked out from the default viewing conditions rather than
  copied as constants. Its own test vectors pass (`PerceptualColorTests`).
- `ColorEngines.cs`: `IColorEngine` (`Decompose`, `Compose`, `TonalScale` of 11 stops 50..950, `RotateHue`,
  `ToneFromLstar`), with `OklchEngine` and `HctEngine`.
- `ContrastSolver.SolveTone`: the tone nearest a wanted tone that reaches a WCAG 2.2 ratio and an APCA Lc together. It
  moves away from the background, and tries the other side if that side can't reach the target.

## Palettes: sections, locks, Generate

A palette is a `PaletteSpec` (`Theming/Palette/PaletteSpec.cs`), an immutable record that is saved as JSON:
- the engine, harmony mode, base hue, dark or light;
- tuning: chroma boost, neutral tint, how far status colours lean towards the brand (semantic pull), and the contrast
  target (4.5 for AA, 7 for AAA);
- a seed;
- **sections**, each with an optional key colour, a lock and a source (`Harmony`, `Random` or `Manual`):
  - core roles: `Primary`, `Secondary`, `Tertiary`, `Neutral`, `NeutralVariant`, `Success`, `Warning`, `Error`, `Info`;
  - syntax roles: `Syntax.Keyword`, `Type`, `Function`, `String`, `Number`, `Preprocessor`, `Variable`, `Comment`,
    `Punctuation`;
  - chart series: `Chart.1`..`Chart.8`.

`PaletteGenerator` is pure and deterministic: the same spec always gives the same palette. Generating and mapping one
takes about 2 ms.

| Operation | What changes |
| :--- | :--- |
| `Generate(spec)` | Nothing in the spec. Works out the key colour of every section and an 11-stop scale for each core role. |
| `Regenerate(spec)` (Generate, Space) | Every unlocked section, with a little seeded variety. A locked section never changes. |
| `RegenerateSection(spec, id)` (↻ on a section) | That section only. It stays unlocked, and the others are generated exactly as before. |
| `SetSection(spec, id, colour)` (typing a hex) | Sets that section and locks it. |
| `SetLocked(spec, id, locked, palette)` | Locks a section at its current colour; unlocking keeps the colour until the next Generate. |

The rules the generator follows:
- **Harmony** places primary, secondary and tertiary on the engine's hue circle (analogous, complementary, split,
  triadic, tetradic, monochromatic). A locked or hand-set chromatic section becomes the anchor the others are placed
  around.
- **Neutrals** are the primary hue at low chroma. **Status roles** keep their meaning (green, amber, red, blue) and lean
  at most 12° towards the brand.
- **Accents** (primary, status) are solved to reach the contrast target on the card surface, with APCA Lc ≥ 45.
- **Syntax colours** are solved on the editor background: text ≥ 4.5:1, comments ≥ 3:1. They are kept at least 0.05
  ΔEOK apart from each other.
- **Chart series** reach 3:1 on the surface. They are picked farthest-first, measured as the smallest distance seen
  with normal vision, deuteranopia or protanopia, so the first few series stay distinct for colour-blind readers.

`ThemeTokenMapper.Map(palette)` turns a palette into the studio's ~300 tokens:
- surfaces come from the neutral scale at fixed L* (dark: window 6, card 10, editor 8; light: 99, 97, 100);
- text is ≥ 7:1 with APCA Lc ≥ 75, or Lc ≥ 90 at AAA; muted text reaches the target with Lc ≥ 60;
- `DsOnAccentBrush` is white or dark, whichever reads on the primary;
- `Syntax{Role}Brush` and `ChartSeries{1..8}Brush` are new tokens.

`HarmonicColorGenerator` keeps its old API as a facade over all this.

## Syntax and charts follow the theme

- `SyntaxPaletteApplier` maps each language's XSHD colour names to the nine roles by name: `*Comment*` → comment,
  `String`, `Char`, `Escape`, `Regex` → string, `*Keyword*`, `Modifiers`, `Control`, `Storage` → keyword, and so on.
  `ThemedSyntaxAndChartsTests` checks that every named colour of every language has a role.
- When the active theme has `Syntax*` tokens, those colours are applied in place to the shared definitions. A theme
  without them gives every language its own colours back.
- Editors (`BindableTextEditor`, `CodeViewer`, the Code Studio editor) subscribe while attached to `EditorColorsChanged`.
  It is raised once per burst of theme changes, on the UI thread; they re-read their editor brushes and redraw.
- `ChartPaletteService.GetSeriesColor` returns the theme's `ChartSeries{n}Brush`, else the default palette.
- Built-in themes are not given `Syntax*` or `ChartSeries*` tokens when they are completed
  (`HarmonicColorGenerator.EnsureCompleteTheme`).

## Where preferences live

Everything is under the studio's data folder: `%LOCALAPPDATA%/FryPDF/Plugins/com.frypdf.plugin.csharpeditor`, or
`~/Library/Application Support/...` on macOS (`StudioLanguageServices.DefaultBaseDirectory()`).

| File | What | Written by |
| :--- | :--- | :--- |
| `studio_settings.json` | Every setting, plus `Appearance` (below) and `SchemaVersion`. | `StudioSettingsStore` |
| `themes/<id>.frytheme.json` | One saved theme each, ids `user-…`. Holds the name, dates, source (`generated`, `imported` or `duplicate`), the theme's colours, the harmony controls, and the `PaletteSpec` it was made from. | `ThemeLibraryStore` |

`Appearance` holds:
- the active theme id, plus a snapshot of its colours when it is neither built in nor saved (so an unsaved generated or
  imported theme still comes back);
- the light/dark choice;
- density;
- single-token overrides;
- the harmony controls;
- the palette being designed (`PaletteSpec`, with its locks and engine).

How the files are written:
- **Settings save themselves.** There is no Apply button. A change goes through `StudioSettingsStore.Update`
  (read-modify-write, so saving one setting can never reset another) and is written 500 ms after the last change, off
  the UI thread; twenty changes in a row are one write. Closing the studio flushes pending changes
  (`CSharpStudioHostViewModel.FlushSettings`, also run on the Runner window's `Closing`).
- **Writes are atomic** (a temporary file, then a rename). A file that can't be read is kept as
  `studio_settings.corrupt-<time>.json` and the studio starts from defaults.
- **Saved themes**: as many as the user likes, each named when saved (Save as), and renamed, duplicated or deleted from
  its card. Built-in themes can only be duplicated. An imported DTCG theme is saved to the library as well. Applying a
  saved theme that was made in the palette studio opens its palette there again.
- **At start**, `AppearanceRestorer.RestoreFrom` runs in the plugin's `ApplyAsync` and in the Runner's window, before
  any page is built and before the customization script. It reads only the settings file and at most one theme file,
  and applies theme, density and overrides through `DynamicThemeEngine`. The user's `init` script still has the last
  word.

## Measuring

```bash
dotnet tools/UiSnapshots/bin/Debug/net10.0/UiSnapshots.dll perf --theme-switch --theme-only
dotnet tools/UiSnapshots/bin/Debug/net10.0/UiSnapshots.dll settings --category Themes --engine hct --lock Primary,Error --generate 3 --height 1700
dotnet tools/UiSnapshots/bin/Debug/net10.0/UiSnapshots.dll studio 1 --palette oklch:12
```

Budgets:
- palette generate and token map ≤ 10 ms (measured about 2 ms);
- a Generate press, previewed and drawn with every page built, ≤ 30 ms (18 ms median);
- applying a palette as the studio theme ≤ 300 ms;
- a hue-wheel step ≤ 16 ms.
