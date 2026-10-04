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

Display.chart({
    type: 'bar',
    title: 'Resource Allocation',
    labels: ['CPU', 'Memory', 'Disk'],
    series: [{ name: 'Usage %', values: [45, 78, 62] }]
});
```

### Java
```java
import com.frypdf.display.Visualizer;

int[][] matrix = { { 1, 0 }, { 0, 1 } };
Visualizer.grid(matrix).title("Identity Matrix").show();
```

### Go
```go
package main
import "fry_display"

func main() {
    fry_display.Chart(fry_display.ChartOptions{
        Type: "scatter",
        Title: "Point Distribution",
        X: []float64{1.0, 2.0, 3.0},
        Y: []float64{2.5, 3.7, 1.8},
    })
}
```

### Rust
```rust
use fry_display::prelude::*;

fn main() {
    let chart = Chart::new()
        .title("Telemetry")
        .line_series("Sensor", vec![(0.0, 1.0), (1.0, 4.0), (2.0, 9.0)]);
    Display::show(chart);
}
```

### C++
```cpp
#include <fry_display.hpp>

int main() {
    fry::Surface3D surface("Saddle", [](double x, double y) { return x*x - y*y; }, -3.0, 3.0, -3.0, 3.0, 30);
    surface.show();
    return 0;
}
```

### F#
```fsharp
open FryDisplay

let data = [ for x in 0.0 .. 0.1 .. 6.28 -> (x, sin x) ]
Display.chart [ "Sine Wave", data ]
```

---

## 5. Live Updates & Two-Way Events

- **In-Place Updates**: Returned visual handles support `.update(...)`, throttling mutations and refreshing the visual in-place without generating new cells.
- **Event Callbacks**: Clicking visual elements routes event messages back through the transport or kernel pipes (`event` message type with `target`, `element`, `modifiers`).
- **Process Disconnection**: When a program terminates, visual handles are marked disconnected, gracefully preserving the last rendered state while alerting the user.
