# Visuals: Charts, 3D Plots, and Algorithm Visualizers

FrySharp provides a unified, polyglot visual runtime for interactive 2D charts, 3D plots, and algorithm/data structure visualizers. Regardless of the language (C#, Python, JavaScript, Java, Go, Rust, C++, or F#), programs generate rich interactive output through canonical APIs, standardized MIME bundles, and a shared visual chrome.

> [!TIP]
> For a full tutorial on generating diagrams (trees, graphs, 2D grids, vector canvas) with multi-language code examples, see [Diagrams & Visualizations API](file:///Users/codefrydev/Desktop/SourceCode/CSharpPlayground/docs/diagrams-and-visualizations.md).

---

## 1. Unified Visual Controls & Chrome

Every visual output is hosted in an interactive control paired with `VisualChromeControl`:

| Visual Control | Target | Default Height | Kinds / Capabilities |
|---|---|---|---|
| `InteractiveChartControl` | 2D Data Series | 260px | Line, Area, Bar, Scatter, Pie, Donut, Histogram |
| `InteractivePlot3DControl` | 3D Spatial Data | 300px | Scatter, Trajectory, Surface, Wireframe, VoxelBar, 3D Graph |
| `InteractiveVisualizerControl` | Algorithms & Data Structures | 200px | Array Pointers, Sorting Bars, Matrix, Islands, Tree, Graph, Linked List, Board, Canvas |

### Standard Toolbar
All visual controls share a consistent toolbar provided by `VisualChromeControl`:
- **Fullscreen / Pop-Out**: Expands the visual across the full window or opens a dedicated pop-out window with independent zoom, pan, and orbit.
- **Copy Spec**: Copies a portable MIME bundle (`application/vnd.fry.*.v1+json`) that can be passed to `Display.show()` in any language.
- **Copy Data (CSV)**: Exports the underlying data (or the currently scrubbed playback step) to standard CSV format.
- **Save PNG**: Renders a crisp vector snapshot of the canvas directly to disk.
- **Reset / Fit to View**: Re-centers the view and resets zoom to 100% or fits the drawing into the current canvas bounds.

### Kind-Specific Controls
- **Charts**: Type switchers (Line, Area, Bar, Scatter, Pie), grid toggle, interactive series legend.
- **3D Plots**: Camera view presets (ISO, TOP, FRT, SIDE), turntable auto-rotation toggle, perspective/orthographic camera toggle, floor grid toggle, wireframe toggle, colormap cycler (Viridis, Plasma, Magma, CoolWarm, Turbo, Rainbow), and Three.js HTML export.
- **Visualizers**: Zoom in/out, actual size, matrix cell values toggle, coordinates toggle, and the step-by-step time-travel playback deck (Play/Pause, Step Prev/Next, speed slider, scrubbing bar).

---

## 2. Decoupled View States & Spec Immutability

Views never mutate the underlying data model or spec:
- `ChartViewState`: Tracks `OverrideType`, `OverrideShowGrid`, `Zoom`, `PanOffsetX`, `PanOffsetY`.
- `Plot3DViewState`: Tracks `Camera` pitch/yaw/zoom, `OverrideShowFloorGrid`, `OverrideShowBoundingBox`, `OverrideWireframe`, `OverrideColorMap`, and `AutoRotate`.
- `VisualizerViewState`: Tracks `Zoom`, `PanOffsetX`, `PanOffsetY`, `OverrideShowValues`, `OverrideShowCoordinates`, and `StayFitted`.

Because view state is decoupled, inline cells and fullscreen/overlay views operate simultaneously without fighting over pan, zoom, or camera angles.

---

## 3. Gestures & Interactions

All visual controls support uniform pointer gestures:
- **Wheel Scroll**: Smooth zoom centered at cursor or canvas center.
- **Drag (Left Button)**: Panning (2D charts & visualizers) or 3D turntable orbiting.
- **Drag (Right Button / Shift+Drag)**: 3D camera pan.
- **Double-Click**: Resets camera, zoom, and pan back to default fit.
- **Hover**: Displays rich formatted tooltips showing coordinates, values, node labels, and metadata.
- **Click**: Dispatches click events with element metadata (`index`, `point`, `nodeId`) back to the running script or kernel callback.

---

## 4. Polyglot Language SDKs

### C#
```csharp
// 2D Chart
Display.Chart(new[] { 10, 25, 40, 15 }, title: "Quarterly Sales");

// 3D Surface
Display.Plot3D((x, y) => Math.Sin(x) * Math.Cos(y), xRange: (-5, 5), yRange: (-5, 5), title: "Wave Surface");

// Data Structure Visualizer
var recorder = new VisualizerRecorder();
recorder.Step(new[] { 5, 2, 8, 1 }, "Initial state");
recorder.Display();
```

Every chart and 3D helper takes the same named settings (`xLabel`, `yLabel`, `zLabel`, `width`, `height`, `legend`), and `Charts` builds the same visual one setting at a time. Both draw the same spec:

```csharp
Display.LineChart(sales, "Sales", xLabel: "Month", yLabel: "USD");

Charts.Line(sales).Title("Sales").XLabel("Month").YLabel("USD").Size(640, 320).Show();
Charts.Surface((x, y) => Math.Sin(x) * Math.Cos(y)).ColorMap(ColorMapPreset.Plasma).Show();
```

`Show()` returns the handle (`Update`, `OnClick`). A builder returned as a notebook cell's last value is shown without `.Show()`; once shown it can't be changed, so change the handle instead.

### Python
```python
from fry_display import Display

# 2D Line Chart
Display.chart(x=[1, 2, 3, 4], y=[10, 20, 15, 30], title="Performance")

# 3D Parametric Surface
Display.plot3d_surface(lambda x, y: x**2 + y**2, x_range=(-3, 3), y_range=(-3, 3))
```

### JavaScript / Node.js
```javascript
const { Display } = require('fry_display');

Display.barChart({ CPU: 45, Memory: 78, Disk: 62 }, 'Resource Allocation');
```

### Java
```java
// In a notebook cell (Display and Visualizer need no import there)
var matrix = List.of(List.of(1, 0), List.of(0, 1));
Visualizer.grid(matrix).title("Identity Matrix").show();
```

### Go
```go
package main

import "fry"

func main() {
    fry.ScatterChart([][]any{{1, 2.5}, {2, 3.7}, {3, 1.8}}, "Point Distribution")
}
```

### Rust
```rust
fn main() {
    fry::scatter_chart(&vec![vec![0.0, 1.0], vec![1.0, 4.0], vec![2.0, 9.0]]).title("Telemetry").show();
}
```

### C++
```cpp
#include <fry/display.hpp>

int main() {
    fry::tree(fry::make_tree(2, fry::make_tree(1), fry::make_tree(3)), "Tree");
    return 0;
}
```

### F#
```fsharp
open Fry

Display.lineChart([ for x in 0.0 .. 0.1 .. 6.28 -> sin x ], "Sine Wave") |> ignore
```

Every sample in the docs' visual articles runs with its real toolchain under the Run button, and `SnippetRunTests.Every*Sample_Runs` keeps them honest.

### More chart kinds and options (every language)

All chart kinds beyond line, bar, scatter, pie and histogram are optional additions to chart v1, so older specs read unchanged. Each language's SDK writes the same spec, checked against `docs/visuals/conformance/chart-*.json` by a real-toolchain test per language.

| Feature | Spec field | Python | JavaScript | Go | F# | Dart |
|---|---|---|---|---|---|---|
| Combo (a series of another kind, on the right axis) | `series[].kind`, `series[].axis`, `y2Axis` | `{"values": v, "kind": "line", "axis": "right"}` + `y2_title=` | series option `kind`/`axis` | `fry.S(v, "kind", "line", "axis", "right")` + `fry.RightAxis(...)` | `Display.series(v).Kind("line").RightAxis()` + `ChartStyle().Y2Axis(...)` | `{'values': v, 'kind': 'line', 'axis': 'right'}` + `'y2Axis'` |
| Stacked, horizontal | `stack`, `orientation` | `stacked_bar_chart`, `horizontal_bar_chart` | `stackedBarChart`, `horizontalBarChart` | `StackedBarChart`, `HorizontalBarChart` | `stackedBarChart`, `horizontalBarChart` | `stackedBarChart`, `horizontalBarChart` |
| Line styling | `dash`, `interpolation`, `tension`, `step`, `fill`, `pointStyle`, `pointRadius`, `colorSegments` | per-series options | per-series options | `fry.S(...)` options | `SeriesData` methods | per-series map keys |
| Bubble, radar, polar area | `kind: bubble \| radar \| polarArea`, `series[].sizes` | `bubble_chart`, `radar_chart`, `polar_area_chart` | `bubbleChart`, `radarChart`, `polarAreaChart` | `BubbleChart`, `RadarChart`, `PolarAreaChart` | `bubbleChart`, `radarChart`, `polarAreaChart` | `bubbleChart`, `radarChart`, `polarAreaChart` |
| Gauge (half donut) | `startAngle`, `sweep` | `gauge=True` | `gauge: true` | `fry.Gauge()` | `ChartStyle().Gauge()` | `'gauge': true` |
| Log, time and reversed scales, suggested range | `xAxis`/`yAxis`: `scale`, `suggestedMin`, `suggestedMax`, `reverse` | `y_scale=`, `y_suggested_min=`, `reverse_x=` | same names in camelCase | `fry.YScale`, `fry.SuggestedY`, `fry.ReverseX` | `ChartStyle().YScale/SuggestedY/ReverseX` | `'yAxis': {...}` |

Java, Rust and C++ follow the same shapes with their own naming (`Display.java`, `lib.rs`, `display.hpp`). The Python tab of the docs and the conformance tests under `Tests/Real*` show the exact calls per language.

---

## 5. Live Updates & Two-Way Events

- **In-Place Updates**: Returned visual handles support `.update(...)`, throttling mutations and refreshing the visual in-place without generating new cells.
- **Event Callbacks**: Clicking visual elements routes event messages back through the transport or kernel pipes (`event` message type with `target`, `element`, `modifiers`).
- **Process Disconnection**: When a program terminates, visual handles are marked disconnected, gracefully preserving the last rendered state while alerting the user.

---

## 6. ECharts in C#: `EChart`

`EChart` draws a chart with Apache ECharts 5.5 (echarts-gl 2.0.9 for 3D) in a web view, in C# only. It has the same
shape as `Charts`: a factory, settings, `Show()`; and it reads data the same way (numbers, `[x, y]` pairs, a
label → number map, a name → sequence map, tuples, records by member name). A chart returned as a cell's last value
shows without `Show()`.

```csharp
EChart.Line(sales, "Sales").Title("Sales").XLabel("Month").YLabel("USD").Smooth().Show();
EChart.Bar(revenue, "Revenue").Series("Profit", profit, EChartType.Line).Zoom().Toolbox().Show();
EChart.Bar(regions, r => r.Region, r => r.Revenue).Horizontal().ValueLabels().Show();
sales.ToEChart(EChartType.Donut).Title("Share").Show();     // any data, any kind
sales.DumpEChart("Sales", EChartType.Bar);                   // shows it, returns the data
```

| Factory | Data |
| --- | --- |
| `Line`, `Area`, `Bar`, `Scatter` | as for `Charts`; records with x and y selectors; `params (name, values)` |
| `Pie`, `Donut`, `Funnel` | label → number, or records with label and value selectors |
| `Radar(data, spokes?, max?)` | a series per name, a value per spoke |
| `Heatmap(grid, xLabels?, yLabels?)` | `double[,]` or rows; a row per y, the first at the top |
| `Candlestick` | `[open, close, low, high]` items, or records with a date and those four |
| `Gauge(value, max)` | one number |
| `Treemap`, `Sunburst` | a map of names to numbers or to the maps inside them, or records with name, value, children |
| `Sankey`, `Graph` | `(from, to, amount)` links, or an adjacency map |
| `Surface(f, xRange, yRange, resolution)`, `Surface(grid)` | z = f(x, y), or heights; NaN is a hole |
| `ParametricSurface(x, y, z, u, v)` | three functions of (u, v): spheres, tori, strips |
| `Bar3D`, `Scatter3D` | a grid, or `(x, y, z)` items; text places are categories (`xLabels`/`yLabels` keep their order) |

Settings: `Title`, `Subtitle`, `XLabel`, `YLabel`, `ZLabel`, `XRange`, `YRange`, `Labels`, `YLabels`, `Legend`,
`LegendAt`, `Tooltip`, `Toolbox`, `Zoom`, `Stacked`, `Horizontal`, `Smooth`, `ValueLabels`, `Colors`, `ColorMap`
(a `ColorMapPreset` or colours), `Wireframe`, `Shading`, `AutoRotate`, `Theme` (`Auto` follows the studio), `Svg`. A
setting a chart can't have says what it is for instead of doing nothing.

### The whole option

Every setting writes into `chart.Option`, the ECharts option itself as a `JsonObject`. What no setting reaches:

```csharp
EChart.Bar(temps)
    .Set("series.label.show", true)                     // a list without an index: every item
    .Set("xAxis.axisLabel.rotate", 30)
    .Set("tooltip.formatter", JsFunc.From("p => p[0].name + ': ' + p[0].value"))
    .Merge("""{ "animationDuration": 300 }""")
    .Configure(o => o["animation"] = true)
    .Show();

EChart.FromJson(json).Title("From JSON").Show();          // bad JSON says where it is wrong
EChart.FromJs("option = { series: [{ type: 'pie', data: [1, 2] }] };").Show(); // an ECharts example, as it is
EChart.FromOption(new { series = new[] { new { type = "pie", data = new[] { 1, 2 } } } }).Show();
```

`JsFunc` is written into the page as code, wherever it is in the option.

### The page

An output carries the option and names the libraries (`<script data-fry-asset="echarts.min.js">`); the HTML view
writes them in when it shows the page (`HtmlAssets.ForDisplay`), so a chart is a few kilobytes in a notebook, not
a megabyte. `chart.ToHtml()` / `chart.SaveHtml(path)` and the view's "open in browser" give a page with the libraries
in it. echarts-gl is only loaded for charts that need it. The view also sets `window.fryHost = { theme, background }`,
which `Theme(EChartTheme.Auto)` follows. An option ECharts refuses shows its error on the page.
