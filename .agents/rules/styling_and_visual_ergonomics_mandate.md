# Strict Styling, Card Surfaces & Visual Ergonomics Mandate

This rule defines the strict visual, ergonomic, and styling standards for **C# Code Studio** (`com.frypdf.plugin.csharpeditor`), FrySharp, Documentation views, and all related controls. All AI agents and contributors must strictly comply with these rules.

---

## 1. Eye Comfort & Anti-Glare Card Standards

### ❌ Strict Prohibitions
- **NO Jarring Saturated or Electric Accent Borders on Content**: Never wrap content bodies, callouts, alerts, summaries, notes, or tips in high-contrast saturated accent borders (e.g. `BorderBrush="{DynamicResource DsPrimaryBrush}"` with 2px or 3px thickness) or glowing tinted backgrounds (`Background="{DynamicResource DsPrimarySubtleBrush}"`). In dark mode, saturated blues and bright accent lines cause severe visual fatigue, chromatic aberration, and eye strain.
- **NO Hardcoded Hex Colors**: Never author hardcoded hex colors (`#007ACC`, `#161b22`, `#ffffff`) in views or control styles. Always bind to semantic tokens.

### ✅ Canonical Dark Card Formula
All cards, content summaries, callout boxes, notes, and auxiliary information panels must strictly adopt the uniform dark card surface design established by the studio and documentation panels:

```xaml
<!-- Canonical Studio / Docs Card Specification -->
<Border Background="{DynamicResource DsSurfaceBrush}"
        BorderBrush="{DynamicResource DsBorderBrush}"
        BorderThickness="1"
        CornerRadius="10"
        Padding="16,14">
    <!-- Card Content -->
</Border>
```

- **Background**: Soft dark surface (`{DynamicResource DsSurfaceBrush}`). Use `{DynamicResource DsSurfaceHighBrush}` only for nested or elevated child cards.
- **Border**: Subtle low-contrast 1px border (`{DynamicResource DsBorderBrush}`).
- **BorderThickness**: `1` uniformly (never unbalanced thick left borders like `3,1,1,1` unless specifically styled as a standard code snippet gutter).
- **CornerRadius**: `10` for standard cards, `8` for compact sub-cards.
- **Typography**: Text inside cards must use soft, readable `{DynamicResource DsTextBrush}` (never blinding pure white for large paragraphs), with titles and headers using `{DynamicResource DsTextWhiteBrush}`.

### Optional Card Header Pattern
If a card includes a category, badge, or section header, mirror the right-side studio card design:
```xaml
<StackPanel Spacing="8">
    <StackPanel Orientation="Horizontal" Spacing="6" VerticalAlignment="Center">
        <materialIcons:MaterialIcon Kind="InformationOutline" Width="14" Height="14" Foreground="{DynamicResource DsMutedBrush}" VerticalAlignment="Center" />
        <TextBlock Text="OVERVIEW" FontSize="10" FontWeight="Bold" LetterSpacing="1.2" Foreground="{DynamicResource DsMutedBrush}" VerticalAlignment="Center" />
    </StackPanel>
    <Rectangle Height="1" Fill="{DynamicResource DsBorderBrush}" />
    <TextBlock Text="..." FontSize="13" Foreground="{DynamicResource DsTextBrush}" TextWrapping="Wrap" LineHeight="22" />
</StackPanel>
```

---

## 2. Pixel-Perfect Alignment & Vertical Centering

### ❌ The "Stretched TextBlock" Misalignment Bug
In Avalonia and WPF horizontal containers (`StackPanel Orientation="Horizontal"`, `Grid ColumnDefinitions="..."`, toolbars, and tab strips), child controls default to `VerticalAlignment="Stretch"`.
- When a `TextBlock` stretches across a container, Avalonia's text layout engine draws glyphs starting from the top boundary of its line box (local y=0).
- This causes button labels (such as "Run" or "Debug") to clip against top rounded corners and float several pixels higher than neighboring fixed-height icons (`MaterialIcon Width="13" Height="13"`), creating severe visual misalignment.

### ✅ Mandatory Vertical Alignment Rules
Inside any button, toolbar, or strip containing icons and text:
1. **Always explicitly set `VerticalAlignment="Center"`** on:
   - The containing `StackPanel` (`VerticalAlignment="Center"`)
   - The icon (`<materialIcons:MaterialIcon ... VerticalAlignment="Center" />`)
   - The text block (`<TextBlock ... VerticalAlignment="Center" />`)
2. **Button Content Alignment**:
   - Button styles (`cs-toolbar-filled`, `cs-toolbar-tonal`, `cs-toolbar-outlined`) must include:
     ```xaml
     <Setter Property="HorizontalContentAlignment" Value="Center" />
     <Setter Property="VerticalContentAlignment" Value="Center" />
     ```
3. **Decorative Dividers**:
   - Always center separator lines: `<Rectangle Width="1" Height="16" Fill="{DynamicResource DsBorderBrush}" Margin="2,0" VerticalAlignment="Center" />`.
4. **Zero Min Dimensions**:
   - Toolbar button styles must specify `<Setter Property="MinHeight" Value="0" />` and `<Setter Property="MinWidth" Value="0" />` so that Avalonia Fluent Theme's default `ButtonMinHeight` (32px) does not override compact toolbar heights (`Height="26"` or `Height="28"`).

---

## 3. Multi-Control Class Selectors & Template State Coverage

### ❌ The `ToggleButton` Class Fallback Trap
- In Avalonia, `ToggleButton` derives from `ButtonBase`, NOT `Button`.
- A style selector defined as `Button.cs-toolbar-icon` does **NOT** apply to `<ToggleButton Classes="cs-toolbar-icon" />`.
- When a `ToggleButton` does not match the selector, Avalonia falls back to the default Fluent theme, rendering an oversized (32px+), bulky, opaque gray box with thick padding.

### ✅ Multi-Control Selector Mandate
Every shared button class must explicitly target both `Button` and `ToggleButton`:
```xaml
<Style Selector="Button.cs-toolbar-icon, ToggleButton.cs-toolbar-icon">
    ...
</Style>
```

### ✅ Complete Template `ContentPresenter` Overrides
Avalonia Fluent Theme applies background and border brushes directly to `/template/ ContentPresenter#PART_ContentPresenter` for pseudo-classes (`:pointerover`, `:pressed`, `:checked`). Styles that customize backgrounds and borders MUST target the template presenter:

```xaml
<!-- Base Template Presenter -->
<Style Selector="Button.cs-toolbar-icon /template/ ContentPresenter#PART_ContentPresenter,
                 ToggleButton.cs-toolbar-icon /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
    <Setter Property="CornerRadius" Value="4" />
</Style>

<!-- Hover State -->
<Style Selector="Button.cs-toolbar-icon:pointerover /template/ ContentPresenter#PART_ContentPresenter,
                 ToggleButton.cs-toolbar-icon:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource DsSurfaceHoverBrush}" />
</Style>

<!-- Pressed State -->
<Style Selector="Button.cs-toolbar-icon:pressed /template/ ContentPresenter#PART_ContentPresenter,
                 ToggleButton.cs-toolbar-icon:pressed /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource DsSurfaceHighBrush}" />
</Style>

<!-- Checked State (Toggle) -->
<Style Selector="ToggleButton.cs-toolbar-icon:checked /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="{DynamicResource DsSurfaceHighBrush}" />
</Style>
<Style Selector="ToggleButton.cs-toolbar-icon:checked materialIcons|MaterialIcon">
    <Setter Property="Foreground" Value="{DynamicResource DsPrimaryBrush}" />
</Style>
```

---

## 4. Semantic Token Palette Reference

| Token Key | Purpose / Role |
| :--- | :--- |
| `DsBgBrush` | Deepest workspace/editor background |
| `DsSurfaceBrush` | Standard cards, sidebars, tool decks, tabs |
| `DsSurfaceHoverBrush` | Hover state for rows, cards, and buttons |
| `DsSurfaceHighBrush` | Elevated active items, popovers, pressed states |
| `DsBorderBrush` | Subtle, non-glaring 1px border for cards and panels |
| `DsBorderSubtleBrush` | Ultra-subtle dividers and inactive borders |
| `DsTextWhiteBrush` | High-emphasis headers, active tab labels |
| `DsTextBrush` | Primary body text, readable off-white |
| `DsMutedBrush` | Secondary subtitles, timestamps, line numbers |
| `DsPrimaryBrush` | Accent indicator line, active icon, checked icon |
| `DsGreenBrush` | Success, run button, passing test badge |
| `DsErrorBrush` | Errors, stop button, failing test badge |

---

## 5. Global ToolTip Styling & Anti-Collision Placement Mandate

### ❌ The Default Avalonia Pointer Collision Trap
- By default in Avalonia, `ToolTip.Placement` is set to `Pointer` with `VerticalOffset="20"`.
- When the user points at a toolbar button or status pill, the tooltip pops up directly beneath the mouse pointer hot-spot.
- This creates severe usability flaws:
  1. The tooltip covers the exact button, icon, or label the user is interacting with.
  2. Any minor mouse jitter causes cursor collision, triggering erratic `PointerLeave` / `PointerEnter` loops and visual flicker.
  3. Default Avalonia tooltips lack elevation, shadow, and rounded studio aesthetics.

### ✅ Smart Non-Colliding Placement Rules
All studio controls must adhere to ergonomic non-colliding placement coordinates:
1. **General Controls & Toolbars (Default)**:
   - Configured globally on `:is(Control)`:
     ```xaml
     <Setter Property="ToolTip.Placement" Value="Bottom" />
     <Setter Property="ToolTip.VerticalOffset" Value="6" />
     <Setter Property="ToolTip.ShowDelay" Value="350" />
     ```
2. **Zone 1 Activity Bar (Vertical Left Rail)**:
   - Tooltips must extend to the **right** of the vertical rail:
     ```xaml
     <Setter Property="ToolTip.Placement" Value="Right" />
     <Setter Property="ToolTip.HorizontalOffset" Value="8" />
     ```
3. **Zone 5 Status Bar (Bottom Edge)**:
   - Tooltips must pop up **above** the status bar:
     ```xaml
     <Setter Property="ToolTip.Placement" Value="Top" />
     <Setter Property="ToolTip.VerticalOffset" Value="-4" />
     ```

### ✅ Studio ToolTip Design Token Specification
Tooltips must use the canonical elevated floating surface styling:
- **Background**: `{DynamicResource DsSurfaceHighBrush}`
- **BorderBrush**: `{DynamicResource DsBorderBrush}` (1px uniform)
- **CornerRadius**: `6`
- **Padding**: `8,4`
- **Elevation Shadow**: `BoxShadow="{DynamicResource DsFloatingShadow}"`
- **Typography**: `FontSize="11.5"`, `Foreground="{DynamicResource DsTextWhiteBrush}"`, `LineHeight="16"`, `TextWrapping="Wrap"`
- **Subtle Micro-Animation**: `DoubleTransition Property="Opacity" Duration="0:0:0.08"`

