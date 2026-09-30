# Visual specs: charts, 3D plots and visualizers as data

Every chart, 3D plot and data-structure visualizer the studio draws is described by a **spec**: a JSON document that says what to draw, not how. A program in any language sends one inside a display bundle, as in [kernel-protocol.md](kernel-protocol.md); C# builds the same spec objects in-process. The studio fills in what is left out (bounds, layout, colours, defaults) and does the work a visual needs (histogram bins, tree and graph layout, finding islands or a list's cycle), so the same spec draws the same picture from every language.

The C# types are the single source of truth: `Visuals/Spec/`. The JSON Schemas in [visuals/schema](visuals/schema) are generated from them (and a test keeps them current), and [Tests/Fixtures/Visuals](../Tests/Fixtures/Visuals) holds a spec of every kind as every language must write it.

## MIME types

| MIME type | Spec | Schema |
|---|---|---|
| `application/vnd.fry.chart.v1+json` | chart | [chart.v1.json](visuals/schema/chart.v1.json) |
| `application/vnd.fry.plot3d.v1+json` | 3D plot | [plot3d.v1.json](visuals/schema/plot3d.v1.json) |
| `application/vnd.fry.visualizer.v1+json` | visualizer | [visualizer.v1.json](visuals/schema/visualizer.v1.json) |

The major version is part of the name (as in `application/vnd.vegalite.v5+json`). The first 3D format, `application/vnd.fry.plot3d+json` with `{title, type, points}`, is still read but never written.

## Writing a spec

- **Names** are camelCase (`showPoints`, `xAxis`); kinds and states are camelCase strings (`"line"`, `"arrayPointers"`, `"crossEdge"`).
- **Left out means default.** Leave out anything you don't need; `null`, empty lists and empty option groups (`"xAxis": {}`) mean the same as leaving them out.
- **Numbers must be finite.** JSON has no NaN or infinity: send `null` for a missing value.
  - In a chart, `null` (in `y`, or in `x` when given) is a gap. A line or area stops at it and starts again after it, a bar or slice is left out, and the header counts it as missing (`N: 7 • 1 missing`). A value between two gaps is drawn as a dot.
  - In a surface, `null` is a hole: the quads around it aren't drawn, and it doesn't count towards the height range.
  - A 3D point with a missing coordinate isn't drawn.
- **Every spec** has `title` and `subtitle`, shown in the visual's header, and `width` and `height` in pixels:
  - `width` is the widest the visual is drawn; it still fits into narrower space. Left out, the visual takes the width there is.
  - `height` is the drawing's height (the header, and a chart's legend, come on top). Left out, the studio picks one for the kind.
- **Values** in cells, nodes and items are JSON numbers, strings, booleans or `null`. The studio formats them, so they read the same from every language: invariant numbers, `true`/`false`, `∞` for the largest 32- and 64-bit integers. Anything else is sent as text; a list reads `[1,2,3]`, at most 6 items, then `…`.
- **Columns, not rows.** A chart series is `{"y": [...], "x": [...], "labels": [...]}` (as in a Plotly trace); every column given is as long as `y`. A 3D series is `x`, `y`, `z` of equal length.
- **Structures are flat.** Trees, graphs and lists are lists of nodes that name each other by `id`, so any depth fits and any language can build them in a loop.
- **An element** is named the same way everywhere (highlights, pointers, arrows, and later events): an item by index `3`, a node by id `"n1"`, a grid cell `[row, col]`, an edge `{"from": "a", "to": "b"}`.

## The three specs, in short

The schemas have every field and its meaning; this is the shape.

**Chart.** `kind` is `line`, `area`, `bar`, `scatter`, `pie`, `donut` or `histogram`. Also `xAxis` and `yAxis` (`{title, min, max}`), `legend` (`{show}`), `grid`, `showPoints`, `showStats`, `bins`, `color`, and `series` (`[{id, name, color, lineWidth, x, y, labels, colors, ids, values}]`). A histogram series gives `values` (the samples); the studio counts them into `bins` (default 10, at most 50) spread over the range of every series.
- An axis `min` or `max` you give is kept exactly, and whatever lies outside it is cut off at the edge. Left out, the range is the data's, with a little room; values that are all positive start from zero, and bars always include zero.
- The legend lists the series under the drawing. Left out, it shows when there is more than one series. A pie or donut always lists its slices beside it.
- `showPoints` marks every value on a line, where there is room for the marks (values at least 4 pixels apart). Left out, values are marked when no series has more than 40.
- Any number of values draws quickly, up to the limits below. A line with more values than the plot has pixel columns is drawn through the values that decide its picture (in each column its first, lowest, highest and last), so it looks the same. Scatter points on top of each other are drawn once, and with more bars than pixels each pixel column shows its tallest bar.
- `colors` gives single values their own colour: a bar, a slice, or the marker of a line or scatter point.

**3D plot.** Z is up for every kind, as in matplotlib and Plotly.
- `kind` is `scatter`, `trajectory`, `voxelBar`, `surface`, `wireframe` or `graph`.
- `xAxis`, `yAxis` and `zAxis` take titles. Also `colorMap`, `color`, `autoRotate`, `showAxes`, `showGrid`.
- Points go in `series` (`x`, `y`, `z`, `labels`, `sizes`, `colors`).
- A surface is `{x: {min, max}, y: {min, max}, z: [[...]]}`, one row of `z` per y value and one column per x value (numpy's `meshgrid` order).
- A graph is `{nodes: [{id, label, x, y, z, color, size}], edges: [{from, to, weight, color, directed}]}`. The studio lays out a graph whose nodes don't all have a position.

**Visualizer.**
- `kind` is one of:
  - `matrix`, `islands` or `board`, drawing `state.grid`;
  - `tree`, drawing `state.tree`;
  - `graph`, drawing `state.graph`;
  - `linkedList`, drawing `state.linkedList`;
  - `arrayPointers`, drawing `state.array`;
  - `bars`, drawing `state.bars`;
  - `canvas`, drawing `state.canvas`.
- `state` holds exactly the one part its kind draws.
- `steps` is a list of `{description, state | changes, highlight, pointers, notes, watches, line}`:
  - A step can give a whole new `state`, or `changes` to the previous one (cells by `[row, col]`, items by `index`, nodes by `id`, edges by `from`/`to`; only the fields given change), or neither to keep the previous state.
  - A step's `highlight`, `pointers` and `notes` belong to it alone.
  - The top-level `highlight` and `pointers` are drawn when there are no steps.
  - A pointer's `at` is `null` when the variable is null. An array pointer may sit one place before or after the items (where loops stop).
- One state vocabulary serves every visualizer: `default`, `current`, `visited`, `frontier`, `path`, `target`, `start`, `wall`, `candidate`, `matched`, `backtracked`, `pruned`, `swapped`, `relaxed`, `cycle`, `unreachable`, `rejected`, `crossEdge`, `pivot`, `done`. Each kind draws the states that mean something for it and draws the others as the nearest one it has, or as `default`.

## What the studio works out

| Spec | The studio… | Turn it off |
|---|---|---|
| a grid's values | reads what they say a cell is: 1 is land and 0 water, `"S"` the start, `"T"`/`"E"` the target, `"#"`/`"wall"` a wall | `"inferTerrain": false`, or give the cell a `terrain` |
| `kind: "islands"` | finds the islands and records the search | `"islands": {"recordSteps": false}`; `fourDirectional: false` counts diagonals |
| a tree with `"traversal"` | records a `preorder`, `inorder`, `postorder` or `levelOrder` walk | leave `traversal` out |
| a list with `"detectCycle": true` | records Floyd's tortoise and hare | leave it out |
| a list's next pointers | finds the cycle and marks the node it closes on | `"markCycle": false` (a node in the `cycle` state is always marked) |
| a graph without positions | lays it out | give every node `x` and `y` (and `z` in 3D) |

## Limits

The studio draws at most 200,000 chart values, 100,000 3D points, 500 × 500 surface heights, 5,000 graph nodes and 20,000 edges, 200 × 200 grid cells, 5,000 tree nodes, 2,000 list nodes, 10,000 array items or bars, 20,000 canvas shapes and 5,000 steps. It reads specs up to 32 MB (`Visuals/Json/VisualLimits.cs`). Past a limit it draws the first part and says so in the visual's header ("showing the first 5,000 of 8,200 steps"); a limit is never silent.

A visualizer with many steps holds only the steps being looked at: a step's data is built when it is drawn, from a copy kept every few dozen steps plus the changes since.

## When a spec is wrong

A spec that can't be read, or can't be drawn, is shown as an error in the output that says where and what, in words for whoever wrote the JSON:

- `$.series[0].y[3]: must be a number or null`
- `$.titel: unknown field "titel" (did you mean "title"?)`
- `$.kind: must be one of "line", "area", "bar", "scatter", "pie", "donut", "histogram"`
- `$.series[0].x: has 3 values but y has 4; give one for each`
- `$.state.tree.nodes[2].left: there is no node "z"`
- `$.steps[4].highlight[0]: must be a cell, like [2, 3] in a matrix visualizer`

A field the visual never draws is an error too (terrain on a board, a label on an array item, `z` on a graph visualizer's node), so nothing is silently left out of the picture.

## Updates and events

Every call that shows a visual returns a handle, in every language. `update` sends a new spec for the same output (an `update_display` message with the output's `transient.display_id`), and the studio redraws it where it is. An update resends the whole spec; helpers send at most 30 a second, the latest winning. An update that can't be drawn leaves the visual as it was, and says why in the output. An update can't change a chart into a 3D plot.

`on("click", fn)` (and `on_click`, `on_select`, `on_step`) sends `subscribe` for the output, and the callback gets what the user did, named as the spec names it:

| Event | Payload |
|---|---|
| `click` | `{"event": "click", "target": {…}, "modifiers": ["ctrl", "shift", "alt", "meta"]}` (`modifiers` left out when none is held) |
| `select` | `{"event": "select", "targets": [{…}, …]}`: Ctrl, Cmd or Shift and click add a target or take it away |
| `step` | `{"event": "step", "index": 4}`: a visualizer moved to that step |

A `target` is one of:

- a chart's value: `{"series": 0, "index": 2, "id": "b", "x": 3, "y": 7.5, "label": "Mar"}` (`id` from the series' `ids`, `label` from its `labels`);
- a 3D point: `{"series", "index", "x", "y", "z", "label"}`, or a graph node: `{"node": "a", "x", "y", "z", "label"}`;
- a visualizer's grid cell: `{"cell": [row, column]}`; an array or bar item: `{"item": 3}`; a list node: `{"item": 1, "node": "head"}` (the spec's `id`, else `n` + its index); a tree or graph node: `{"node": "a"}`.

Callbacks run when the program is idle (a notebook kernel between cells), or inside running code that asks: `process_events()` runs those whose events have come, and `wait()` runs them as they come, until Stop. A callback's output goes to the cell that showed the visual. How events reach a program is in [kernel-protocol.md](kernel-protocol.md): an `event` message on a kernel's stdin, or over the event socket for a program that is run.

## C# scripts written before the spec

C# builds these same specs (`Visuals/Building`: `ChartSpecBuilder`, `Plot3DSpecBuilder`, `VisualizerSpecBuilder`), so a C# chart is drawn exactly like a Python one. What changed for older scripts:

- Every `Display.*` visual call returns a handle (`DisplayHandle<ChartSpec>` and so on) instead of a control or options object; its `Spec` is what was shown.
- Every helper takes `configure`, which changes the spec last: `Display.LineChart(data, "Sales", configure: s => s.YAxis.Title = "EUR")`. Settings a call leaves out are the studio's defaults, as in every language (markers on a line of up to 40 values, a legend for more than one series).
- The extension for a visual is `Display` + its helper's name (`data.DisplayLineChart()`, `points.DisplayScatter3D()`, `grid.DisplayMatrix()`). The older names (`.LineChart()`, `.Chart()`, `.Scatter3D()`, `.Dump3D()` and the rest) still work but aren't offered by completion.
- 3D plots draw z up for every kind. Point plots used to draw y up, so they now look turned: z is the height.
- A 2D array of heights is a row per y and a column per x, as in numpy, over the rows' and columns' indices (it was read the other way round, over 0 to 10).
- Missing values (null, NaN, ∞) are gaps, not zeros, and a `null` in a list keeps its place.
- Trackers chain (`tracker.Visit(…).Mark(node, ElementState.Done)`), colour the same way (`Mark(element, state)` with the shared states, `Paint(element, color)`), and can be shown with `Display.Show(tracker)` or by ending a notebook cell with one.

## In a notebook

A notebook cell keeps every visual it shows, in order (at most 50, and it says so past that), and the notebook saves each one as its MIME type and spec, as a Jupyter notebook saves its outputs. A visual that can't be drawn when the notebook is opened again is shown as an empty frame with its title that says why, and is saved again unchanged:

- one larger than 4 MB of JSON isn't saved: run the cell again to redraw it;
- one saved by a newer version of the studio (a later major version of its MIME type);
- one whose saved spec is damaged, with what is wrong with it.

A display bundle whose only visual is of a version this studio doesn't read falls back to the bundle's other types (`text/plain`), as in Jupyter.
