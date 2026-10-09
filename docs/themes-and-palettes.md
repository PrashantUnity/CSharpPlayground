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

## Syntax colours are VS Code's

- Every editor (`BindableTextEditor`, `CodeViewer`, the Code Studio editor) colours code through
  `SyntaxColoring.Apply(editor, language, isDark, enabled, fileExtension)` (`Controls/Editor/SyntaxColoring.cs`).
- **Grammars:** VS Code's TextMate grammars and themes, from `AvaloniaEdit.TextMate` and `TextMateSharp.Grammars`
  (`Services/Languages/Highlighting/StudioTextMate.cs`).
  - Grammar choice: the language id first (VS Code's ids are the studio's), then the open file's extension, so a `.json`,
    `.md`, `.yaml` or `.sh` file opened as plain text gets its own grammar, then the language's extensions. Plain `.txt`
    gets none.
  - Each grammar is read once for all editors. Tokenizing runs in the background, so a slow grammar (C++) colours
    progressively instead of blocking.
  - The grammars' regex engine is native (Onigwrap, `runtimes/<rid>/native`, packaged with the plugin). Where it
    can't load, every language falls back to its own XSHD rules.
- **Themes:**
  - Dark+, Light+, Dracula, Monokai and One Dark use VS Code's own theme.
  - A theme with `Syntax*` colours (generated themes) gets a TextMate theme made from those nine roles.
  - Anything else gets Dark+ or Light+ by scheme.
  - Theme switches re-theme every open editor (`ThemeChanged`, coalesced).
- **C#:** the grammar can't follow top-level code (scripts and notebook cells), so on top of it a layer colours each word
  by the role Roslyn's syntax tree gives it, as VS Code's C# extension does: types, namespaces, methods, properties,
  locals and parameters, constants and enum members, control and other keywords, strings with escapes, and directives.
  - `CSharpClassifier` (pure; uses the file's own declarations to tell `Items.Count` from `Console.WriteLine`).
  - `CSharpLiveSyntax` reparses incrementally in the background (an edit in a 20,000-line file: well under 500 ms).
    Until then it moves the old roles with their text.
  - `CSharpRoleColorizer` reads the theme's colour for each role.
- **Notebook markdown cells:** edited in the same editor (markdown grammar, wrapped, no code helpers). They open
  rendered; Shift+Enter renders them.
- Tests: `SyntaxColoringTests` holds VS Code's Dark+/Light+ colours per language and the C# roles.

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

## Layout & Typography

Settings → Layout & Typography has levers for:
- **fonts:** the interface font and the code font;
- **type:** a type ramp of 13 sizes that follows the base size and a hierarchy lever, plus weights for body, labels, emphasis and headings, and caps letter spacing;
- **corners:** a radius scale, with overrides for cards, buttons, inputs, tabs, rows, dialogs, chips and tooltips;
- **borders:** border width, card outlines, dividers and the accent bar;
- **spacing:** density and a spacing scale, with overrides for control height, row height, tab height and card padding;
- **shadows:** strength, softness and card elevation.

There are eight built-in presets: Studio, VS Code, Fluent 2, Material 3, macOS, Sharp, Soft and Large text. "My layouts" holds named layouts you save. A layout can be exported as W3C design tokens or as CSS variables, and imported back.

How it applies:
- The preview on the page follows each lever at once.
- The studio follows when the slider is let go, or 300 ms after a key or click. That is one layout change.
- Every change is remembered and can be undone (50 steps).
- A colour theme switch keeps the layout. Saving a theme with "Include current layout" bundles the layout with it.

### The tokens
`LayoutSpec` (`Theming/Layout/LayoutSpec.cs`) is what is saved. `LayoutTokenMapper` turns it into the tokens; at the default layout every token equals the size the studio was drawn with before it had tokens.

| Family | Tokens | Default |
| :--- | :--- | :--- |
| Type ramp | `DsFontSize050` … `DsFontSize800` | 8.5, 9.5, 10, 10.5, 11, 11.5, 12 (body), 12.5, 13, 14, 16, 20, 26 |
| Weights | `DsWeightBody`, `DsWeightLabel`, `DsWeightEmphasis`, `DsWeightStrong` | Normal, Medium, SemiBold, Bold |
| Letter spacing | `DsTracking050` … `DsTracking120` | 0.5 … 1.2 |
| Fonts | `DsUiFontFamily`, `DsCodeFontFamily` | the platform default; Cascadia Code, JetBrains Mono, … |
| Radii | `DsRadiusXS`, `SM`, `MD`, `LG`, `XL`, `2XL`, `3XL`, `4XL`, `Full` | 3, 4, 6, 8, 10, 12, 16, 24, 9999 |
| Radii, one side | the radius tokens plus `Top`, `Bottom`, `Left` or `Right` (e.g. `DsRadiusMDTop`) | |
| Radii, per component | `DsRadiusCard`, `Button`, `Input`, `Tab`, `Row`, `Dialog`, `Chip`, `Tooltip` | 10, 6, 6, 6, 4, 16, 12, 6 |
| Borders | `DsBorderThin`, `Medium`, `Top`, `Bottom`, `Left`, `Right`, `NoBottom`, `AccentLeft`, `DsCardBorder` | 1 px (accent 2) |
| Spacing | `DsSpace2` … `DsSpace32` (named by their default pixels), `DsCardPadding`, `DsControlHeight100…500`, `DsControlHeight`, `DsTabHeight`, `DsRowHeight` | density × spacing scale |
| Shadows | `DsShadowLevel0…4`, `DsCardShadow`, `DsModalShadow`, `DsFloatingShadow`, `M3Elevation*` | Material 3 levels, deeper on dark themes |

The activity bar (48 px) and status bar (22 px) never scale.

`Styles/Tokens/StudioLayoutTokens.axaml` holds the defaults. It is generated, and a test keeps it in step: after changing the mapper, run `UiSnapshots layout-tokens`.

### Using them in a view: keys, not dynamic resources
Controls take layout tokens by **key**:

```xml
<TextBlock lt:Tokens.FontSize="DsFontSize200" lt:Tokens.FontWeight="DsWeightEmphasis" />
<Style Selector="Button.my-button">
    <Setter Property="lt:Tokens.CornerRadius" Value="DsRadiusButton" />
    <Setter Property="lt:Tokens.BorderThickness" Value="=0" />   <!-- a fixed value -->
</Style>
```

The namespace is `xmlns:lt="clr-namespace:PdfEditorApp.Plugins.CSharpEditor.Services.Extensibility.Theming.Layout;assembly=CSharpEditorPlugin"`.

`Tokens` (`Theming/Layout/Tokens.cs`) sets the token's value at the priority the key was set with. A layout change sets it again from a weak registry: ~5,000 controls, 140–260 ms including the re-layout with every page built.

Why not `{DynamicResource}`:
- In shared style setters it held about 140 MB (measured by A/B against literals). Avalonia builds a style per control as soon as a setter is a dynamic resource.
- Every dynamic resource is re-resolved on each colour-theme switch.

With keys, memory and theme switches are the same as with plain literals, and a key works outside the host (tool windows) too.

The rules:
- In shared styles, these properties always go through a key: `FontSize`, `FontWeight`, `FontFamily`, `LetterSpacing`, `CornerRadius`, `BorderThickness` and `BoxShadow`. A value without a token is written `Value="=0,2,1,0"`. Avalonia then decides which style wins on the key; a value set from code can't take part in style ordering.
- Only the page's own preview (`LayoutSpecimenControl`) uses dynamic resources, from its own resources, so it can show values that aren't applied yet.
- Code that builds controls uses `Tokens.SetFontSize(control, "DsFontSize300")`, or `LayoutTokens` for values read once (rendered Markdown and HTML, completion lists, tables), and rebuilds on `DynamicThemeEngine.LayoutChanged`.
- `DesignTokenLintTests` fails on any hard-coded font size, weight, radius, border, code font or direct style setter in a view. `python3 tools/token_migrate.py <files>` converts them. Circles and rings that must stay round are on its short allowlist.

Scripts: `Themes.ApplyLayout(json)`, `Themes.GetLayoutJson()` and `Themes.GetLayoutToken("DsRadiusCard")`. `SetDensity` changes the layout's density.

## Where preferences live

Everything is under the studio's data folder: `%LOCALAPPDATA%/FryPDF/Plugins/com.frypdf.plugin.csharpeditor`, or
`~/Library/Application Support/...` on macOS (`StudioLanguageServices.DefaultBaseDirectory()`).

| File | What | Written by |
| :--- | :--- | :--- |
| `studio_settings.json` | Every setting, plus `Appearance` (below) and `SchemaVersion`. | `StudioSettingsStore` |
| `layouts/<id>.frylayout.json` | One saved layout each, ids `layout-user-…`. | `LayoutLibraryStore` |
| `themes/<id>.frytheme.json` | One saved theme each, ids `user-…`. Holds the name, dates, source (`generated`, `imported` or `duplicate`), the theme's colours, the harmony controls, and the `PaletteSpec` it was made from. | `ThemeLibraryStore` |

`Appearance` holds:
- the active theme id, plus a snapshot of its colours when it is neither built in nor saved (so an unsaved generated or
  imported theme still comes back);
- the light/dark choice;
- density;
- single-token overrides;
- the harmony controls;
- the palette being designed (`PaletteSpec`, with its locks and engine);
- the layout (`LayoutSpec`) and which preset or saved layout it came from. An older file with only a density becomes a layout with that density.

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
- a layout change with every page built ≤ 400 ms (140–260 ms measured);
- a colour-theme switch the same with layout tokens as without.
